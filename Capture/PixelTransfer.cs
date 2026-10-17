using System.Windows;
using System.Windows.Media.Imaging;

namespace MacShot.Capture;

/// <summary>
/// Обмен пикселями между буфером GDI и WriteableBitmap.
///
/// Направления разные и путать их нельзя:
/// <list type="bullet">
/// <item><description><c>CopyPixels</c> — читает из WriteableBitmap в массив.
/// Третий параметр — размер буфера в байтах.</description></item>
/// <item><description><c>WritePixels</c> — пишет из массива в WriteableBitmap.
/// Третий параметр — ШАГ СТРОКИ, а не размер буфера. Если передать
/// размер всего буфера, WPF посчитает stride * height и получит
/// переполнение int.</description></item>
/// </list>
/// Обратного пути у CopyPixels нет: BackBufferPointer в .NET 8 закрыт,
/// поэтому единственный способ записать пиксели — WritePixels.
/// </summary>
internal static class PixelTransfer
{
    /// <summary>Копирует пиксели из WriteableBitmap в массив.</summary>
    public static void Read(WriteableBitmap bitmap, int width, int height, byte[] buffer)
    {
        // Третий параметр у CopyPixels — тоже шаг строки, а не размер буфера
        bitmap.CopyPixels(new Int32Rect(0, 0, width, height), buffer, width * 4, 0);
    }

    /// <summary>Копирует массив обратно в WriteableBitmap.</summary>
    public static void Write(WriteableBitmap bitmap, int width, int height, byte[] buffer)
    {
        int stride = width * 4;
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), buffer, stride, 0);
    }

    /// <summary>Копирует буфер GDI (BGRA) в массив.</summary>
    public static unsafe void FromGdi(IntPtr bits, int width, int height, byte[] buffer)
    {
        int stride = width * 4;

        fixed (byte* destination = buffer)
        {
            for (int y = 0; y < height; y++)
                System.Buffer.MemoryCopy(
                    (byte*)bits + (long)y * stride,
                    destination + (long)y * stride,
                    stride, stride);
        }
    }
}
