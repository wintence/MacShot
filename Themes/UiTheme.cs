using System.Globalization;
using MacShot.Settings;

namespace MacShot.Themes;

/// <summary>
/// Тема оформления интерфейса.
///
/// Тёмная идёт первой: у того, кто запустит программу впервые, тема ещё не
/// выбрана, и интерфейс должен остаться таким, каким был до появления тем.
/// </summary>
internal enum UiTheme
{
    Dark = 0,
    Light = 1,
}

/// <summary>
/// Цвета одной темы.
///
/// Палитра лежит в словаре, а не в свойствах класса: одни и те же ключи нужны
/// и разметке (через ресурсы приложения), и коду, который красит строки списков
/// и макет окна в предпросмотре. Свойства означали бы два перечня одного и
/// того же, и они разошлись бы при первой же правке темы.
///
/// Ключи в обеих темах обязаны совпадать: тему переключают подменой содержимого
/// словаря, и отсутствующий ключ тихо останется прежним цветом.
/// </summary>
internal sealed class ThemePalette
{
    private readonly Dictionary<string, SolidColorBrush> _brushes;

    private ThemePalette((string Key, string Hex)[] entries)
    {
        _brushes = new Dictionary<string, SolidColorBrush>(entries.Length);

        foreach (var (key, hex) in entries)
        {
            // Замороженная кисть не следит за изменениями и годится для общей
            // раздачи: одна на все окна вместо кисти на каждое свойство
            var brush = new SolidColorBrush(Parse(hex));
            brush.Freeze();
            _brushes[key] = brush;
        }
    }

    /// <summary>
    /// Цвет палитры. Понимает и восемь знаков #AARRGGBB — прозрачность нужна
    /// квадратам выбора позиции: они лежат поверх фона кадра, который
    /// выбирает пользователь, и заранее известного цвета под ними нет.
    /// </summary>
    private static Color Parse(string hex)
    {
        if (hex.Length == 9
            && hex[0] == '#'
            && uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out uint argb))
        {
            return Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF));
        }

        return ColorUtil.Parse(hex, "#000000");
    }

    /// <summary>Кисти по ключам — теми же именами, что и в разметке.</summary>
    public IReadOnlyDictionary<string, SolidColorBrush> Brushes => _brushes;

    /// <summary>
    /// Кисть по ключу — для кода. В разметке те же ключи берутся через
    /// {DynamicResource}, так что переключение темы догоняет и их.
    /// </summary>
    public Brush Of(string key) =>
        _brushes.TryGetValue(key, out var brush) ? brush : System.Windows.Media.Brushes.Black;

    /// <summary>Исходная тёмная тема: та, что была в программе до тем.</summary>
    public static ThemePalette Dark { get; } = new(new (string, string)[]
    {
        // Единственный цветной акцент — тёплый янтарь. Всё прочее —
        // графит: страница различается весом шрифта и воздухом, а не
        // рамками и подкрасками.
        ("Accent", "#FFB454"),
        ("AccentDim", "#3A2A14"),
        ("AccentHover", "#FFC574"),
        ("AccentPressed", "#E89C38"),
        // Полупрозрачная заливка, которая проявляется под курсором.
        // Анимируется прозрачность, а не сам цвет Background: смена цвета
        // перерисовывает элемент процессором, а прозрачность ложится на GPU
        // и остаётся плавной даже при сотне кнопок на экране.
        ("AccentVeil", "#12FFFFFF"),
        // Свечение вокруг активного элемента.
        ("Glow", "#40FFB454"),
        ("BadgeText", "#FFB454"),
        ("Bg", "#0F0F13"),
        ("ButtonBg", "#1B1B21"),
        ("ButtonBgHover", "#23232A"),
        ("ButtonBgPressed", "#141419"),
        ("ButtonBorder", "#24242B"),
        ("ButtonBorderHover", "#33333C"),
        ("Card", "#16161A"),
        ("CardLine", "#24242B"),
        ("ControlLine", "#2A2A31"),
        ("DangerBg", "#2E1D1B"),
        ("DangerBorder", "#3E2623"),
        ("DangerText", "#E4685C"),
        ("Input", "#1A1A20"),
        ("MockLine", "#FFFFFF"),
        ("MockTitle", "#4A4A59"),
        ("MockWindow", "#3A3A46"),
        ("Muted", "#96938C"),
        // Тёмный текст на янтаре: белый на этом фоне выцветает.
        ("OnAccent", "#201708"),
        ("RowBg", "#17171C"),
        ("RowSelected", "#1D1D24"),
        ("ScrollThumbBg", "#3A3A41"),
        // Боковая панель настроек: чуть светлее фона окна, чтобы колонка
        // отделялась без рамки, и темнее карточек, чтобы карточки читались
        // на ней, а не наоборот.
        ("SidebarBg", "#0F0F13"),
        ("SidebarLine", "#1C1C22"),
        // Вуаль под курсором в списке разделов. Прозрачность вместо цвета:
        // подсветка ложится на любой фон и не перерисовывает строку.
        ("NavHover", "#12FFFFFF"),
        ("SearchBg", "#1A1A20"),
        // Дорожка выключенного переключателя и его бегунок.
        ("ToggleOff", "#33333A"),
        ("ToggleKnob", "#EDEBE5"),
        ("SliderKnob", "#D9D6CE"),
        // Квадраты выбора позиции лежат поверх фона кадра, а не поверх темы,
        // поэтому берут не цвета темы, а собственную двойную рамку: тёмную
        // снаружи и светлую внутри. Она одинакова в обеих темах — иначе
        // квадрат был бы виден на одном фоне кадра и не виден на другом
        ("SpotFill", "#B01A1A20"),
        ("SpotLine", "#B3FFFFFF"),
        ("SpotHalo", "#8C000000"),
        ("Subtle", "#6E6D69"),
        ("ToolTipBg", "#1F1F24"),
        ("ToolTipFg", "#ECEAE4"),
        ("Surface", "#16161A"),
        ("Text", "#ECEAE4"),
        ("TrackBg", "#26262C"),
    });

    /// <summary>
    /// Светлая тема.
    ///
    /// Акцент здесь темнее, чем в тёмной: на белом фоне тот же янтарь
    /// выглядит бледнее, а мелкий текст поверх него читается плохо.
    /// </summary>
    public static ThemePalette Light { get; } = new(new (string, string)[]
    {
        ("Accent", "#C07A1E"),
        ("AccentDim", "#F6E9D2"),
        ("AccentHover", "#A8680F"),
        ("AccentPressed", "#8F5711"),
        ("AccentVeil", "#0C000000"),
        ("Glow", "#40C07A1E"),
        ("BadgeText", "#B26A1B"),
        ("Bg", "#F3F1EB"),
        ("ButtonBg", "#FFFFFF"),
        ("ButtonBgHover", "#F1EDE4"),
        ("ButtonBgPressed", "#E7E2D6"),
        ("ButtonBorder", "#E4E0D6"),
        ("ButtonBorderHover", "#D6D1C4"),
        ("Card", "#FBFAF6"),
        ("CardLine", "#E6E2D8"),
        ("ControlLine", "#DDD8CC"),
        ("DangerBg", "#FBEBE7"),
        ("DangerBorder", "#F0CFC6"),
        ("DangerText", "#C0402F"),
        ("Input", "#FFFFFF"),
        ("MockLine", "#39404D"),
        ("MockTitle", "#E7EAF0"),
        ("MockWindow", "#FFFFFF"),
        ("Muted", "#77746B"),
        ("OnAccent", "#2A1602"),
        ("RowBg", "#F7F5EF"),
        ("RowSelected", "#EFECE3"),
        ("ScrollThumbBg", "#D0C9BA"),
        ("SidebarBg", "#F3F1EB"),
        ("SidebarLine", "#E4E0D6"),
        ("NavHover", "#0D000000"),
        ("SearchBg", "#E9E6DE"),
        ("ToggleOff", "#D5CFC2"),
        ("ToggleKnob", "#FFFFFF"),
        ("SliderKnob", "#A9A393"),
        ("SpotFill", "#991C1F26"),
        ("SpotLine", "#B3FFFFFF"),
        ("SpotHalo", "#8C000000"),
        ("Subtle", "#9A968C"),
        ("ToolTipBg", "#2A2823"),
        ("ToolTipFg", "#F3F1EB"),
        ("Surface", "#FBFAF6"),
        ("Text", "#23211D"),
        ("TrackBg", "#E4E0D6"),
    });
}
