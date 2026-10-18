using System.Windows;
using System.Windows.Media.Imaging;

namespace MacShot.Capture;

/// <summary>
/// Pixel transfer between a GDI buffer and a WriteableBitmap.
///
/// The two directions are different and must not be confused:
/// <list type="bullet">
/// <item><description><c>CopyPixels</c> reads from a WriteableBitmap into an array.
/// Its third argument is the buffer size in bytes.</description></item>
/// <item><description><c>WritePixels</c> writes from an array into a WriteableBitmap.
/// Its third argument is the ROW STRIDE, not the buffer size. Pass the
/// buffer size and WPF computes stride * height, which
/// overflows an int.</description></item>
/// </list>
/// There is no reverse path through CopyPixels: BackBufferPointer is not
/// public in .NET 8, so WritePixels is the only way to get pixels in.
/// </summary>
internal static class PixelTransfer
{
    /// <summary>Copies pixels out of a WriteableBitmap into an array.</summary>
    public static void Read(WriteableBitmap bitmap, int width, int height, byte[] buffer)
    {
        // Третий параметр у CopyPixels — тоже шаг строки, а не размер буфера
        bitmap.CopyPixels(new Int32Rect(0, 0, width, height), buffer, width * 4, 0);
    }

    /// <summary>Copies an array back into a WriteableBitmap.</summary>
    public static void Write(WriteableBitmap bitmap, int width, int height, byte[] buffer)
    {
        int stride = width * 4;
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), buffer, stride, 0);
    }

    /// <summary>Copies a GDI buffer (BGRA) into an array.</summary>
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
