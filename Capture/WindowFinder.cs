using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using MacShot.Interop;

namespace MacShot.Capture;

/// <summary>
/// Перечисление и проверка окон верхнего уровня.
/// </summary>
internal static class WindowFinder
{
    /// <summary>Заголовки окон, которые нельзя снимать: они не несут смысла и мешают.</summary>
    private static readonly HashSet<string> IgnoredTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Program Manager",
        "Windows Input Experience",
        "MSCTFIME UI",
        "Default IME",
        "Setup",
    };

    private static uint _ownProcessId = (uint)Process.GetCurrentProcess().Id;

    /// <summary>
    /// Возвращает все окна, которые имеет смысл предложить для захвата.
    /// </summary>
    public static List<WindowInfo> Enumerate()
    {
        var result = new List<WindowInfo>();

        Native.EnumWindows((hwnd, _) =>
        {
            var info = Inspect(hwnd);
            if (info.HasValue)
                result.Add(info.Value);
            return true;
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>
    /// Возвращает окно верхнего уровня под экранной точкой или null,
    /// если под курсором ничего подходящего нет.
    /// </summary>
    public static WindowInfo? FindAt(int screenX, int screenY)
    {
        var child = Native.WindowFromPoint(new Native.POINT { X = screenX, Y = screenY });
        if (child == IntPtr.Zero)
            return null;

        var root = Native.GetAncestor(child, Native.GA_ROOT);
        if (root == IntPtr.Zero)
            root = child;

        return Inspect(root);
    }

    /// <summary>
    /// Возвращает окно, если оно пригодно для захвата, иначе null.
    /// </summary>
    public static WindowInfo? Inspect(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        if (!Native.IsWindowVisible(hwnd))
            return null;

        // Окно может быть скрытым: свёрнуто в оболочку, на другом виртуальном
        // рабочем столе или отрисовано невидимым способом.
        if (Native.IsCloaked(hwnd))
            return null;

        // Свои окна и трей предлагать нельзя
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == _ownProcessId)
            return null;

        var title = GetTitle(hwnd);
        if (string.IsNullOrWhiteSpace(title) || IgnoredTitles.Contains(title))
            return null;

        var bounds = GetCaptureBounds(hwnd);
        if (bounds.Width < 80 || bounds.Height < 40)
            return null;

        return new WindowInfo(hwnd, title, bounds);
    }

    /// <summary>
    /// Границы окна для захвата: DWM extended frame bounds, с откатом на
    /// GetWindowRect. Именно так уходит невидимая рамка, из-за которой
    /// по краям снимка появляются серые полосы.
    /// </summary>
    public static Native.RECT GetCaptureBounds(IntPtr hwnd)
    {
        try
        {
            Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS,
                out Native.RECT rect, System.Runtime.InteropServices.Marshal.SizeOf<Native.RECT>());

            if (rect.Width > 0 && rect.Height > 0)
                return rect;
        }
        catch
        {
            // DWM недоступен — падаем на Win32
        }

        Native.GetWindowRect(hwnd, out var fallback);
        return fallback;
    }

    public static string GetTitle(IntPtr hwnd)
    {
        int length = Native.GetWindowTextLength(hwnd);
        if (length <= 0)
            return string.Empty;

        var buffer = new StringBuilder(length + 1);
        Native.GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    /// <summary>Окно успело закрыться или уехало в фон между выбором и захватом.</summary>
    public static bool HasClosed(IntPtr hwnd) => !Native.IsWindowVisible(hwnd);
}

internal readonly record struct WindowInfo(IntPtr Handle, string Title, Native.RECT Bounds);
