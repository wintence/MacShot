using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using MacShot.Interop;

using MacShot.Lang;

namespace MacShot.Services;

/// <summary>
/// Глобальная горячая клавиша через RegisterHotKey: работает, когда
/// приложение свёрнуто в трей и не в фокусе.
/// </summary>
internal sealed class HotKeyService : IDisposable
{
    private const int HotKeyId = 0x574E; // 'WN'
    private const int WmHotKey = 0x0312;

    private readonly HwndSource _source;
    private Action? _onPressed;
    private bool _registered;

    public HotKeyService()
    {
        // Скрытое окно нужно, чтобы WPF доставлял нам WM_HOTKEY
        _source = new HwndSource(new HwndSourceParameters("MacShotHotKey")
        {
            Width = 0,
            Height = 0,
            PositionX = -32000,
            PositionY = -32000,
            WindowStyle = 0,
        });

        _source.AddHook(WndProc);
    }

    public string? ActiveGesture { get; private set; }

    /// <summary>Последняя ошибка регистрации — показывается пользователю.</summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Регистрирует сочетание. Возвращает false, если клавишу уже держит
    /// другая программа.
    /// </summary>
    public bool Register(string gesture, Action onPressed)
    {
        Unregister();

        if (!TryParse(gesture, out uint modifiers, out Key key))
        {
            LastError = I18n.Current["notify.hotkeyInvalid"];
            return false;
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        _onPressed = onPressed;

        // MOD_NOREPEAT здесь не используем: на этой машине с ним сочетание
        // считалось занятым там, где без него регистрация проходила.
        _registered = Native.RegisterHotKey(_source.Handle, HotKeyId, modifiers, vk);

        if (_registered)
        {
            ActiveGesture = gesture;
            LastError = null;
        }
        else
        {
            ActiveGesture = null;
            LastError = Marshal.GetLastWin32Error() == Native.ERROR_HOTKEY_ALREADY_REGISTERED
                ? I18n.Current["notify.hotkeyBusy"]
                : I18n.Current["notify.hotkeyFailed"];
        }

        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            Native.UnregisterHotKey(_source.Handle, HotKeyId);
            _registered = false;
        }

        ActiveGesture = null;
        _onPressed = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && wParam.ToInt32() == HotKeyId)
        {
            handled = true;
            AppLog.Write("WM_HOTKEY received");
            _onPressed?.Invoke();
        }

        return IntPtr.Zero;
    }

    /// <summary>Разбирает "Ctrl+Shift+8" в модификаторы и виртуальный код.</summary>
    public static bool TryParse(string? gesture, out uint modifiers, out Key key)
    {
        modifiers = 0;
        key = Key.None;

        if (string.IsNullOrWhiteSpace(gesture))
            return false;

        var hasModifier = false;

        foreach (var part in gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control" or "ctl":
                    modifiers |= Native.MOD_CONTROL; hasModifier = true; continue;
                case "alt":
                    modifiers |= Native.MOD_ALT; hasModifier = true; continue;
                case "shift":
                    modifiers |= Native.MOD_SHIFT; hasModifier = true; continue;
                case "win" or "windows" or "meta":
                    modifiers |= Native.MOD_WIN; hasModifier = true; continue;
            }

            if (!Enum.TryParse<Key>(part, ignoreCase: true, out var parsed) || parsed == Key.None)
                return false;

            key = parsed;
        }

        // Без модификатора такой комбинации мы бы перехватывали обычный ввод
        return hasModifier && key != Key.None;
    }

    public static bool IsValid(string gesture) => TryParse(gesture, out _, out _);

    /// <summary>Приводит сочетание к каноническому виду для сохранения.</summary>
    public static string Normalize(string gesture) =>
        TryParse(gesture, out uint modifiers, out Key key)
            ? Describe(modifiers, key)
            : "Ctrl+Shift+8";

    /// <summary>
    /// Подпись сочетания для экрана.
    ///
    /// В настройках хранится имя клавиши из перечисления Windows
    /// ("Ctrl+OemPlus"): по нему сочетание разбирается обратно. Человеку же
    /// понятнее знак на клавише — "Ctrl+=". Меняем только подпись, хранение
    /// остаётся прежним, иначе старые настройки перестали бы читаться.
    /// </summary>
    public static string Friendly(string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
            return string.Empty;

        var parts = gesture.Split('+');

        for (int i = 0; i < parts.Length; i++)
        {
            parts[i] = parts[i] switch
            {
                "OemPlus" => "=",
                "OemMinus" => "-",
                "OemComma" => ",",
                "OemPeriod" => ".",
                "OemQuestion" => "/",
                "OemSemicolon" => ";",
                "OemOpenBrackets" => "[",
                "OemCloseBrackets" => "]",
                "OemQuotes" => "'",
                "OemPipe" => "\\",
                "OemBackslash" => "\\",
                "OemTilde" => "`",
                _ when parts[i].Length == 2 && parts[i][0] == 'D' && char.IsDigit(parts[i][1])
                    => parts[i][1].ToString(),
                _ => parts[i],
            };
        }

        return string.Join("+", parts);
    }

    private static string Describe(uint modifiers, Key key)
    {
        var parts = new System.Collections.Generic.List<string>(4);

        if ((modifiers & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & Native.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & Native.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & Native.MOD_WIN) != 0) parts.Add("Win");

        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
