using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MacShot.Interop;

/// <summary>
/// Объявления Win32 API. Только то, что реально нужно приложению.
/// </summary>
internal static class Native
{
    // ---- Захват окна ----

    public const uint PW_RENDERFULLCONTENT = 0x00000002;

    /// <summary>
    /// Рисует окно в переданный DC. С PW_RENDERFULLCONTENT работает даже для
    /// перекрытого окна, но на окнах DirectComposition (Chromium/Electron)
    /// может уходить в бесконечное ожидание — поэтому вызываем с таймаутом.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int DWMWA_CLOAKED = 14;

    /// <summary>
    /// Реальные границы окна без невидимой рамки. GetWindowRect возвращает
    /// прямоугольник с зарезервированной под тень полосой — из-за этого в снимок
    /// попадают серые полосы по краям.
    /// </summary>
    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern void DwmGetWindowAttributeInt(IntPtr hwnd, int attr, out int value, int size);

    /// <summary>
    /// Окно скрыто: свёрнуто в оболочку, находится на другом виртуальном рабочем
    /// столе или отрисовано невидимым способом. Такие окна снимать бессмысленно.
    /// </summary>
    public static bool IsCloaked(IntPtr hwnd)
    {
        try
        {
            DwmGetWindowAttributeInt(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
            return cloaked != 0;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowTextLength(IntPtr hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    /// <summary>Окно под экранной точкой. Может вернуть дочернее окно.</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT point);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    public const uint GA_ROOT = 2;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT point);

    /// <summary>
    /// DPI конкретного окна. Нужен, а не общий DPI монитора: при переносе
    /// окна между экранами с разным масштабом координаты съедут.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    /// <summary>Масштаб окна относительно 96 DPI: 1.0 = 100%, 1.25 = 125% и т.д.</summary>
    public static double GetScale(IntPtr hwnd)
    {
        try
        {
            uint dpi = GetDpiForWindow(hwnd);
            return dpi > 0 ? dpi / 96.0 : 1.0;
        }
        catch
        {
            return 1.0;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(
        IntPtr process, uint flags, System.Text.StringBuilder name, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    /// <summary>
    /// Путь к исполняемому файлу, которому принадлежит окно.
    ///
    /// Это ключ для настроек, привязанных к приложению: заголовок у всех
    /// окон одного приложения одинаковый, а у одного и того же приложения
    /// на разных запусках — тоже. Привязываться к самому окну нельзя, его
    /// дескриптор не переживает перезапуск.
    /// </summary>
    public static string GetAppPath(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0)
                return string.Empty;

            IntPtr process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (process == IntPtr.Zero)
                return string.Empty;

            try
            {
                uint size = 1024;
                var buffer = new System.Text.StringBuilder((int)size);
                return QueryFullProcessImageName(process, 0, buffer, ref size)
                    ? buffer.ToString()
                    : string.Empty;
            }
            finally
            {
                CloseHandle(process);
            }
        }
        catch
        {
            return string.Empty;
        }
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    /// <summary>
    /// Высота системного заголовка этой системы, DIP.
    ///
    /// Нужна только предпросмотру: окна там нет, а показать надо что-то
    /// похожее на настоящее. SM_CYSCAPTION у процесса с поддержкой DPI
    /// уже возвращает значение под его масштабом.
    /// </summary>
    public static double GetSystemCaptionHeight()
    {
        const int SM_CYSCAPTION = 4;

        try
        {
            int height = GetSystemMetrics(SM_CYSCAPTION);
            return height > 0 ? height : 32;
        }
        catch
        {
            return 32;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hwnd, ref POINT point);

    /// <summary>
    /// Высота системного заголовка окна в DIP.
    ///
    /// Считается как разница между верхом видимой рамки (её мы и снимаем)
    /// и верхом клиентской области. У окон со штатным заголовком выходит его
    /// настоящая высота вместе с невидимой рамкой захвата.
    ///
    /// У окон без системного заголовка — браузеров, Electron-приложений —
    /// выходит около нуля: клиентская область начинается сразу под рамкой, и
    /// резать там нечего. Именно поэтому фиксированная высота резала у таких
    /// окон кусок содержимого.
    /// </summary>
    public static double GetTitleBarHeight(IntPtr hwnd, double scale)
    {
        try
        {
            var frame = new RECT();
            DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out frame, System.Runtime.InteropServices.Marshal.SizeOf<RECT>());

            var origin = new POINT { X = 0, Y = 0 };
            if (!ClientToScreen(hwnd, ref origin))
                return 0;

            double height = (origin.Y - frame.Top) / scale;

            return height > 0 ? height : 0;
        }
        catch
        {
            return 0;
        }
    }

    // ---- Мышь и клавиатура ----

    /// <summary>
    /// Состояние кнопки/клавиши. Нужно потому, что прозрачный оверлей не
    /// получает события мыши: система ищет его попадание по альфе, а она нулевая.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vk);

    public const int VK_LBUTTON = 0x01;
    public const int VK_ESCAPE = 0x1B;

    // ---- Горячие клавиши ----

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    public const int ERROR_HOTKEY_ALREADY_REGISTERED = 1409;

    // ---- Окно и стили ----

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

    /// <summary>GetWindowLong с поправкой на разрядность процесса.</summary>
    public static int GetWindowLongAuto(IntPtr hwnd, int index) =>
        IntPtr.Size == 8
            ? unchecked((int)(long)GetWindowLongPtr64(hwnd, index))
            : GetWindowLong32(hwnd, index);

    public static int SetWindowLongAuto(IntPtr hwnd, int index, int value)
    {
        if (IntPtr.Size == 8)
            return unchecked((int)(long)SetWindowLongPtr64(hwnd, index, new IntPtr(value)));
        return SetWindowLong32(hwnd, index, value);
    }

    public static void AddExStyle(IntPtr hwnd, int flags) =>
        SetWindowLongAuto(hwnd, GWL_EXSTYLE, GetWindowLongAuto(hwnd, GWL_EXSTYLE) | flags);

    // ---- Оформление окна ----

    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>
    /// Скругляет углы окна без системной рамки. Признак появился в Windows 11
    /// (22H2): там DWM сам обрезает окно по радиусу, и клиентская область не
    /// вылезает за скругление.
    ///
    /// На Windows 10 вызов просто ничего не делает — возвращаемся молча,
    /// окно остаётся с прямыми углами, как и было до этой правки.
    /// </summary>
    public static void RoundCorners(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        try
        {
            int round = 2; // DWMWCP_ROUND
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        }
        catch
        {
            // Старая сборка dwmapi — атрибута нет, скругления не будет
        }
    }

    // ---- Структуры ----

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }
}
