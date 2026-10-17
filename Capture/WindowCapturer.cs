using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacShot.Interop;

using MacShot.Lang;

namespace MacShot.Capture;

internal sealed class CaptureException : Exception
{
    public CaptureException(string message) : base(message) { }
}

internal enum CaptureMethod
{
    PrintWindow,
    ScreenCopy,
}

/// <summary>
/// Захват окна в BitmapSource без системной тени Windows.
/// </summary>
internal static class WindowCapturer
{
    /// <summary>
    /// Сколько ждём PrintWindow. На окнах DirectComposition (Chromium, Electron,
    /// часть игр) вызов уходит в бесконечное ожидание — проверено на практике,
    /// процесс зависал на минуты. Поэтому ограничиваем время и переходим
    /// на копирование области экрана.
    /// </summary>
    public static int PrintWindowTimeoutMs { get; set; } = 2000;

    public static CaptureResult Capture(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            throw new CaptureException(I18n.Current["cap.notFound"]);

        if (!Native.IsWindowVisible(hwnd))
            throw new CaptureException(I18n.Current["cap.invisible"]);

        if (Native.IsIconic(hwnd))
            throw new CaptureException(I18n.Current["cap.minimized"]);

        var bounds = WindowFinder.GetCaptureBounds(hwnd);
        int width = bounds.Width;
        int height = bounds.Height;

        if (width <= 0 || height <= 0)
            throw new CaptureException(I18n.Current["cap.noArea"]);

        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);

        byte[]? pixels;
        string error;

        if (!TryPrintWindow(hwnd, width, height, out pixels, out error))
            pixels = null;

        if (pixels is not null)
        {
            // Запись в WriteableBitmap допустима только из потока диспетчера,
            // поэтому из буфера GDI переносим пиксели здесь, а не в рабочем потоке.
            PixelTransfer.Write(bitmap, width, height, pixels);
        }
        else if (!TryScreenCopy(bounds, width, height, bitmap, out error))
        {
            throw new CaptureException(I18n.Format("cap.imageFailed", error));
        }

        // GDI альфа-канал не заполняет: после PrintWindow байт A равен нулю,
        // и прозрачность в углах получится «дырявой». Принудительно делаем
        // кадр непрозрачным — скругление добавит прозрачность позже.
        MakeOpaque(bitmap);
        bitmap.Freeze();

        var method = pixels is not null ? CaptureMethod.PrintWindow : CaptureMethod.ScreenCopy;

        return new CaptureResult(bitmap, bounds, method);
    }

    /// <summary>
    /// PrintWindow выполняется в отдельном потоке с таймаутом.
    ///
    /// Геометрия важна. Печатается всё окно целиком, по границам GetWindowRect,
    /// а потом кадр обрезается до DWM-границ. Если сразу рисовать в буфер
    /// размером с видимую область, содержимое сместится на ширину невидимой
    /// рамки: слева появляется полоса, справа кадр обрезается.
    ///
    /// Рабочий поток отдаёт только сырой буфер: трогать WPF-объекты оттуда нельзя,
    /// WriteableBitmap привязан к потоку диспетчера и отвечает отказом.
    /// </summary>
    private static bool TryPrintWindow(IntPtr hwnd, int width, int height, out byte[]? pixels, out string error)
    {
        pixels = null;
        error = "";

        Native.GetWindowRect(hwnd, out var full);

        int renderWidth = full.Width;
        int renderHeight = full.Height;

        // На сколько видимая область смещена относительно всего окна
        int cropX = WindowFinder.GetCaptureBounds(hwnd).Left - full.Left;
        int cropY = WindowFinder.GetCaptureBounds(hwnd).Top - full.Top;

        // На всякий случай прижимаем к нулю и к размеру буфера
        cropX = Math.Clamp(cropX, 0, Math.Max(0, renderWidth - width));
        cropY = Math.Clamp(cropY, 0, Math.Max(0, renderHeight - height));

        if (renderWidth <= 0 || renderHeight <= 0)
        {
            error = I18n.Current["cap.zeroBounds"];
            return false;
        }

        byte[]? buffer = null;
        string failure = "";

        var worker = Task.Run(() =>
        {
            using var surface = new DibSurface(renderWidth, renderHeight);

            if (!Native.PrintWindow(hwnd, surface.Hdc, Native.PW_RENDERFULLCONTENT))
            {
                failure = I18n.Format("cap.printFalse", Marshal.GetLastWin32Error());
                return;
            }

            var whole = new byte[renderWidth * renderHeight * 4];
            PixelTransfer.FromGdi(surface.Bits, renderWidth, renderHeight, whole);

            buffer = cropX == 0 && cropY == 0 && renderWidth == width && renderHeight == height
                ? whole
                : Crop(whole, renderWidth, renderHeight, cropX, cropY, width, height);
        });

        if (!worker.Wait(PrintWindowTimeoutMs))
        {
            error = I18n.Format("cap.printTimeout", PrintWindowTimeoutMs);
            return false;
        }

        if (worker.IsFaulted)
        {
            var inner = worker.Exception?.InnerException ?? worker.Exception;
            error = inner?.ToString() ?? "неизвестная ошибка";
            return false;
        }

        if (buffer is null)
        {
            error = failure;
            return false;
        }

        pixels = buffer;
        return true;
    }

    /// <summary>Вырезает из кадра прямоугольник нужного размера (BGRA).</summary>
    private static byte[] Crop(
        byte[] source, int sourceWidth, int sourceHeight, int x, int y, int width, int height)
    {
        var result = new byte[width * height * 4];

        for (int row = 0; row < height; row++)
        {
            var from = ((y + row) * sourceWidth + x) * 4;
            var to = row * width * 4;
            Array.Copy(source, from, result, to, width * 4);
        }

        return result;
    }

    /// <summary>Резервный путь: копируем область экрана через BitBlt.</summary>
    private static bool TryScreenCopy(Native.RECT bounds, int width, int height,
        WriteableBitmap bitmap, out string error)
    {
        error = "";

        try
        {
            using var surface = new DibSurface(width, height);

            var screenDc = DibSurface.GetDC(IntPtr.Zero);
            try
            {
                if (!DibSurface.BitBlt(surface.Hdc, 0, 0, width, height,
                        screenDc, bounds.Left, bounds.Top,
                        DibSurface.SRCCOPY | DibSurface.CAPTUREBLT))
                {
                    error = I18n.Format("cap.printFalse", Marshal.GetLastWin32Error());
                    return false;
                }
            }
            finally
            {
                DibSurface.ReleaseDC(IntPtr.Zero, screenDc);
            }

            var buffer = new byte[width * height * 4];
            PixelTransfer.FromGdi(surface.Bits, width, height, buffer);
            PixelTransfer.Write(bitmap, width, height, buffer);

            return true;
        }
        catch (Exception ex)
        {
            error = ex.ToString();
            return false;
        }
    }

    /// <summary>Проставляет A = 255 во всём кадре.</summary>
    private static void MakeOpaque(WriteableBitmap bitmap)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;

        var buffer = new byte[width * height * 4];
        PixelTransfer.Read(bitmap, width, height, buffer);

        // BGRA: альфа — каждый 4-й байт, начиная с 3-го
        for (int i = 3; i < buffer.Length; i += 4)
            buffer[i] = 255;

        PixelTransfer.Write(bitmap, width, height, buffer);
    }
}

internal sealed record CaptureResult(
    BitmapSource Bitmap,
    Native.RECT Bounds,
    CaptureMethod Method);
