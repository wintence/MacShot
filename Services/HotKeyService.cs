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
    /// <summary>'WN' — «снять окно», 'WO' — «снять область».</summary>
    public const int WindowHotKeyId = 0x574E;
    public const int RegionHotKeyId = 0x574F;

    private const int WmHotKey = 0x0312;

    private readonly HwndSource _source;

    /// <summary>Что и на каком id сейчас зарегистрировано.</summary>
    private readonly Dictionary<int, (bool Registered, Action? OnPressed)> _byId = new();

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

    public string? LastError { get; private set; }

    /// <summary>
    /// Регистрирует сочетание под данным id. Возвращает false, если клавишу
    /// уже держит другая программа.
    /// </summary>
    public bool Register(int id, string gesture, Action onPressed)
    {
        Unregister(id);

        if (!TryParse(gesture, out uint modifiers, out Key key))
        {
            LastError = I18n.Current["notify.hotkeyInvalid"];
            return false;
        }

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);

        // MOD_NOREPEAT здесь не используем: на этой машине с ним сочетание
        // считалось занятым там, где без него регистрация проходила.
        var registered = Native.RegisterHotKey(_source.Handle, id, modifiers, vk);

        _byId[id] = (registered, onPressed);

        if (registered)
            LastError = null;
        else
            LastError = Marshal.GetLastWin32Error() == Native.ERROR_HOTKEY_ALREADY_REGISTERED
                ? I18n.Current["notify.hotkeyBusy"]
                : I18n.Current["notify.hotkeyFailed"];

        return registered;
    }

    public void Unregister(int id)
    {
        if (_byId.TryGetValue(id, out var entry) && entry.Registered)
            Native.UnregisterHotKey(_source.Handle, id);

        _byId.Remove(id);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey && _byId.TryGetValue(wParam.ToInt32(), out var entry))
        {
            handled = true;
            AppLog.Write($"WM_HOTKEY received, id {wParam}");
            entry.OnPressed?.Invoke();
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
        // Снимаем регистрации до закрытия источника, иначе система
        // держала бы хоткеи за уже уничтоженное окно
        Unregister(WindowHotKeyId);
        Unregister(RegionHotKeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
