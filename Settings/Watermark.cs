using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MacShot.Settings;

/// <summary>
/// Где стоит водяной знак относительно кадра.
/// Девять позиций сетки — того хватает для подписи и картинки.
/// </summary>
internal enum Placement
{
    TopLeft,
    TopCenter,
    TopRight,
    MiddleLeft,
    Center,
    MiddleRight,
    BottomLeft,
    BottomCenter,
    BottomRight,
}

/// <summary>Что за элемент: текст или картинка.</summary>
internal enum MarkKind
{
    Text,
    Picture,
}

/// <summary>
/// Один элемент водяного знака.
///
/// Надпись и картинка живут в одном списке, а не в двух полях: иначе получить
/// можно было бы ровно одну надпись и одну картинку, а несколько — уже никак.
/// </summary>
internal sealed class Mark
{
    public MarkKind Kind { get; set; } = MarkKind.Text;

    /// <summary>Текст надписи. Для картинки не используется.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Путь к файлу картинки. Для надписи не используется.</summary>
    public string Path { get; set; } = string.Empty;

    public Placement Placement { get; set; } = Placement.BottomRight;

    /// <summary>
    /// Размер: кегль для надписи, высота для картинки.
    /// Одно поле вместо двух — в настройках это всё равно один ползунок,
    /// а лишнее свойство только путало бы при разборе настроек.
    ///
    /// По умолчанию кегль. Для картинки при создании элемента ставится своё:
    /// инициализатор свойства не имеет права читать другие свойства объекта,
    /// он выполняется раньше их присваивания.
    /// </summary>
    public double Size { get; set; } = 18;

    /// <summary>Непрозрачность 0..1.</summary>
    public double Opacity { get; set; } = 0.9;

    /// <summary>Отступ от края кадра, DIP.</summary>
    public double Margin { get; set; } = 24;

    /// <summary>Цвет надписи. Для картинки не используется.</summary>
    public string Color { get; set; } = "#FFFFFF";

    public string FontFamily { get; set; } = "Segoe UI";

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    /// <summary>Тень под текстом — иначе надпись не читается на светлом.</summary>
    public bool Shadow { get; set; } = true;

    public string ShadowColor { get; set; } = "#000000";

    public double ShadowOpacity { get; set; } = 0.5;

    public double ShadowBlur { get; set; } = 6;

    public double ShadowOffsetY { get; set; } = 2;

    /// <summary>
    /// Есть ли что рисовать. У картинки отдельно проверяем файл: путь
    /// остаётся в настройках после удаления, и тогда на кадре было бы
    /// пустое место там, где должна быть картинка.
    /// </summary>
    public bool IsRenderable => Kind switch
    {
        MarkKind.Text => !string.IsNullOrWhiteSpace(Text),
        _ => !string.IsNullOrWhiteSpace(Path) && File.Exists(Path),
    };

    /// <summary>Подпись для списка в настройках.</summary>
    public string Summary => Kind == MarkKind.Text
        ? (string.IsNullOrWhiteSpace(Text) ? "—" : Text)
        : (string.IsNullOrWhiteSpace(Path) ? "—" : System.IO.Path.GetFileName(Path));

    public Mark Clone() => (Mark)MemberwiseClone();
}

/// <summary>
/// Водяной знак целиком: список надписей и картинок.
///
/// Элементы в одном поле умеют разойтись, если им задали одну позицию:
/// см. WatermarkRenderer — там они становятся в ряд.
/// </summary>
internal sealed class Watermark
{
    public List<Mark> Marks { get; set; } = new();

    public bool IsActive => Marks.Any(m => m.IsRenderable);

    public Watermark Clone() => new() { Marks = Marks.Select(m => m.Clone()).ToList() };

    /// <summary>Шрифты, которые гарантированно есть в Windows.</summary>
    public static IReadOnlyList<string> Fonts { get; } = new[]
    {
        "Segoe UI",
        "Segoe UI Semibold",
        "Arial",
        "Calibri",
        "Verdana",
        "Tahoma",
        "Georgia",
        "Times New Roman",
        "Consolas",
        "Courier New",
        "Impact",
        "Trebuchet MS",
    };

    /// <summary>Приводит значения к допустимым — на случай битых настроек.</summary>
    public static Watermark Sanitize(Watermark w)
    {
        // Старые settings.json приходят без списка: поля Text и Image лежали
        // рядом, и в новый список сами не переезжают. Без этой проверки
        // разбор такого файла заканчивался исключением.
        w.Marks ??= new List<Mark>();

        foreach (var mark in w.Marks)
        {
            if (mark.Kind == MarkKind.Text)
            {
                mark.Size = Clamp(mark.Size, 6, 200);
                mark.Color = ColorUtil.Normalize(mark.Color, "#FFFFFF");
                mark.FontFamily = string.IsNullOrWhiteSpace(mark.FontFamily)
                    ? "Segoe UI"
                    : mark.FontFamily;
                mark.ShadowColor = ColorUtil.Normalize(mark.ShadowColor, "#000000");
                mark.ShadowBlur = Clamp(mark.ShadowBlur, 0, 40);
                mark.ShadowOffsetY = Clamp(mark.ShadowOffsetY, -20, 20);
                mark.ShadowOpacity = Clamp(mark.ShadowOpacity, 0, 1);
            }
            else
            {
                mark.Size = Clamp(mark.Size, 8, 2000);
            }

            mark.Opacity = Clamp(mark.Opacity, 0, 1);
            mark.Margin = Clamp(mark.Margin, 0, 300);
        }

        return w;
    }

    private static double Clamp(double value, double min, double max) =>
        double.IsNaN(value) ? min : System.Math.Clamp(value, min, max);
}
