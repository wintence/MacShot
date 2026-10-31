using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacShot.Interop;

using MacShot.Lang;

namespace MacShot.Capture;

/// <summary>
/// Захват произвольной области экрана в физическом разрешении.
///
/// Окно тут не участвует: PrintWindow не подходит, кадр снимается прямо
/// с экрана через BitBlt — тем же резервным способом, которым
/// WindowCapturer снимает перекрытые окна.
/// </summary>
internal static class ScreenCapturer
{
    /// <summary>
    /// Снимает прямоугольник экрана. Координаты — физические пиксели
    /// виртуального экрана, такие же, как у GetCursorPos.
    /// </summary>
    public static WriteableBitmap CaptureRegion(Native.RECT rect)
    {
        int width = rect.Width;
        int height = rect.Height;

        if (width <= 0 || height <= 0)
            throw new CaptureException(I18n.Current["cap.noArea"]);

        try
        {
            using var surface = new DibSurface(width, height);

            var screenDc = DibSurface.GetDC(IntPtr.Zero);
            try
            {
                if (!DibSurface.BitBlt(surface.Hdc, 0, 0, width, height,
                        screenDc, rect.Left, rect.Top,
                        DibSurface.SRCCOPY | DibSurface.CAPTUREBLT))
                {
                    throw new CaptureException(I18n.Format(
                        "cap.screenFailed", Marshal.GetLastWin32Error()));
                }
            }
            finally
            {
                DibSurface.ReleaseDC(IntPtr.Zero, screenDc);
            }

            var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

            var buffer = new byte[width * height * 4];
            PixelTransfer.FromGdi(surface.Bits, width, height, buffer);

            // GDI оставляет альфу нулём — при вставке в JPEG или старые
            // приложения кадр вышел бы прозрачным. Приводим к полному цвету.
            for (int i = 3; i < buffer.Length; i += 4)
                buffer[i] = 255;

            PixelTransfer.Write(bitmap, width, height, buffer);
            bitmap.Freeze();

            return bitmap;
        }
        catch (CaptureException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new CaptureException(I18n.Format("cap.screenFailed", ex.Message));
        }
    }
}
