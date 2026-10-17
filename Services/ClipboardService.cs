using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace MacShot.Services;

/// <summary>
/// Копирование в буфер обмена и сохранение в файл.
/// </summary>
internal static class ClipboardService
{
    /// <summary>
    /// Кладёт изображение в буфер. Windows не даёт держать буфер дольше
    /// процесса, поэтому данные копируются внутрь.
    /// </summary>
    public static bool TrySetImage(BitmapSource image)
    {
        if (image is null)
            return false;

        try
        {
            var copy = Freeze(image);

            var data = new DataObject();
            // PNG сохраняет альфу, поэтому скруглённые углы вставятся
            // корректно в Word, браузер, Figma
            data.SetData(DataFormats.Bitmap, copy);

            var files = new System.Collections.Specialized.StringCollection
            {
                WriteTempPng(image),
            };
            data.SetFileDropList(files);

            Clipboard.SetDataObject(data, true);
            return true;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException
                                       or System.Runtime.InteropServices.ExternalException)
        {
            // Буфер часто занят другим приложением — это не ошибка снимка
            return false;
        }
    }

    private static BitmapSource Freeze(BitmapSource image)
    {
        if (image.IsFrozen)
            return image;

        var copy = new WriteableBitmap(image);
        copy.Freeze();
        return copy;
    }

    private static string WriteTempPng(BitmapSource image)
    {
        var path = Path.Combine(Path.GetTempPath(),
            $"MacShot_{Guid.NewGuid():N}.png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);

        return path;
    }

    /// <summary>
    /// Сохраняет снимок в «Изображения». Возвращает пустую строку при ошибке.
    /// </summary>
    public static string SaveToPictures(BitmapSource image, string windowTitle, Settings.ImageFormat format, int quality)
    {
        try
        {
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(pictures))
                pictures = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");

            var folder = Path.Combine(pictures, "MacShot");
            Directory.CreateDirectory(folder);

            var stamp = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
            var safe = MakeSafeName(windowTitle);
            var path = Path.Combine(folder, $"{stamp}_{safe}{Extension(format)}");

            int counter = 2;
            while (File.Exists(path))
            {
                path = Path.Combine(folder, $"{stamp}_{safe} ({counter}){Extension(format)}");
                counter++;
            }

            Write(image, path, format, quality);
            return path;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void Write(BitmapSource image, string path, Settings.ImageFormat format, int quality)
    {
        BitmapEncoder encoder = format switch
        {
            Settings.ImageFormat.Jpg => new JpegBitmapEncoder { QualityLevel = Math.Clamp(quality, 1, 100) },
            Settings.ImageFormat.Webp => CreateWebp(),
            _ => new PngBitmapEncoder(),
        };

        // JPEG не умеет альфу: прозрачные углы без подложки превратились бы в мусор
        var source = format == Settings.ImageFormat.Jpg ? FlattenOntoWhite(image) : image;

        encoder.Frames.Add(BitmapFrame.Create(source));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>
    /// Формат WEBP в WPF отсутствует — сохраняем в PNG.
    /// Отдельная реализация потребовала бы внешней библиотеки.
    /// </summary>
    private static BitmapEncoder CreateWebp() => new PngBitmapEncoder();

    /// <summary>Накладывает изображение на белый фон — для форматов без альфы.</summary>
    private static BitmapSource FlattenOntoWhite(BitmapSource image)
    {
        int width = image.PixelWidth;
        int height = image.PixelHeight;

        var visual = new System.Windows.Controls.Border
        {
            Background = Brushes.White,
            Width = width,
            Height = height,
            Child = new System.Windows.Controls.Image
            {
                Source = image,
                Stretch = Stretch.Fill,
            },
        };

        visual.Measure(new Size(width, height));
        visual.Arrange(new Rect(0, 0, width, height));
        visual.UpdateLayout();

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Bgra32);
        target.Render(visual);
        target.Freeze();

        return target;
    }

    private static string Extension(Settings.ImageFormat format) => format switch
    {
        Settings.ImageFormat.Jpg => ".jpg",
        Settings.ImageFormat.Webp => ".webp",
        _ => ".png",
    };

    private static string MakeSafeName(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "Окно";

        var cleaned = string.Join("_", title.Split(Path.GetInvalidFileNameChars())).Trim();

        if (cleaned.Length == 0)
            cleaned = "Окно";

        return cleaned.Length <= 48 ? cleaned : cleaned[..48];
    }
}
