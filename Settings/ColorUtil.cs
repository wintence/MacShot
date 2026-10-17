using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace MacShot.Settings;

/// <summary>
/// Разбор и нормализация цветов в формате #RRGGBB.
/// </summary>
internal static class ColorUtil
{
    /// <summary>Приводит строку к каноническому виду, подставляя запасной цвет при ошибке.</summary>
    public static string Normalize(string? hex, string fallback) =>
        TryParse(hex, out var color)
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : fallback;

    /// <summary>Разбирает #RGB, #RRGGBB и то же самое без решётки.</summary>
    public static bool TryParse(string? hex, out Color color)
    {
        color = Colors.Black;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        var text = hex.Trim().TrimStart('#');

        if (text.Length == 3)
            text = string.Concat(text[0], text[0], text[1], text[1], text[2], text[2]);

        if (text.Length != 6)
            return false;

        if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return false;

        color = Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF));

        return true;
    }

    public static Color Parse(string hex, string fallback) =>
        TryParse(hex, out var color) ? color : Parse(fallback, "#000000");

    public static string ToHex(Color color) =>
        $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    /// <summary>
    /// Ось градиента по углу: 0° — сверху вниз, 90° — слева направо,
    /// 180° — снизу вверх, 270° — справа налево.
    ///
    /// Раньше направление задавалось RotateTransform на кисти. Так делать
    /// нельзя: при повороте ось уходит за пределы фигуры, а у кисти стоит
    /// WrapMode.Tile, и заливка начинает повторяться плиткой. На замерах
    /// угол 90° давал сплошной начальный цвет вместо перехода.
    /// Теперь ось считается напрямую и всегда проходит через фигуру.
    /// </summary>
    public static (Point Start, Point End) GradientAxis(double angle)
    {
        double radians = angle * Math.PI / 180.0;
        double dx = Math.Sin(radians);
        // Именно cos, а не -cos: при 0° начало должно быть сверху.
        // Со знаком минус 0° и 180° менялись местами — замеры это показали.
        double dy = Math.Cos(radians);

        return (new Point(0.5 - dx * 0.5, 0.5 - dy * 0.5),
                new Point(0.5 + dx * 0.5, 0.5 + dy * 0.5));
    }
}
