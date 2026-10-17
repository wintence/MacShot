using System;
using System.Runtime.InteropServices;

namespace MacShot.Interop;

/// <summary>
/// Буфер под кадр, выделенный GDI как DIBSection.
/// Это позволяет получить указатель на пиксели напрямую, без промежуточного
/// System.Drawing.Bitmap и без потери скорости на копировании.
/// </summary>
internal sealed class DibSurface : IDisposable
{
    private IntPtr _screenDc;
    private IntPtr _dc;
    private IntPtr _dib;
    private IntPtr _oldBitmap;

    /// <summary>Указатель на пиксели BGRA (32 бита на пиксель).</summary>
    public IntPtr Bits { get; }

    /// <summary>DC, в который рисует PrintWindow.</summary>
    public IntPtr Hdc => _dc;

    public DibSurface(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Размеры должны быть положительными.");

        var info = new BITMAPINFO();
        info.Header = new BITMAPINFOHEADER
        {
            Size = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            Width = width,
            // Отрицательная высота = top-down, как ожидает WPF.
            Height = -height,
            Planes = 1,
            BitCount = 32,
            Compression = 0, // BI_RGB
        };

        _screenDc = GetDC(IntPtr.Zero);
        if (_screenDc == IntPtr.Zero)
            throw new InvalidOperationException("Не удалось получить DC экрана.");

        _dib = CreateDIBSection(_screenDc, ref info, 0, out IntPtr bits, IntPtr.Zero, 0);
        if (_dib == IntPtr.Zero || bits == IntPtr.Zero)
        {
            ReleaseDC(IntPtr.Zero, _screenDc);
            _screenDc = IntPtr.Zero;
            throw new InvalidOperationException(
                $"Не удалось создать DIB ({width}x{height}), код {Marshal.GetLastWin32Error()}.");
        }

        Bits = bits;

        _dc = CreateCompatibleDC(_screenDc);
        _oldBitmap = SelectObject(_dc, _dib);
    }

    public void Dispose()
    {
        if (_dc != IntPtr.Zero)
        {
            SelectObject(_dc, _oldBitmap);
            DeleteObject(_dc);
            _dc = IntPtr.Zero;
        }
        if (_dib != IntPtr.Zero)
        {
            DeleteObject(_dib);
            _dib = IntPtr.Zero;
        }
        if (_screenDc != IntPtr.Zero)
        {
            ReleaseDC(IntPtr.Zero, _screenDc);
            _screenDc = IntPtr.Zero;
        }
    }

    // ---- Объявления GDI ----

    public const uint SRCCOPY = 0x00CC0020;
    public const uint CAPTUREBLT = 0x40000000;

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO info, uint usage,
        out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeleteObject(IntPtr handle);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr handle);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height,
        IntPtr source, int srcX, int srcY, uint rop);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReleaseDC(IntPtr hwnd, IntPtr dc);

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFO
    {
        public BITMAPINFOHEADER Header;
        public uint Colors;
    }
}
