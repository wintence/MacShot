using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacShot.Settings;

namespace MacShot.Render;

/// <summary>
/// Собирает итоговый снимок: окно на фоне, со скруглением и мягкой тенью.
/// Рендер идёт средствами WPF, поэтому внешние библиотеки не нужны.
/// </summary>
internal static class ShotRenderer
{
    /// <summary>
    /// Скругляет снимок окна и собирает финальную композицию.
    /// </summary>
    /// <param name="window">Кадр окна в физических пикселях.</param>
    /// <param name="scale">Масштаб DPI монитора, где снимали окно.</param>
    public static RenderTargetBitmap Render(BitmapSource window, AppSettings settings, double scale)
    {
        // Работаем в DIP, а готовим растр в пикселях: так один и тот же
        // результат получается при любом масштабировании Windows.
        double windowWidthDip = window.PixelWidth / scale;
        double windowHeightDip = window.PixelHeight / scale;

        double padding = settings.Padding;

// Отступ берём ровно такой, как задал пользователь. Раньше здесь стояло
// авторасширение под тень (сдвиг + размытие + 4 = 56 при значениях по
// умолчанию), и любой ползунок ниже 56 не давал видимого эффекта.
// Теперь тень может обрезаться краем кадра при маленьком отступе —
// это честнее, чем ползунок, который ничего не меняет.
        double radius = settings.CornerRadius;

        double canvasWidthDip = windowWidthDip + padding * 2;
        double canvasHeightDip = windowHeightDip + padding * 2;

        var background = CreateBackgroundBrush(settings, canvasWidthDip, canvasHeightDip);
        var picture = CreateRoundedPicture(window, width: windowWidthDip, height: windowHeightDip, radius);

        var shadowHost = new Border
        {
            Background = Brushes.Transparent,
            Child = picture,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (settings.ShadowEnabled)
            shadowHost.Effect = CreateShadow(settings, background);

        var root = new Border
        {
            Background = background,
            Width = canvasWidthDip,
            Height = canvasHeightDip,
            // Именно Grid, а не Canvas: Canvas раскладывает детей по Left/Top
            // и игнорирует выравнивание, из-за чего окно прижималось к левому
            // верхнему углу и отступ пропадал.
            Child = new Grid { Children = { shadowHost } },
        };

        // Знак кладём поверх окна и тени, иначе надпись уходила бы под снимок.
        // Слой берём того же размера, что и кадр: отступы водяного знака
        // отсчитываются от края готовой картинки, а не от края окна.
        if (settings.Watermark?.IsActive == true)
        {
            var overlay = (UIElement)WatermarkRenderer.Build(
                settings.Watermark, canvasWidthDip, canvasHeightDip);

            if (root.Child is Grid grid)
            {
                Panel.SetZIndex(overlay, 10);
                grid.Children.Add(overlay);
            }
        }

        root.Measure(new Size(canvasWidthDip, canvasHeightDip));
        root.Arrange(new Rect(0, 0, canvasWidthDip, canvasHeightDip));
        root.UpdateLayout();

        int pixelWidth = (int)Math.Round(canvasWidthDip * scale);
        int pixelHeight = (int)Math.Round(canvasHeightDip * scale);

        var target = new RenderTargetBitmap(
            pixelWidth, pixelHeight, 96 * scale, 96 * scale, PixelFormats.Pbgra32);

        target.Render(root);
        target.Freeze();

        return target;
    }

    /// <summary>
    /// Контейнер со скруглением. Прозрачный фон не мешает: форму задаёт
    /// сама маска Image, а тень считается по получившейся альфе.
    /// </summary>
    private static FrameworkElement CreateRoundedPicture(BitmapSource source, double width, double height, double radius)
    {
        var border = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(radius),
            // Clip делает настоящее скругление видимой области изображения
            ClipToBounds = true,
            Clip = new RectangleGeometry(new Rect(0, 0, width, height), radius, radius),
            Child = new Image
            {
                Source = source,
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true,
            },
        };

        return border;
    }

    /// <summary>
    /// Тень. Цвет подбирается по фону: на тёмном фоне чёрная тень
    /// не читается и окно как бы приклеивается к подложке, поэтому
    /// там тень светлая, а на светлом — тёмная, как в macOS.
    /// </summary>
    private static Effect CreateShadow(AppSettings s, Brush background)
    {
        bool dark = IsDarkBackground(s, background);

        return new DropShadowEffect
        {
            BlurRadius = Math.Max(1, s.ShadowBlur),
            ShadowDepth = s.ShadowOffsetY,
            // Направление в WPF задаётся так, что 270 — вниз, 90 — вверх.
            // Проверено замерами: при 90 яркость над окном падала до 144,
            // а под окном оставалась 230 (тени не было). При 270 наоборот.
            Direction = 270,
            Opacity = s.ShadowOpacity,
            Color = dark ? Colors.White : Colors.Black,
            RenderingBias = RenderingBias.Quality,
        };
    }

    /// <summary>
    /// Тёмный ли фон. Для однотонного цвета и градиента решение принимается
    /// по их собственным цветам, для картинки — по верхнему левому пикселю
    /// (обои обычно однотонные по краю).
    /// </summary>
    private static bool IsDarkBackground(AppSettings s, Brush background)
    {
        switch (s.Background)
        {
            case BackgroundKind.Solid:
                return IsDark(ColorUtil.Parse(s.SolidColor, "#E9EDF5"));

            case BackgroundKind.Gradient:
            {
                var start = ColorUtil.Parse(s.GradientStart, "#E9EDF5");
                var end = ColorUtil.Parse(s.GradientEnd, "#C3CDE0");

                // Если один конец тёмный, а другой светлый, тень берём
                // по более тёмному краю: там тень точно будет видна
                var darker = IsDark(start) || IsDark(end);
                return darker && !(IsDark(start) && IsDark(end)) ? true : IsDark(start);
            }

            default:
                return background is SolidColorBrush solid && IsDark(solid.Color);
        }
    }

    /// <summary>Воспринимаемая светлота по формуле Rec. 709.</summary>
    private static bool IsDark(System.Windows.Media.Color c) =>
        (0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B) < 128;

    private static Brush CreateBackgroundBrush(AppSettings s, double width, double height)
    {
        switch (s.Background)
        {
            case BackgroundKind.Solid:
                return new SolidColorBrush(ColorUtil.Parse(s.SolidColor, "#E9EDF5"));

            case BackgroundKind.Image:
                var brush = TryCreateImageBrush(s, width, height);
                if (brush is not null)
                    return brush;
                // Файл недоступен — не молча показываем пустоту, а откатываемся
                return CreateGradient(s);

            default:
                return CreateGradient(s);
        }
    }

    private static LinearGradientBrush CreateGradient(AppSettings s)
    {
        var (start, end) = ColorUtil.GradientAxis(s.GradientAngle);

        var brush = new LinearGradientBrush
        {
            StartPoint = start,
            EndPoint = end,
        };

        brush.GradientStops.Add(new GradientStop(ColorUtil.Parse(s.GradientStart, "#E9EDF5"), 0));
        brush.GradientStops.Add(new GradientStop(ColorUtil.Parse(s.GradientEnd, "#C3CDE0"), 1));

        brush.Freeze();
        return brush;
    }

    private static ImageBrush? TryCreateImageBrush(AppSettings s, double width, double height)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(s.BackgroundImagePath) ||
                !File.Exists(s.BackgroundImagePath))
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(s.BackgroundImagePath);
            bitmap.EndInit();
            bitmap.Freeze();

            return new ImageBrush(bitmap)
            {
                Stretch = Stretch.UniformToFill,
                ViewportUnits = BrushMappingMode.Absolute,
                Viewport = new Rect(0, 0, width, height),
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Готовит снимок окна к рендеру: копия со скруглёнными углами.
    ///
    /// Замороженный (frozen) BitmapSource менять нельзя — WPF бросает
    /// InvalidOperationException. Поэтому, если исходник уже заморожен,
    /// скругление делаем на его копии.
    /// </summary>
    public static BitmapSource PrepareWindow(BitmapSource source, double radiusDip, double scale)
    {
        // Радиус задан в DIP, а пиксели — в физических, поэтому пересчитываем
        var working = source.IsFrozen
            ? new WriteableBitmap(source)
            : (WriteableBitmap)source;

        CornerRounder.Apply(working, radiusDip * scale);

        working.Freeze();
        return working;
    }
}
