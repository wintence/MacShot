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

    /// <summary>
    /// Тёмная тема: чёрный с едва заметной серой примесью.
    ///
    /// Палитра монохромная. Единственный «акцент» — белый: он же цвет
    /// текста, поэтому интерфейс различает элементы весом шрифта, воздухом
    /// и глубиной серого, а не цветными пятнами.
    /// </summary>
    public static ThemePalette Dark { get; } = new(new (string, string)[]
    {
        ("Accent", "#F2F2F5"),
        ("AccentDim", "#242428"),
        ("AccentHover", "#FFFFFF"),
        ("AccentPressed", "#D2D2D8"),
        // Полупрозрачная заливка, которая проявляется под курсором.
        // Анимируется прозрачность, а не сам цвет Background: смена цвета
        // перерисовывает элемент процессором, а прозрачность ложится на GPU
        // и остаётся плавной даже при сотне кнопок на экране.
        ("AccentVeil", "#14FFFFFF"),
        // Свечение вокруг активного элемента.
        ("Glow", "#33FFFFFF"),
        ("BadgeText", "#E8E8EC"),
        ("Bg", "#0C0C0E"),
        ("ButtonBg", "#17171A"),
        ("ButtonBgHover", "#1F1F23"),
        ("ButtonBgPressed", "#121215"),
        ("ButtonBorder", "#232327"),
        ("ButtonBorderHover", "#333338"),
        ("Card", "#141417"),
        ("CardLine", "#232327"),
        ("ControlLine", "#2A2A2F"),
        ("DangerBg", "#2A1A19"),
        ("DangerBorder", "#3D2624"),
        ("DangerText", "#E0766E"),
        ("Input", "#19191C"),
        ("MockLine", "#FFFFFF"),
        ("MockTitle", "#4A4A52"),
        ("MockWindow", "#38383F"),
        // Описание настройки намеренно темнее её названия: заголовок
        // держит структуру, пояснение уходит на второй план.
        ("Muted", "#9A9AA3"),
        // Тёмный текст на белом акценте.
        ("OnAccent", "#0C0C0E"),
        ("RowBg", "#161619"),
        ("RowSelected", "#1D1D21"),
        ("ScrollThumbBg", "#3A3A41"),
        // Боковая панель настроек: чуть темнее страницы, чтобы колонка
        // отделялась без рамки.
        ("SidebarBg", "#0A0A0C"),
        ("SidebarLine", "#1C1C20"),
        // Вуаль под курсором в списке разделов. Прозрачность вместо цвета:
        // подсветка ложится на любой фон и не перерисовывает строку.
        ("NavHover", "#12FFFFFF"),
        ("SearchBg", "#19191C"),
        // Дорожка выключенного переключателя и его бегунок.
        ("ToggleOff", "#33333A"),
        ("ToggleKnob", "#F2F2F4"),
        ("SliderKnob", "#E6E6EA"),
        // Квадраты выбора позиции лежат поверх фона кадра, а не поверх темы,
        // поэтому берут не цвета темы, а собственную двойную рамку: тёмную
        // снаружи и светлую внутри. Она одинакова в обеих темах — иначе
        // квадрат был бы виден на одном фоне кадра и не виден на другом
        ("SpotFill", "#B01A1A20"),
        ("SpotLine", "#B3FFFFFF"),
        ("SpotHalo", "#8C000000"),
        ("Subtle", "#6E6E76"),
        ("ToolTipBg", "#1E1E22"),
        ("ToolTipFg", "#F2F2F4"),
        ("Surface", "#141417"),
        ("Text", "#F5F5F7"),
        ("TrackBg", "#26262B"),
    });

    /// <summary>
    /// Светлая тема: белая страница и чёрный текст.
    ///
    /// Акцент здесь тоже чёрный — пара к тёмной теме, где акцент белый.
    /// Так переключение темы меняет только полярность, а не настроение.
    /// </summary>
    public static ThemePalette Light { get; } = new(new (string, string)[]
    {
        ("Accent", "#141416"),
        ("AccentDim", "#EFEFF2"),
        ("AccentHover", "#2A2A2E"),
        ("AccentPressed", "#000000"),
        ("AccentVeil", "#0C000000"),
        ("Glow", "#20000000"),
        ("BadgeText", "#2A2A2E"),
        ("Bg", "#F7F7F8"),
        ("ButtonBg", "#FFFFFF"),
        ("ButtonBgHover", "#F0F0F2"),
        ("ButtonBgPressed", "#E6E6EA"),
        ("ButtonBorder", "#E4E4E8"),
        ("ButtonBorderHover", "#D2D2D8"),
        ("Card", "#FFFFFF"),
        ("CardLine", "#ECECEF"),
        ("ControlLine", "#DDDDE2"),
        ("DangerBg", "#FBEBEA"),
        ("DangerBorder", "#F2D2CF"),
        ("DangerText", "#B23A2C"),
        ("Input", "#FFFFFF"),
        ("MockLine", "#1A1A1D"),
        ("MockTitle", "#E7E7EC"),
        ("MockWindow", "#FFFFFF"),
        ("Muted", "#6E6E76"),
        ("OnAccent", "#FFFFFF"),
        ("RowBg", "#FBFBFC"),
        ("RowSelected", "#F1F1F4"),
        ("ScrollThumbBg", "#CFCFD6"),
        ("SidebarBg", "#F2F2F4"),
        ("SidebarLine", "#E4E4E8"),
        ("NavHover", "#0D000000"),
        ("SearchBg", "#EFEFF2"),
        ("ToggleOff", "#C2C2CA"),
        ("ToggleKnob", "#FFFFFF"),
        ("SliderKnob", "#6E6E76"),
        ("SpotFill", "#991C1F26"),
        ("SpotLine", "#B3FFFFFF"),
        ("SpotHalo", "#8C000000"),
        ("Subtle", "#8A8A92"),
        ("ToolTipBg", "#1C1C1F"),
        ("ToolTipFg", "#F7F7F8"),
        ("Surface", "#FFFFFF"),
        ("Text", "#111114"),
        ("TrackBg", "#E4E4E8"),
    });
}
