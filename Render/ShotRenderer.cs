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
    /// <summary>Высота места под подпись: воздух сверху, строка и зазор до окна.</summary>
    internal const double CaptionSpace = 40;

    /// <summary>Зазор между подписью и окном, DIP.</summary>
    internal const double CaptionGap = 10;

    /// <summary>
    /// Скругляет снимок окна и собирает финальную композицию.
    /// </summary>
    /// <param name="window">Кадр окна в физических пикселях.</param>
    /// <param name="scale">Масштаб DPI монитора, где снимали окно.</param>
    /// <param name="appName">
    /// Название приложения для подписи над окном. Пусто — подписи нет,
    /// даже если настройка включена: показывать «неизвестно» незачем.
    /// </param>
    public static RenderTargetBitmap Render(
        BitmapSource window, AppSettings settings, double scale, string? appName = null)
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

        bool caption = settings.AppNameCaption && !string.IsNullOrWhiteSpace(appName);

        // Место под подпись отступа сверху не отнимает, а добавляет: при
        // маленьком отступе подпись легла бы на окно, поэтому кадр подрастает
        // ровно на недостающую разницу.
        double top = caption ? Math.Max(padding, CaptionSpace) : padding;

        double canvasWidthDip = windowWidthDip + padding * 2;
        double canvasHeightDip = windowHeightDip + top + padding;

        var background = CreateBackgroundBrush(settings, canvasWidthDip, canvasHeightDip);
        var picture = CreateRoundedPicture(window, width: windowWidthDip, height: windowHeightDip, radius);

        // Окно и свечение лежат в общем слое: свечение — та же картинка,
        // размытая и полупрозрачная, поэтому ореол повторяет её цвета,
        // а не изображает тень одного цвета.
        var windowLayer = new Grid
        {
            Width = windowWidthDip,
            Height = windowHeightDip,
        };

        if (settings.GlowEnabled)
            windowLayer.Children.Add(CreateGlow(window, settings, windowWidthDip, windowHeightDip));

        windowLayer.Children.Add(WrapInShadow(picture, settings, background));

        var root = new Grid
        {
            Background = background,
            Width = canvasWidthDip,
            Height = canvasHeightDip,
        };

        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(top) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(windowHeightDip) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(padding) });

        // Именно Grid, а не Canvas: Canvas раскладывает детей по Left/Top
        // и игнорирует выравнивание, из-за чего окно прижималось к левому
        // верхнему углу и отступ пропадал.
        Grid.SetRow(windowLayer, 1);
        root.Children.Add(windowLayer);

        if (caption)
            root.Children.Add(BuildCaption(appName!, settings, background));

        // Знак кладём поверх окна, тени и подписи, иначе надпись уходила бы
        // под снимок. Слой того же размера, что и кадр: отступы водяного знака
        // отсчитываются от края готовой картинки, а не от края окна.
        if (settings.Watermark?.IsActive == true)
        {
            var overlay = (UIElement)WatermarkRenderer.Build(
                settings.Watermark, canvasWidthDip, canvasHeightDip);

            Grid.SetRowSpan(overlay, 3);
            Panel.SetZIndex(overlay, 10);
            root.Children.Add(overlay);
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
    /// Подпись над окном: название приложения мелким шрифтом по центру.
    /// Цвет — светлый на тёмном фоне и тёмный на светлом, с прозрачностью:
    /// подпись должна читаться, но не спорить с окном.
    /// </summary>
    private static TextBlock BuildCaption(string text, AppSettings s, Brush background)
    {
        var color = IsDarkBackground(s, background) ? Colors.White : Colors.Black;

        var brush = new SolidColorBrush(Color.FromArgb(158, color.R, color.G, color.B));
        brush.Freeze();

        return new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
            Foreground = brush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, CaptionGap),
        };
    }

    /// <summary>Размытая копия окна: из неё и получается цветной ореол.</summary>
    private static Image CreateGlow(
        BitmapSource window, AppSettings s, double width, double height) =>
        new()
        {
            Source = window,
            Width = width,
            Height = height,
            Stretch = Stretch.Fill,
            Opacity = s.GlowOpacity,
            Effect = new BlurEffect
            {
                Radius = Math.Max(1, s.GlowBlur),
                RenderingBias = RenderingBias.Quality,
            },
        };

    /// <summary>
    /// Тень живёт на отдельной обёртке, а не на самом окне: эффект и клип на
    /// одном элементе WPF считает через промежуточный растр, и края снимка
    /// теряли по пикселю.
    /// </summary>
    private static FrameworkElement WrapInShadow(
        FrameworkElement picture, AppSettings s, Brush background)
    {
        var host = new Border { Child = picture };

        if (s.ShadowEnabled)
            host.Effect = CreateShadow(s, background);

        return host;
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
    /// Тень. Цвет либо задан пользователем, либо подбирается по фону: на
    /// тёмном фоне чёрная тень не читается и окно как бы приклеивается
    /// к подложке, поэтому там тень светлая, а на светлом — тёмная,
    /// как в macOS.
    /// </summary>
    private static Effect CreateShadow(AppSettings s, Brush background)
    {
        Color color = string.IsNullOrWhiteSpace(s.ShadowColor)
            ? (IsDarkBackground(s, background) ? Colors.White : Colors.Black)
            : ColorUtil.Parse(s.ShadowColor, "#000000");

        return new DropShadowEffect
        {
            BlurRadius = Math.Max(1, s.ShadowBlur),
            ShadowDepth = s.ShadowOffsetY,
            // Направление в WPF задаётся так, что 270 — вниз, 90 — вверх.
            // Проверено замерами: при 90 яркость над окном падала до 144,
            // а под окном оставалась 230 (тени не было). При 270 наоборот.
            Direction = 270,
            Opacity = s.ShadowOpacity,
            Color = color,
            RenderingBias = RenderingBias.Quality,
        };
    }

    /// <summary>
    /// Тёмный ли фон. Для однотонного цвета и градиента решение принимается
    /// по их собственным цветам, для картинки — по верхнему левому пикселю
    /// (обои обычно однотонные по краю).
    /// </summary>
    internal static bool IsDarkBackground(AppSettings s, Brush background)
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
    /// Готовит снимок окна к рендеру: срезает заголовок и скругляет углы.
    ///
    /// Замороженный (frozen) BitmapSource менять нельзя — WPF бросает
    /// InvalidOperationException. Поэтому, если исходник уже заморожен,
    /// скругление делаем на его копии.
    /// </summary>
    /// <param name="source">Кадр окна в физических пикселях.</param>
    /// <param name="settings">Настройки: радиус и, если включено, срез заголовка.</param>
    /// <param name="scale">Масштаб DPI монитора, где снимали окно.</param>
    /// <param name="hwnd">
    /// Окно, снятое в кадр. Нужно в автоматическом режиме: высоту заголовка
    /// мы берём из геометрии именно этого окна.
    /// </param>
    public static BitmapSource PrepareWindow(
        BitmapSource source, AppSettings settings, double scale, IntPtr hwnd = default)
    {
        // Радиус задан в DIP, а пиксели — в физических, поэтому пересчитываем
        var working = source.IsFrozen
            ? new WriteableBitmap(source)
            : (WriteableBitmap)source;

        int crop = settings.HideTitleBar
            ? (int)Math.Round(TitleBarDip(settings, scale, hwnd) * scale)
            : 0;

        // Сначала срез, потом скругление. Наоборот нельзя: верхние углы,
        // которые мы скруглили бы первыми, всё равно уходят в обрез.
        if (crop > 0)
        {
            working = CornerRounder.CropTop(working, crop);
            CornerRounder.Apply(working, settings.CornerRadius * scale, topOnly: true);
        }
        else
        {
            CornerRounder.Apply(working, settings.CornerRadius * scale);
        }

        working.Freeze();
        return working;
    }

    /// <summary>
    /// Высота среза, DIP.
    ///
    /// Порядок такой: своя настройка приложения, потом общий режим. Своя
    /// запись важнее всего — иначе приложение с собственной панелью
    /// пришлось бы каждый раз перенастраивать глобально, задевая остальные.
    /// </summary>
    private static double TitleBarDip(AppSettings settings, double scale, IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero &&
            settings.TitleBarOverrides.TryGetValue(
                Interop.Native.GetAppPath(hwnd), out double custom))
        {
            return custom;
        }

        if (settings.TitleBarMode == TitleBarMode.Manual)
            return settings.TitleBarHeight;

        if (hwnd == IntPtr.Zero)
            return 0;

        return Interop.Native.GetTitleBarHeight(hwnd, scale);
    }

    /// <summary>
    /// Высота среза для окна, DIP — то же, что применит рендер.
    ///
    /// Нужна, чтобы настройка, увиденная после снимка, совпадала с той,
    /// которая на самом деле использовалась.
    /// </summary>
    public static double MeasureTitleBar(AppSettings settings, double scale, IntPtr hwnd) =>
        settings.HideTitleBar ? TitleBarDip(settings, scale, hwnd) : 0;
}
