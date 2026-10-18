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
        ("Accent", "#4C8DFF"),
        ("AccentDim", "#2F4E86"),
        ("AccentGloss", "#33FFFFFF"),
        ("AccentHover", "#6BA1FF"),
        ("AccentPressed", "#3D7BEA"),
        ("BadgeText", "#A8C0E8"),
        ("Bg", "#17171B"),
        ("ButtonBg", "#2E2E3A"),
        ("ButtonBgHover", "#3B3B4A"),
        ("ButtonBgPressed", "#24242E"),
        ("ButtonBorder", "#3A3A48"),
        ("ButtonBorderHover", "#4A4A5C"),
        ("Card", "#212128"),
        ("CardLine", "#2E2E38"),
        ("ControlLine", "#3C3C4A"),
        ("DangerBg", "#33242B"),
        ("DangerBorder", "#4A2E36"),
        ("DangerText", "#FF8A8A"),
        ("Gloss", "#14FFFFFF"),
        ("Input", "#1A1A20"),
        ("MockLine", "#FFFFFF"),
        ("MockTitle", "#4A4A59"),
        ("MockWindow", "#3A3A46"),
        ("Muted", "#8E8E99"),
        ("OnAccent", "#08111F"),
        ("RowBg", "#1A1A20"),
        ("RowSelected", "#2A3B57"),
        ("ScrollThumbBg", "#4A4A58"),
        // Квадраты выбора позиции лежат поверх фона кадра, а не поверх темы,
        // поэтому берут не цвета темы, а собственную двойную рамку: тёмную
        // снаружи и светлую внутри. Она одинакова в обеих темах — иначе
        // квадрат был бы виден на одном фоне кадра и не виден на другом
        ("SpotFill", "#B01A1A20"),
        ("SpotLine", "#B3FFFFFF"),
        ("SpotHalo", "#8C000000"),
        ("Subtle", "#C8C8D4"),
        ("Surface", "#26262F"),
        ("Text", "#E9E9EF"),
        ("TrackBg", "#2C2C36"),
    });

    /// <summary>
    /// Светлая тема.
    ///
    /// Акцент здесь темнее, чем в тёмной: на белом фоне тот же #4C8DFF
    /// выглядит бледнее, а мелкий текст поверх него читается плохо.
    /// </summary>
    public static ThemePalette Light { get; } = new(new (string, string)[]
    {
        ("Accent", "#2F6FE4"),
        ("AccentDim", "#E3ECFD"),
        ("AccentGloss", "#4DFFFFFF"),
        ("AccentHover", "#2560CE"),
        ("AccentPressed", "#1E4FAD"),
        ("BadgeText", "#2F6FE4"),
        ("Bg", "#F3F4F7"),
        ("ButtonBg", "#EDF0F4"),
        ("ButtonBgHover", "#E2E7EE"),
        ("ButtonBgPressed", "#D6DCE5"),
        ("ButtonBorder", "#D2D8E1"),
        ("ButtonBorderHover", "#BFC7D4"),
        ("Card", "#FFFFFF"),
        ("CardLine", "#DDE1E8"),
        ("ControlLine", "#C3CAD6"),
        ("DangerBg", "#FBEDEC"),
        ("DangerBorder", "#F0CBC7"),
        ("DangerText", "#C0392B"),
        ("Gloss", "#B3FFFFFF"),
        ("Input", "#F7F8FA"),
        ("MockLine", "#39404D"),
        ("MockTitle", "#E7EAF0"),
        ("MockWindow", "#FFFFFF"),
        ("Muted", "#6E7681"),
        ("OnAccent", "#FFFFFF"),
        ("RowBg", "#F5F6F9"),
        ("RowSelected", "#E4EDFD"),
        ("ScrollThumbBg", "#B3BAC6"),
        ("SpotFill", "#991C1F26"),
        ("SpotLine", "#B3FFFFFF"),
        ("SpotHalo", "#8C000000"),
        ("Subtle", "#4E5661"),
        ("Surface", "#FFFFFF"),
        ("Text", "#1C1F26"),
        ("TrackBg", "#DCE0E7"),
    });
}
