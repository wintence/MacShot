using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacShot.Interop;

using MacShot.Lang;

namespace MacShot.Capture;

/// <summary>
/// Захват экрана в физическом разрешении.
///
/// Сценарий «заморозки»: сначала снимается весь виртуальный экран
/// (CaptureVirtualScreen), оверлей показывает этот кадр вместо живого экрана,
/// а итоговый снимок вырезается из кадра (CropRegion). Поэтому во время
/// выбора анимации, видео и всё остальное визуально «остановлены», и снимок
/// выходит ровно тем, что видел пользователь, а не тем, что уже уехало.
///
/// Окна тут не участвуют: PrintWindow не подходит, кадр снимается прямо
/// с экрана через BitBlt — тем же резервным способом, которым
/// WindowCapturer снимает перекрытые окна.
/// </summary>
internal static class ScreenCapturer
{
    /// <summary>
    /// Снимает весь виртуальный экран — все мониторы одной картиной.
    ///
    /// Вызывается до показа оверлея, поэтому в кадр не попадает ни
    /// затемнение, ни рамка выделения. Координаты кадра — физические
    /// пиксели виртуального экрана, тот же базис, что и у GetCursorPos.
    /// </summary>
    public static FrozenScreen CaptureVirtualScreen()
    {
        Native.RECT frame = MonitorUnion();

        int width = frame.Width;
        int height = frame.Height;

        if (width <= 0 || height <= 0)
            throw new CaptureException(I18n.Current["cap.noArea"]);

        try
        {
            using var surface = new DibSurface(width, height);

            var screenDc = DibSurface.GetDC(IntPtr.Zero);
            try
            {
                if (!DibSurface.BitBlt(surface.Hdc, 0, 0, width, height,
                        screenDc, frame.Left, frame.Top,
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

            var bitmap = FromSurface(surface, width, height);
            bitmap.Freeze();

            return new FrozenScreen(bitmap, frame);
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

    /// <summary>
    /// Вырезает прямоугольник из замороженного кадра. Координаты — физические
    /// пиксели экрана, как их отдаёт GetCursorPos.
    ///
    /// Прямоугольник может вылезти за край виртуального экрана (рамку тянули
    /// к краю), а CroppedBitmap такое не прощает — поэтому сначала обрезаем.
    /// </summary>
    public static WriteableBitmap CropRegion(FrozenScreen frozen, Native.RECT rect)
    {
        rect = ClampTo(rect, frozen.Frame);

        int width = rect.Width;
        int height = rect.Height;

        if (width <= 0 || height <= 0)
            throw new CaptureException(I18n.Current["cap.noArea"]);

        try
        {
            var piece = new CroppedBitmap(frozen.Bitmap, new Int32Rect(
                rect.Left - frozen.Frame.Left,
                rect.Top - frozen.Frame.Top,
                width, height));
            piece.Freeze();

            // Дальше по цепочке (скругление углов, срез заголовка) ждут
            // перезаписываемый bitmap: копия снимает «замороженность»
            return new WriteableBitmap(piece);
        }
        catch (Exception ex)
        {
            throw new CaptureException(I18n.Format("cap.screenFailed", ex.Message));
        }
    }

    /// <summary>Переносит GDI-кадр в WriteableBitmap с полной альфой.</summary>
    private static WriteableBitmap FromSurface(DibSurface surface, int width, int height)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

        var buffer = new byte[width * height * 4];
        PixelTransfer.FromGdi(surface.Bits, width, height, buffer);

        // GDI оставляет альфу нулём — при вставке в JPEG или старые
        // приложения кадр вышел бы прозрачным. Приводим к полному цвету.
        for (int i = 3; i < buffer.Length; i += 4)
            buffer[i] = 255;

        PixelTransfer.Write(bitmap, width, height, buffer);
        return bitmap;
    }

    /// <summary>Пересечение прямоугольника с пределами кадра.</summary>
    private static Native.RECT ClampTo(Native.RECT rect, Native.RECT frame)
    {
        int left = Math.Max(rect.Left, frame.Left);
        int top = Math.Max(rect.Top, frame.Top);
        int right = Math.Min(rect.Right, frame.Right);
        int bottom = Math.Min(rect.Bottom, frame.Bottom);

        return new Native.RECT
        {
            Left = left,
            Top = top,
            Right = Math.Max(left, right),
            Bottom = Math.Max(top, bottom),
        };
    }

    /// <summary>
    /// Объединение всех мониторов в физических пикселях. EnumDisplayMonitors
    /// отдаёт rcMonitor в физических координатах независимо от DPI процесса —
    /// в отличие от GetSystemMetrics, который под PMv2 виртуализируется.
    /// </summary>
    private static Native.RECT MonitorUnion()
    {
        var monitors = new List<Native.RECT>();

        try
        {
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdc, ref Native.RECT monitor, IntPtr data) =>
                {
                    monitors.Add(monitor);
                    return true;
                }, IntPtr.Zero);
        }
        catch
        {
            // user32 внезапно не смогла — ниже откат на системные метрики
        }

        if (monitors.Count > 0)
        {
            var union = monitors[0];

            foreach (var monitor in monitors)
            {
                union.Left = Math.Min(union.Left, monitor.Left);
                union.Top = Math.Min(union.Top, monitor.Top);
                union.Right = Math.Max(union.Right, monitor.Right);
                union.Bottom = Math.Max(union.Bottom, monitor.Bottom);
            }

            return union;
        }

        // Откат: перечисление не удалось, берём системные метрики. Под PMv2
        // они могут быть виртуализированы, но без кадра оверлею показывать
        // нечего — неточный прямоугольник лучше нулевого.
        Native.RECT fallback = Native.VirtualScreenRect();
        return fallback.Width > 0 && fallback.Height > 0 ? fallback : default;
    }
}
