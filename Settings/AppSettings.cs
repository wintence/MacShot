using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacShot.Services;

using MacShot.Lang;

namespace MacShot.Settings;

internal enum BackgroundKind
{
    Solid,
    Gradient,
    Image,
}

internal enum ImageFormat
{
    Png,
    Jpg,
    Webp,
}

/// <summary>
/// Все настройки приложения. Значения по умолчанию подобраны под референс:
/// светлый градиентный фон, скругление ~9 DIP, мягкая тень снизу.
/// </summary>
internal sealed class AppSettings
{
    public string HotKey { get; set; } = "Ctrl+Shift+8";

    /// <summary>
/// Interface language. English by default since the program is meant
/// for a wide audience. The enum order is deliberately Russian-first so that
/// settings.json files written earlier keep meaning Russian.
/// </summary>
    public UiLanguage Language { get; set; } = UiLanguage.English;

    public BackgroundKind Background { get; set; } = BackgroundKind.Gradient;

    public string SolidColor { get; set; } = "#E9EDF5";

    public string GradientStart { get; set; } = "#E9EDF5";
    public string GradientEnd { get; set; } = "#C3CDE0";

    /// <summary>Направление градиента в градусах: 90 — сверху вниз, 0 — слева направо.</summary>
    public double GradientAngle { get; set; } = 90;

    public string BackgroundImagePath { get; set; } = string.Empty;

    /// <summary>Поле вокруг окна, DIP.</summary>
    public double Padding { get; set; } = 40;

    /// <summary>
    /// Радиус скругления углов окна, DIP.
    /// Настройки у пользователя больше нет: значение фиксированное, как в macOS.
    /// </summary>
    public double CornerRadius { get; set; } = 10;

    public bool ShadowEnabled { get; set; } = true;

    /// <summary>
    /// Срезать заголовок снимаемого окна, оставив только содержимое.
    /// </summary>
    public bool HideTitleBar { get; set; }

    /// <summary>Как искать высоту заголовка: по геометрии окна или вручную.</summary>
    public TitleBarMode TitleBarMode { get; set; } = TitleBarMode.Auto;

    /// <summary>
    /// Сколько срезать сверху, DIP. Используется только в ручном режиме.
    /// </summary>
    public double TitleBarHeight { get; set; } = 32;

    /// <summary>Плотность тени, 0..1.</summary>
    public double ShadowOpacity { get; set; } = 0.45;

    /// <summary>Размытие тени, DIP.</summary>
    public double ShadowBlur { get; set; } = 36;

    /// <summary>Смещение тени вниз, DIP.</summary>
    public double ShadowOffsetY { get; set; } = 16;

    public bool SaveToDisk { get; set; }

    public ImageFormat Format { get; set; } = ImageFormat.Png;

    public int JpegQuality { get; set; } = 92;

    public bool RunAtStartup { get; set; }

    /// <summary>Надпись и логотип поверх снимка.</summary>
    public Watermark Watermark { get; set; } = new();

    /// <summary>
    /// Копия настроек для окна настроек: правки идут в копии, и только
    /// по кнопке сохранения попадают в живые настройки.
    ///
    /// Копия глубокая только для водяного знака. Остальные поля — простые
    /// типы, им MemberwiseClone достаточно. Знак пришлось копировать вручную:
    /// это объект, и при поверхностном копировании окно настроек и работающая
    /// программа правили бы одни и те же данные — отмена настроек перестала бы
    /// работать.
    /// </summary>
    public AppSettings Clone() => new()
    {
        HotKey = HotKey,
        Language = Language,
        Background = Background,
        SolidColor = SolidColor,
        GradientStart = GradientStart,
        GradientEnd = GradientEnd,
        GradientAngle = GradientAngle,
        BackgroundImagePath = BackgroundImagePath,
        Padding = Padding,
        CornerRadius = CornerRadius,
        ShadowEnabled = ShadowEnabled,
        HideTitleBar = HideTitleBar,
        TitleBarMode = TitleBarMode,
        TitleBarHeight = TitleBarHeight,
        ShadowOpacity = ShadowOpacity,
        ShadowBlur = ShadowBlur,
        ShadowOffsetY = ShadowOffsetY,
        SaveToDisk = SaveToDisk,
        Format = Format,
        JpegQuality = JpegQuality,
        RunAtStartup = RunAtStartup,
        Watermark = Watermark?.Clone() ?? new Watermark(),
    };
}

/// <summary>
/// Хранение настроек в %AppData%\MacShot\settings.json.
/// </summary>
/// <summary>Как определять высоту срезаемого заголовка.</summary>
internal enum TitleBarMode
{
    /// <summary>По геометрии окна: режется ровно системный заголовок.</summary>
    Auto,

    /// <summary>Заданной вручную величиной — если у окна своя панель сверху.</summary>
    Manual,
}

/// <summary>
/// Хранилище настроек в %AppData%\MacShot\settings.json.
/// </summary>
internal static class SettingsStore
{
    /// <summary>Переопределяется в тестах, чтобы не трогать настройки пользователя.</summary>
    public static string? OverridePath { get; set; }

    private static string Path_ =>
        OverridePath ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MacShot", "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path_))
            {
                var settings =
                    JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path_), Options);
                if (settings is not null)
                    return Sanitize(settings);
            }
        }
        catch
        {
            // Повреждённый файл — начинаем с настроек по умолчанию
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path_)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path_, JsonSerializer.Serialize(settings, Options));
        }
        catch
        {
            // Не смогли сохранить — работаем в памяти
        }
    }

    /// <summary>Чинит значения, которые могли прийти битыми из файла или из правок вручную.</summary>
    private static AppSettings Sanitize(AppSettings s)
    {
        s.Padding = Clamp(s.Padding, 0, 300);
        s.CornerRadius = Clamp(s.CornerRadius, 0, 80);
        s.ShadowOpacity = Clamp(s.ShadowOpacity, 0, 1);
        s.ShadowBlur = Clamp(s.ShadowBlur, 0, 120);
        s.ShadowOffsetY = Clamp(s.ShadowOffsetY, -60, 60);
        s.TitleBarHeight = Clamp(s.TitleBarHeight, 0, 200);
        s.GradientAngle = ((s.GradientAngle % 360) + 360) % 360;
        s.JpegQuality = (int)Clamp(s.JpegQuality, 1, 100);

        s.SolidColor = ColorUtil.Normalize(s.SolidColor, "#E9EDF5");
        s.GradientStart = ColorUtil.Normalize(s.GradientStart, "#E9EDF5");
        s.GradientEnd = ColorUtil.Normalize(s.GradientEnd, "#C3CDE0");

        if (!HotKeyService.IsValid(s.HotKey))
            s.HotKey = "Ctrl+Shift+8";

        // Старые settings.json без раздела со знаком, либо с явным null:
        // в обоих случаях настройки должны быть рабочими, а не пустыми.
        s.Watermark = s.Watermark is null
            ? new Watermark()
            : Watermark.Sanitize(s.Watermark);

        return s;
    }

    private static double Clamp(double value, double min, double max) =>
        double.IsNaN(value) ? min : Math.Clamp(value, min, max);
}
