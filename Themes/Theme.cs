namespace MacShot.Themes;

/// <summary>
/// Применённая тема оформления.
///
/// Кисти раздаются ресурсам приложения, и разметка берёт их через
/// {DynamicResource}. Благодаря этому смена темы перекрашивает уже открытые
/// окна на лету: пересоздавать окно не нужно, в отличие от языка, строки
/// которого читаются при разборе разметки.
///
/// Элементы, которые строится кодом (строки списков, макет окна в
/// предпросмотре), тему не помнят: им красят либо ссылкой на ресурс, либо
/// перерисовкой по событию <see cref="Changed"/>.
/// </summary>
internal static class Theme
{
    public static UiTheme Current { get; private set; } = UiTheme.Dark;

    public static ThemePalette Palette { get; private set; } = ThemePalette.Dark;

    /// <summary>Сработало после смены темы — окна перерисовывают то, что строят кодом.</summary>
    public static event Action? Changed;

    /// <summary>Раздавались ли кисти в ресурсы приложения.</summary>
    private static bool _applied;

    public static void Apply(UiTheme theme)
    {
        // Повторное применение той же темы — пустое дело: ресурсы и так верные,
        // а Changed заставил бы окно перерисовать кодовые элементы впустую.
        // Второй вызов — из SettingsWindow, который мог открыться позже.
        if (_applied && Current == theme)
            return;

        _applied = true;
        Current = theme;
        Palette = theme == UiTheme.Light ? ThemePalette.Light : ThemePalette.Dark;

        if (Application.Current?.Resources is { } resources)
        {
            foreach (var (key, brush) in Palette.Brushes)
                resources[key] = brush;
        }

        Changed?.Invoke();
    }
}
