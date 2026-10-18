using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MacShot.Render;

/// <summary>
/// Скругляет углы изображения, обрезая пиксели по форме скруглённого
/// прямоугольника с антиалиасингом.
/// </summary>
internal static class CornerRounder
{
    /// <summary>
    /// Прозрачными делаются только углы — остальные пиксели не трогаем,
    /// иначе кадр большого окна пришлось бы переписывать целиком.
    /// </summary>
    /// <param name="topOnly">
    /// Скруглять только верхнюю пару углов. Нужно, когда заголовок окна
    /// срезан: снизу рама и так уходит в тень, и скруглять её незачем.
    /// </param>
    public static void Apply(WriteableBitmap bitmap, double radius, bool topOnly = false)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;

        if (radius <= 0.5)
            return;

        double r = Math.Min(radius, Math.Min(width, height) / 2.0);
        if (r < 0.5)
            return;

        int stride = width * 4;
        var buffer = new byte[stride * height];

        Capture.PixelTransfer.Read(bitmap, width, height, buffer);

        int margin = (int)Math.Ceiling(r);

        for (int y = 0; y < margin; y++)
        {
            int topRow = y * stride;

            for (int x = 0; x < margin; x++)
            {
                int rightX = width - 1 - x;

                buffer[topRow + x * 4 + 3] =
                    (byte)Coverage(x, y, width, height, r);

                buffer[topRow + rightX * 4 + 3] =
                    (byte)Coverage(rightX, y, width, height, r);
            }

            if (topOnly)
                continue;

            int bottomRow = (height - 1 - y) * stride;

            for (int x = 0; x < margin; x++)
            {
                int rightX = width - 1 - x;

                buffer[bottomRow + x * 4 + 3] =
                    (byte)Coverage(x, height - 1 - y, width, height, r);

                buffer[bottomRow + rightX * 4 + 3] =
                    (byte)Coverage(rightX, height - 1 - y, width, height, r);
            }
        }

        Capture.PixelTransfer.Write(bitmap, width, height, buffer);
    }

    /// <summary>
    /// Срезает сверху <paramref name="pixels"/> строк — заголовок окна.
    ///
    /// Через CroppedBitmap, а не построчно: WPF справится сам, и главное —
    /// не придётся вручную двигать буфер, где легко оставить мусор в последней
    /// строке кадра.
    /// </summary>
    public static WriteableBitmap CropTop(WriteableBitmap source, int pixels)
    {
        var height = source.PixelHeight;
        var width = source.PixelWidth;

        if (pixels <= 0 || pixels >= height - 1)
            return source;

        var cropped = new CroppedBitmap(
            source, new Int32Rect(0, pixels, width, height - pixels));

        return new WriteableBitmap(cropped);
    }

    /// <summary>
    /// Покрытие пикселя формой скруглённого угла, 0..255.
    /// Центр дуги лежит на расстоянии r от двух ближайших краёв.
    /// </summary>
    private static double Coverage(int x, int y, int width, int height, double r)
    {
        double dx = Math.Min(x + 0.5, width - x - 0.5);
        double dy = Math.Min(y + 0.5, height - y - 0.5);

        // За пределами радиуса идёт прямая часть рамки — угол ничего не срезает
        if (dx > r || dy > r)
            return 255;

        double dist = Math.Sqrt((r - dx) * (r - dx) + (r - dy) * (r - dy));

        // На диагонали дуги граница идёт под 45°, и покрытие меняется примерно
        // на 1.4 единицы за пиксель. При полосе сглаживания в 1px переход
        // 0 -> 255 перескакивал бы, и угол вышел бы «лесенкой» — поэтому
        // ширину фильтра берём √2.
        const double filterWidth = 1.41421356;

        return Math.Clamp(0.5 + (r - dist) / filterWidth, 0, 1) * 255;
    }
}
