using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using MacShot.Render;
using MacShot.Services;
using MacShot.Settings;
using MacShot.Themes;

using MacShot.Lang;

namespace MacShot.UI;

/// <summary>
/// Окно настроек. Работает поверх трея, закрывается крестиком.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly TrayIcon _tray;

    /// <summary>Заполнено ли уже поле — защита от обработчиков, летящих при создании.</summary>
    private bool _ready;

    internal SettingsWindow(AppSettings settings, TrayIcon tray)
    {
        InitializeComponent();

        _settings = settings;
        _tray = tray;

        Title = I18n.Current["set.title"];

        // Заголовок окна свой, поэтому и надпись, и иконку рисуем сами.
        TitleText.Text = Title;
        TitleIcon.Source = TitleBar.AppIcon();

        // Метка сборки в подвале: по ней видно, какой файл запущен.
        // Версия одна и та же, а сборка пересобирается часто, и без метки
        // невозможно понять, обновилась программа или нет.
        BuildStamp.Text = BuildStampText();

        // Макет окна и строки списков красятся кодом, и цвет они берут в
        // момент построения. Без подписки те, что уже собраны, остались бы
        // в цвете прежней темы до перерисовки списка.
        Themes.Theme.Changed += OnThemeChanged;

        // Окно пересоздаётся при смене языка, а событие статическое: без
        // отписки закрытое окно висело бы в списке подписчиков до конца
        // работы программы вместе со всем своим деревом элементов
        Closed += (_, _) => Themes.Theme.Changed -= OnThemeChanged;

        Load();
    }

    /// <summary>Язык сменили — окно нужно пересоздать.</summary>
    public event Action? LanguageChanged;

    /// <summary>
    /// Смена языка. Строки в разметке читаются через x:Static при разборе
    /// XAML, поэтому обновить их на лету нельзя — окно пересоздаётся.
    /// </summary>
    private void OnLanguageChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;

        var wanted = ReferenceEquals(sender, LangEn) ? UiLanguage.English : UiLanguage.Russian;

        if (_settings.Language == wanted)
            return;

        _settings.Language = wanted;
        Persist();

        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// Смена темы. В отличие от языка окно пересоздавать не нужно: цвета
    /// разметки приходят через {DynamicResource} и меняются сами, а всё, что
    /// построено кодом, перерисовывает <see cref="OnThemeChanged"/>.
    /// </summary>
    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;

        var wanted = ReferenceEquals(sender, ThemeLight) ? UiTheme.Light : UiTheme.Dark;

        if (_settings.Theme == wanted)
            return;

        _settings.Theme = wanted;
        Persist();

        // Apply поднимает Changed — перерисовкой кода занимается OnThemeChanged()
        Themes.Theme.Apply(wanted);
    }

    /// <summary>
    /// Смена темы. Разметку красит {DynamicResource} сам, но макет окна в
    /// предпросмотре и строки списков строятся кодом и держат цвет, взятый
    /// при построении, — их приходится перерисовать.
    /// </summary>
    private void OnThemeChanged()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            RebuildMarkList();
            RebuildAppOverrides();
            UpdatePreview();
        }));
    }

    /// <summary>
    /// Секции въезжают по очереди при открытии окна.
    ///
    /// Задержка задаётся вручную, а не вычисляется: у панели справа есть
    /// прокрутка, и часть секций приходит в кадр позже. Всё равно очередь
    /// ровная и предсказуемая — с вычислением она зависела бы от прокрутки
    /// и прыгала при первом изменении размеров окна.
    ///
    /// Анимация сдвигает только прозрачность и вертикальное смещение:
    /// оба идут через композитор, и окно открывается плавно даже на слабом
    /// ноутбуке.
    /// </summary>
    private void AnimateIn()
    {
        if (SettingsPanel is null)
            return;

        var groups = SettingsPanel.Children.OfType<Border>().ToList();
        int index = 0;

        foreach (Border group in groups)
        {
            group.RenderTransformOrigin = new Point(0.5, 0.5);
            group.RenderTransform = new TranslateTransform(0, 14);
            group.Opacity = 0;

            var board = new Storyboard();

            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(280),
                BeginTime = TimeSpan.FromMilliseconds(index * 45),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };

            var rise = new DoubleAnimation
            {
                From = 14,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(420),
                BeginTime = TimeSpan.FromMilliseconds(index * 45),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };

            Storyboard.SetTargetProperty(fade, new PropertyPath(UIElement.OpacityProperty));
            Storyboard.SetTarget(fade, group);

            Storyboard.SetTargetProperty(rise, new PropertyPath(TranslateTransform.YProperty));
            Storyboard.SetTarget(rise, group.RenderTransform);

            board.Children.Add(fade);
            board.Children.Add(rise);

            index++;
            board.Begin(this, isControllable: true);
        }
    }

    /// <summary>Заголовок окна: перетаскивание, двойной щелчок и кружки.</summary>
    private void OnTitleBarDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        // Щелчок по кружку не должен двигать окно
        if (FindButton(e.OriginalSource as DependencyObject) is not null)
            return;

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Кнопку отпустили раньше, чем перетаскивание началось
            }
        }
    }

    /// <summary>Кружки в заголовке: развернуть, свернуть, закрыть.</summary>
    private void OnTrafficClick(object sender, RoutedEventArgs e)
    {
        switch ((sender as FrameworkElement)?.Tag as string)
        {
            case "Max":
                ToggleMaximize();
                break;
            case "Min":
                WindowState = WindowState.Minimized;
                break;
            case "Close":
                Close();
                break;
        }
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>Ищет кнопку вверх по дереву: клик по кружку не двигает окно.</summary>
    private static System.Windows.Controls.Primitives.ButtonBase? FindButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase button)
                return button;

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    /// <summary>
    /// Метка сборки: время файла программы.
    ///
    /// Время берётся у .exe, а не у сборки в памяти: в однофайловой
    /// сборке папки рядом нет, а файл — есть, и его время меняется при
    /// каждой пересборке.
    /// </summary>
    private static string BuildStampText()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return string.Empty;

            var time = File.GetLastWriteTime(path);
            return $"build {time:yy.MM.dd HH:mm}";
        }
        catch
        {
            return string.Empty;
        }
    }

    private void Load()
    {
        _ready = false;

        // Появление запускаем по событию Loaded, а не здесь. В конструкторе
        // окно ещё не показано, и анимация отыгрывает в пустоте: к моменту
        // показа всё уже на месте, и движение не видно вовсе.
        Loaded += (_, _) => AnimateIn();

        ThemeDark.IsChecked = _settings.Theme == UiTheme.Dark;
        ThemeLight.IsChecked = _settings.Theme == UiTheme.Light;

        switch (_settings.Background)
        {
            case BackgroundKind.Solid: BgSolid.IsChecked = true; break;
            case BackgroundKind.Image: BgImage.IsChecked = true; break;
            default: BgGradient.IsChecked = true; break;
        }

        SolidColorBox.Text = _settings.SolidColor;
        GradientStartBox.Text = _settings.GradientStart;
        GradientEndBox.Text = _settings.GradientEnd;
        AngleSlider.Value = _settings.GradientAngle;

        PaddingSlider.Value = _settings.Padding;
        ShadowCheck.IsChecked = _settings.ShadowEnabled;
        BlurSlider.Value = _settings.ShadowBlur;
        OpacitySlider.Value = _settings.ShadowOpacity * 100;
        OffsetSlider.Value = _settings.ShadowOffsetY;

        HideTitleCheck.IsChecked = _settings.HideTitleBar;
        TitleBarSlider.Value = _settings.TitleBarHeight;
        TitleAuto.IsChecked = _settings.TitleBarMode == TitleBarMode.Auto;
        TitleManual.IsChecked = _settings.TitleBarMode == TitleBarMode.Manual;

        RebuildAppOverrides();

        SaveCheck.IsChecked = _settings.SaveToDisk;
        StartupCheck.IsChecked = _settings.RunAtStartup;

        HotKeyLabel.Text = _settings.HotKey;

        LangRu.IsChecked = _settings.Language == UiLanguage.Russian;
        LangEn.IsChecked = _settings.Language == UiLanguage.English;

        // Тема применяется при старте приложения, но окно настроек можно
        // открыть и в том же процессе повторно — на всякий случай
        Themes.Theme.Apply(_settings.Theme);

        BuildPlacementSpots();
        FontBox.ItemsSource = Watermark.Fonts;

        // Сразу показываем первый элемент: пустой редактор выглядит поломкой,
        // хотя настроек может быть и нет, и добавлять их предстоит отсюда
        _selected = _settings.Watermark.Marks.Count > 0 ? 0 : -1;
        RebuildMarkList();
        LoadMarkIntoEditor();

        // Образцы повторяют сохранённый цвет, а не значение из разметки
        SetSwatch(SolidSwatch, _settings.SolidColor);
        SetSwatch(GradientStartSwatch, _settings.GradientStart);
        SetSwatch(GradientEndSwatch, _settings.GradientEnd);

        _ready = true;

        UpdateVisibility();
        UpdateLabels();
        UpdateBackgroundPreview();
        UpdatePreview();
    }

    /// <summary>Показывает только панель выбранного типа фона.</summary>
    private void UpdateVisibility()
    {
        SolidPanel.Visibility = _settings.Background == BackgroundKind.Solid
            ? Visibility.Visible : Visibility.Collapsed;

        GradientPanel.Visibility = _settings.Background == BackgroundKind.Gradient
            ? Visibility.Visible : Visibility.Collapsed;

        ImagePanel.Visibility = _settings.Background == BackgroundKind.Image
            ? Visibility.Visible : Visibility.Collapsed;

        ImagePathLabel.Text = string.IsNullOrWhiteSpace(_settings.BackgroundImagePath)
            ? I18n.Current["set.noFile"]
            : System.IO.Path.GetFileName(_settings.BackgroundImagePath);
    }

    /// <summary>
    /// Число для подписи ползунка, без длинного дробного хвоста.
    ///
    /// Ползунок даёт непрерывные значения, и в подпись попадало
    /// «23,879472693032028». Для читателя это выглядит как ошибка,
    /// хотя просто никто не округлял.
    /// </summary>
    private static string Number(double value) =>
        value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture);

    private void UpdateLabels()
    {
        PaddingLabel.Text = I18n.Format("set.padding", Number(PaddingSlider.Value));
        AngleLabel.Text = I18n.Format("set.angle", Number(AngleSlider.Value));
        BlurLabel.Text = I18n.Format("set.blur", Number(BlurSlider.Value));
        OpacityLabel.Text = I18n.Format("set.opacity", Number(OpacitySlider.Value));
        OffsetLabel.Text = I18n.Format("set.offset", Number(OffsetSlider.Value));
        TitleBarLabel.Text = I18n.Format("set.titleBarHeight", Number(TitleBarSlider.Value));

        // Заголовок не срезается — высота ничего не значит
        bool hideTitle = _settings.HideTitleBar;

        // И в автоматическом режиме ползунок бесполезен: высоту берёт окно
        bool manual = hideTitle && _settings.TitleBarMode == TitleBarMode.Manual;

        TitleBarSlider.IsEnabled = manual;
        TitleBarLabel.Opacity = manual ? 1 : 0.4;
        TitleBarModePanel.Visibility = hideTitle ? Visibility.Visible : Visibility.Collapsed;
        AppOverridesCaption.Opacity = hideTitle ? 1 : 0.4;

        // Тень выключена — её параметры бессмысленны
        var hasShadow = _settings.ShadowEnabled;
        BlurSlider.IsEnabled = hasShadow;
        OpacitySlider.IsEnabled = hasShadow;
        OffsetSlider.IsEnabled = hasShadow;
        BlurLabel.Opacity = hasShadow ? 1 : 0.4;
        OpacityLabel.Opacity = hasShadow ? 1 : 0.4;
        OffsetLabel.Opacity = hasShadow ? 1 : 0.4;
    }

    /// <summary>Живой образец фона — иначе цвет приходится запоминать на глаз.</summary>
    private void UpdateBackgroundPreview()
    {
        BackgroundPreview.Background = CurrentBackgroundBrush();
    }

    /// <summary>
    /// Фон по текущим настройкам. Общий и для узкой полоски образца, и для
    /// большого предпросмотра — иначе один рисовал бы градиент в другую
    /// сторону, чем другой.
    /// </summary>
    private Brush CurrentBackgroundBrush()
    {
        switch (_settings.Background)
        {
            case BackgroundKind.Solid:
                return new SolidColorBrush(ColorUtil.Parse(SolidColorBox.Text, "#E9EDF5"));

            case BackgroundKind.Image when FileOk(_settings.BackgroundImagePath):
                return TryImageBrush(_settings.BackgroundImagePath);

            default:
                var (gStart, gEnd) = ColorUtil.GradientAxis(AngleSlider.Value);
                var gradient = new LinearGradientBrush
                {
                    StartPoint = gStart,
                    EndPoint = gEnd,
                };
                gradient.GradientStops.Add(
                    new GradientStop(ColorUtil.Parse(GradientStartBox.Text, "#E9EDF5"), 0));
                gradient.GradientStops.Add(
                    new GradientStop(ColorUtil.Parse(GradientEndBox.Text, "#C3CDE0"), 1));
                return gradient;
        }
    }

    /// <summary>Размер, в котором собирается предпросмотр.</summary>
    private const double NominalPreviewWidth = 1200;
    private const double NominalPreviewHeight = 760;

    /// <summary>
    /// Оболочка предпросмотра. Живёт всё время работы окна и не пересоздаётся
    /// при правках знака: на ней висят обработчики перетаскивания, а новый
    /// слой им терял бы захват мыши посреди перетаскивания.
    /// </summary>
    private Grid? _previewRoot;

    /// <summary>Контейнер макета окна — его размеры зависят от отступа в настройках.</summary>
    private Border? _previewWindow;

    /// <summary>Контейнер слоя знаков: заменяется целиком при каждой правке.</summary>
    private Border? _previewMarkHost;

    /// <summary>
    /// Контейнер квадратов позиций: лежит поверх знаков и не пересобирается,
    /// только переключает отметку.
    /// </summary>
    private Canvas? _previewSpotHost;

    /// <summary>
    /// Большой предпросмотр слева: фон, макет окна и водяной знак.
    ///
    /// Композиция собирается в номинальном размере, а Viewbox ужимает её под
    /// панель. Так масштаб уменьшается ровно один раз и целиком: и надпись,
    /// и картинка, и отступы от края съезжают пропорционально, как в
    /// настоящем кадре. Рисовать уменьшенную копию вручную означало бы
    /// постоянно расходиться с боевым рендером.
    /// </summary>
    private void UpdatePreview()
    {
        BuildPreviewShell();

        if (_previewRoot is null || _previewWindow is null || _previewMarkHost is null)
            return;

        _previewRoot.Background = CurrentBackgroundBrush();

        double padding = _settings.Padding;
        _previewWindow.Width = NominalWidthFor(padding);
        _previewWindow.Height = NominalHeightFor(padding);
        _previewWindow.CornerRadius = new CornerRadius(_settings.CornerRadius);
        _previewWindow.Child = BuildMockWindowContent(
            _previewWindow.Width, _previewWindow.Height, PreviewTitleBarDip());

        ApplyMockWindowClip();

        RefreshPreviewMarks();
    }

    /// <summary>
    /// Срез заголовка и скругление в предпросмотре — теми же числами, что и в
    /// боевом рендере.
    ///
    /// Без этого макет врал бы: показывал рамку, которую кадр потом срежет,
    /// и пользователь выбрал бы высоту по неверным границам.
    /// </summary>
    private void ApplyMockWindowClip()
    {
        if (_previewWindow is null)
            return;

        double width = _previewWindow.Width;
        double height = _previewWindow.Height;

        if (width <= 0 || height <= 0)
            return;

        double radius = _settings.CornerRadius;

        if (!_settings.HideTitleBar)
        {
            var whole = new RectangleGeometry(new Rect(0, 0, width, height))
            {
                RadiusX = radius,
                RadiusY = radius,
            };

            _previewWindow.Clip = whole;
            return;
        }

        // Срез не должен съесть макет целиком
        double top = Math.Clamp(PreviewTitleBarDip(), 0, height - 24);

        // Верхние углы скруглены, нижние прямые: снизу рама уходит в тень
        var cropped = new RectangleGeometry(new Rect(0, top, width, height - top))
        {
            RadiusX = radius,
            RadiusY = radius,
        };

        _previewWindow.Clip = cropped;
    }

    /// <summary>
    /// Создаёт оболочку предпросмотра один раз за сеанс окна.
    /// Внутри два слоя: макет окна и знаки поверх него.
    /// </summary>
    private void BuildPreviewShell()
    {
        if (_previewRoot is not null)
            return;

        var window = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
        };

        // Ссылка на ресурс, а не кисть: макет переживает переключение темы,
        // и цвет, взятый при построении, остался бы прежним
        window.SetResourceReference(Control.BackgroundProperty, "MockWindow");

        var markHost = new Border
        {
            Width = NominalPreviewWidth,
            Height = NominalPreviewHeight,
        };

        // Квадраты позиций поверх всего: ими выбирают, куда поставить знак
        var spotHost = new Canvas
        {
            Width = NominalPreviewWidth,
            Height = NominalPreviewHeight,
        };

        var root = new Grid
        {
            Width = NominalPreviewWidth,
            Height = NominalPreviewHeight,
            Children = { window, markHost, spotHost },
        };

        // Порядок снизу вверх: макет окна, квадраты позиций, сами знаки.
        // Квадраты под знаками нужны, чтобы не лежать поверх надписи и не
        // портить её читаемость; выбрать позицию всё равно можно — там, где
        // знака нет, квадрат кликается напрямую.
        Panel.SetZIndex(markHost, 10);
        Panel.SetZIndex(spotHost, 5);

        root.PreviewMouseLeftButtonDown += OnPreviewMarkDown;
        root.PreviewMouseMove += OnPreviewMarkMove;
        root.PreviewMouseLeftButtonUp += OnPreviewMarkUp;

        _previewWindow = window;
        _previewMarkHost = markHost;
        _previewSpotHost = spotHost;
        _previewRoot = root;

        BuildPlacementSpots();

        WatermarkPreview.Child = new Viewbox
        {
            // Только вниз: на низком окне настроек образец должен остаться
            // читаемым, а не растянуться в размытую полосу.
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            // К верхнему краю, а не по центру: кадр широкий, панель высокая,
            // и при центрировании сверху и снизу оставалось бы пустое поле
            VerticalAlignment = VerticalAlignment.Top,
            Child = root,
        };

        WatermarkPreview.SizeChanged += (_, _) => ApplyPreviewClip();
        ApplyPreviewClip();
    }

    /// <summary>
    /// Скругляет содержимое предпросмотра.
    ///
    /// WPF умеет скруглять углы у самой рамки, но не у её содержимого:
    /// ClipToBounds у Border режет по обычному прямоугольнику, и картинка
    /// всё равно уезжает в углы. Поэтому содержимое режется явно геометрией.
    /// Радиус на пиксель меньше угла рамки — иначе по краю видна полоска рамки.
    /// </summary>
    private void ApplyPreviewClip()
    {
        double width = WatermarkPreview.ActualWidth;
        double height = WatermarkPreview.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        WatermarkPreview.Clip = new RectangleGeometry(new Rect(0, 0, width, height), 11, 11);
    }

    /// <summary>
    /// Меняет только слой знаков. Оболочка и макет окна остаются на месте —
    /// именно поэтому перетаскивание не рвётся на середине.
    /// </summary>
    private void RefreshPreviewMarks()
    {
        if (_previewMarkHost is null)
            return;

        _previewMarkHost.Child = WatermarkRenderer.Build(
            _settings.Watermark, NominalPreviewWidth, NominalPreviewHeight,
            interactive: true);

        // Отметку на квадратах держим в шаге с выбранным элементом: слой
        // знаков перерисовывается на каждый ползунок, и без этого квадрат
        // оставался бы на старой позиции.
        var selected = Selected;
        if (selected is not null)
            SyncPlacementButtons(selected.Placement);
    }

    /// <summary>Прикидка содержимого снятого окна.</summary>
    private static FrameworkElement BuildMockWindowContent(
        double width, double height, double titleHeight)
    {
        var content = new Grid { Margin = new Thickness(20) };
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Заголовок рисуется высотой, которую срезает кадр: иначе макет и
        // реальное окно расходились бы на всю разницу
        content.Children.Add(BuildMockTitleBar(
            titleHeight <= 0 ? 34 : titleHeight + 8));

        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Top };

        // Строки разной длины: одинаковые читались бы как гистограмма,
        // а не как содержимое окна
        var mockLine = (Color)((SolidColorBrush)Themes.Theme.Palette.Of("MockLine")).Color;

        foreach (double share in new[] { 0.92, 0.75, 0.86, 0.55 })
        {
            lines.Children.Add(new Border
            {
                Height = 12,
                Width = Math.Round((width - 40) * share),
                Margin = new Thickness(0, 0, 0, 12),
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(
                    (byte)(70 * share), mockLine.R, mockLine.G, mockLine.B)),
            });
        }

        Grid.SetRow(lines, 2);
        content.Children.Add(lines);
        return content;
    }

    /// <summary>При огромном отступе поля съедали бы макет окна целиком.</summary>
    private static double NominalWidthFor(double padding) =>
        Math.Max(240, Math.Min(1200 - padding * 2, 1200 - 24));

    private static double NominalHeightFor(double padding) =>
        Math.Max(160, Math.Min(760 - padding * 2, 760 - 24));

    private static FrameworkElement BuildMockTitleBar(double height)
    {
        var bar = new Grid { Height = height };

        var title = new Border
        {
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 0, 12),
        };
        title.SetResourceReference(Control.BackgroundProperty, "MockTitle");
        bar.Children.Add(title);

        // Три кружка слева — узнаваемый признак окна, как в macOS
        var dots = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(2, 7, 0, 0),
        };

        foreach (var shade in new byte[] { 0xE0, 0xC0, 0x90 })
        {
            dots.Children.Add(new Ellipse
            {
                Width = 9,
                Height = 9,
                Margin = new Thickness(0, 0, 6, 0),
                Fill = new SolidColorBrush(Color.FromRgb(
                    shade, (byte)(shade - 0x10), (byte)(shade - 0x20))),
            });
        }

        bar.Children.Add(dots);
        return bar;
    }

    private static bool FileOk(string path) =>
        !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path);

    private static Brush TryImageBrush(string path)
    {
        try
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();

            return new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
        }
        catch
        {
            return new SolidColorBrush(Colors.White);
        }
    }

    // ---- Водяной знак ----

    /// <summary>Индекс выбранного элемента в списке.</summary>
    private int _selected = -1;

    /// <summary>
    /// Защита от лишней работы при переносе настроек в поля редактора: без неё
    /// выбор элемента тут же записывался бы обратно и перерисовывал список,
    /// а правка текста гоняла бы это по кругу.
    /// </summary>
    private bool _loading;

    /// <summary>
    /// Идёт программная отметка квадрата позиции. Нужен, чтобы отметка,
    /// которую ставит <see cref="SyncPlacementButtons"/>, не выглядела для
    /// обработчика кликом пользователя и не двигала выбранный элемент.
    /// </summary>
    private bool _syncing;

    /// <summary>Выбранный элемент или null, если список пуст.</summary>
    private Mark? Selected =>
        _selected >= 0 && _selected < _settings.Watermark.Marks.Count
            ? _settings.Watermark.Marks[_selected]
            : null;

    /// <summary>
    /// Позиции квадратов на рамке кадра в координатах холста.
    /// Порядок совпадает со значением <see cref="Placement"/>: строка за
    /// строкой, поэтому индекс точки — это и есть номер позиции.
    /// </summary>
    private static readonly (double X, double Y)[] Spots =
    {
        (24, 24),       (600, 24),      (1176, 24),
        (24, 380),      (600, 380),     (1176, 380),
        (24, 736),      (600, 736),     (1176, 736),
    };

    /// <summary>
    /// Раскладывает квадраты позиций по рамке кадра предпросмотра.
    ///
    /// Квадраты стоят поверх знаков: ими удобнее выбрать место, чем тянуть
    /// вслепую. Центральная точка попадает на макет окна — так видно, что
    /// знак окажется на самом окне, а не на поле вокруг него.
    /// </summary>
    private void BuildPlacementSpots()
    {
        if (_previewSpotHost is null)
            return;

        for (int i = 0; i < Spots.Length; i++)
        {
            var (x, y) = Spots[i];

            var spot = new RadioButton
            {
                Style = (Style)FindResource("Spot"),
                // GroupName обязателен: без него RadioButton'ы не знают друг о друге
                // и отмечались бы все девять сразу.
                GroupName = "previewSpots",
                // Индекс точки в Spots — это номер позиции, поэтому по Tag
                // квадрат сопоставляется со значением Placement.
                Tag = (Placement)i,
            };

            // Квадрат 24x24, поэтому его левый верхний угол — на 12 раньше точки
            Canvas.SetLeft(spot, x - 12);
            Canvas.SetTop(spot, y - 12);

            spot.Checked += OnPlacementPicked;
            _previewSpotHost.Children.Add(spot);
        }
    }

    /// <summary>
    /// Список надписей и картинок. Строится кодом, а не шаблоном: строки
    /// короткие, их немного, а состав меняется по кнопкам — с привязкой
    /// получилось бы больше кода, чем с пересборкой руками.
    /// </summary>
    private void RebuildMarkList()
    {
        MarkList.Children.Clear();

        var marks = _settings.Watermark.Marks;

        for (int i = 0; i < marks.Count; i++)
        {
            int index = i;
            var mark = marks[i];

            var remove = new Button
            {
                Style = (Style)FindResource("CloseButton"),
                Tag = index,
            };
            remove.Click += OnRemoveMarkClick;

            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new TextBlock
            {
                // Буква вместо иконки: набор иконок у нас один, а здесь
                // достаточно одного символа, чтобы различать виды
                Text = mark.Kind == MarkKind.Text ? "T" : "▤",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 11, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ResourceLink(badge, TextBlock.ForegroundProperty, "BadgeText");

            var summary = new TextBlock
            {
                Text = mark.Summary,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            // Цвет задаём явно: неявный стиль достаётся только элементам
            // из разметки, а строки списка строятся кодом, и без этого
            // подпись осталась бы цвета фона — то есть невидимой.
            ResourceLink(summary, TextBlock.ForegroundProperty, "Text");

            var kind = new TextBlock
            {
                Text = mark.Kind == MarkKind.Text
                    ? I18n.Current["set.markText"]
                    : I18n.Current["set.markPicture"],
                FontSize = 11,
                Margin = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            ResourceLink(kind, TextBlock.ForegroundProperty, "Subtle");

            Grid.SetColumn(badge, 0);
            Grid.SetColumn(summary, 1);
            Grid.SetColumn(kind, 2);
            Grid.SetColumn(remove, 3);

            content.Children.Add(badge);
            content.Children.Add(summary);
            content.Children.Add(kind);
            content.Children.Add(remove);

            var row = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(11, 8, 11, 8),
                Margin = new Thickness(0, 0, 0, 6),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = index,
                Child = content,
            };

            row.MouseLeftButtonUp += OnMarkRowClick;

            if (index == _selected)
                MarkRowSelected(row);
            else
                MarkRowPlain(row);

            MarkList.Children.Add(row);
        }

        MarkEditor.Visibility = Selected is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void MarkRowSelected(Border row)
    {
        ResourceLink(row, Control.BackgroundProperty, "RowSelected");
        ResourceLink(row, Border.BorderBrushProperty, "Accent");
    }

    private static void MarkRowPlain(Border row)
    {
        ResourceLink(row, Control.BackgroundProperty, "RowBg");
        ResourceLink(row, Border.BorderBrushProperty, "CardLine");
    }

    /// <summary>
    /// Привязывает свойство элемента к кисти из ресурсов приложения.
    ///
    /// Элементы, которые окно строит кодом, не берут цвета темы сами: у них
    /// нет неявного стиля из разметки, а кисть, прочитанная при построении,
    /// пережила бы переключение темы. Ссылка на ресурс обновляется вместе со
    /// всеми остальными.
    /// </summary>
    private static void ResourceLink(FrameworkElement target, DependencyProperty property, string key) =>
        target.SetResourceReference(property, key);

    /// <summary>Клик по строке списка — выбрать элемент для правки.</summary>
    private void OnMarkRowClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Крестик лежит внутри строки: без этой проверки удаление
        // тут же выбирало бы уже удалённый элемент
        if (e.OriginalSource is System.Windows.Controls.Primitives.ButtonBase)
            return;

        if (sender is Border { Tag: int index })
            SelectMark(index);
    }

    private void SelectMark(int index)
    {
        _selected = index;
        RebuildMarkList();
        LoadMarkIntoEditor();
        UpdatePreview();
    }

    /// <summary>
    /// Переносит выбранный элемент в поля редактора.
    ///
    /// Под _loading обработчики молчат: присваивание Text само по себе
    /// вызывает TextChanged, который иначе записал бы старое значение
    /// обратно поверх только что прочитанного.
    /// </summary>
    private void LoadMarkIntoEditor()
    {
        var mark = Selected;
        if (mark is null)
            return;

        _loading = true;
        try
        {
            bool text = mark.Kind == MarkKind.Text;

            MarkKindLabel.Text = text
                ? I18n.Current["set.markText"]
                : I18n.Current["set.markPicture"];

            MarkTextField.Visibility = text ? Visibility.Visible : Visibility.Collapsed;
            MarkPicturePanel.Visibility = text ? Visibility.Collapsed : Visibility.Visible;
            MarkStylePanel.Visibility = text ? Visibility.Visible : Visibility.Collapsed;
            MarkColorRow.Visibility = text ? Visibility.Visible : Visibility.Collapsed;
            // Шрифт нужен только надписи: у картинки его нет, а открытый
            // список на месте текста выглядит как недоделка.
            MarkFontPanel.Visibility = text ? Visibility.Visible : Visibility.Collapsed;

            if (text)
            {
                MarkContentBox.Text = mark.Text;

                FontBox.SelectedItem = Watermark.Fonts.Contains(mark.FontFamily)
                    ? mark.FontFamily
                    : "Segoe UI";

                MarkColorBox.Text = mark.Color;
                SetSwatch(MarkColorSwatch, mark.Color);

                BoldCheck.IsChecked = mark.Bold;
                ItalicCheck.IsChecked = mark.Italic;
                MarkShadowCheck.IsChecked = mark.Shadow;

                ShadowColorBox.Text = mark.ShadowColor;
                SetSwatch(ShadowColorSwatch, mark.ShadowColor);
                ShadowBlurSlider.Value = mark.ShadowBlur;
                ShadowOpacitySlider.Value = mark.ShadowOpacity * 100;

                MarkSizeSlider.Minimum = 6;
                MarkSizeSlider.Maximum = 200;
                MarkSizeSlider.Value = mark.Size;

                MarkShadowPanel.Visibility = mark.Shadow ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                MarkPathLabel.Text = string.IsNullOrWhiteSpace(mark.Path)
                    ? I18n.Current["set.noFile"]
                    : System.IO.Path.GetFileName(mark.Path);

                // У картинки размер — это высота, у надписи кегль. Диапазоны
                // разные, иначе один ползунок тянул бы оба смысла сразу
                MarkSizeSlider.Minimum = 8;
                MarkSizeSlider.Maximum = 400;
                MarkSizeSlider.Value = mark.Size;
            }

            MarkOpacitySlider.Value = mark.Opacity * 100;
            MarkMarginSlider.Value = mark.Margin;

            SyncPlacementButtons(mark.Placement);
        }
        finally
        {
            _loading = false;
        }

        UpdateMarkLabels();
    }

    /// <summary>
    /// Ставит галочку на квадрат и прячет занятые точки.
    ///
    /// Отметку ставим под флагом _syncing: присваивание IsChecked поднимает
    /// Checked, и без флага обработчик клика считал бы нашу же программную
    /// отметку действием пользователя — и переносил выбранный элемент.
    ///
    /// Занятые точки прячем: там уже стоит знак, и квадрат лежал бы поверх
    /// него. Кликнуть туда и не с чем — перенос делается перетаскиванием.
    /// </summary>
    private void SyncPlacementButtons(Placement place)
    {
        if (_previewSpotHost is null)
            return;

        var taken = _settings.Watermark.Marks
            .Where(m => m.IsRenderable)
            .Select(m => m.Placement)
            .ToHashSet();

        _syncing = true;
        try
        {
            foreach (var spot in _previewSpotHost.Children.OfType<RadioButton>())
            {
                var here = (Placement)spot.Tag!;

                spot.Visibility = taken.Contains(here)
                    ? Visibility.Collapsed
                    : Visibility.Visible;

                spot.IsChecked = here == place;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnAddTextMark(object sender, RoutedEventArgs e)
    {
        var mark = new Mark
        {
            Kind = MarkKind.Text,
            Text = I18n.Current["set.markDefaultText"],
            Placement = Placement.BottomCenter,
            Size = 24,
        };

        _settings.Watermark.Marks.Add(mark);
        _selected = _settings.Watermark.Marks.Count - 1;

        AfterListChanged();
    }

    private void OnAddPictureMark(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = I18n.Current["set.pickPicture"],
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var mark = new Mark
        {
            Kind = MarkKind.Picture,
            Path = dialog.FileName,
            Placement = Placement.TopLeft,
            Size = 32,
        };

        _settings.Watermark.Marks.Add(mark);
        _selected = _settings.Watermark.Marks.Count - 1;

        AfterListChanged();
    }

    /// <summary>Крестик в строке списка.</summary>
    private void OnRemoveMarkClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index })
            return;

        _settings.Watermark.Marks.RemoveAt(index);
        _selected = ClampSelection();

        AfterListChanged();
    }

    private void OnRemoveMark(object sender, RoutedEventArgs e)
    {
        if (Selected is null)
            return;

        _settings.Watermark.Marks.Remove(Selected);
        _selected = ClampSelection();

        AfterListChanged();
    }

    /// <summary>
    /// После удаления индекс мог уйти за конец списка. Если список опустел,
    /// выбор сбрасывается, и редактор прячется.
    /// </summary>
    private int ClampSelection()
    {
        int count = _settings.Watermark.Marks.Count;

        if (count == 0)
            return -1;

        return Math.Clamp(_selected, 0, count - 1);
    }

    /// <summary>Общий хвост для добавления и удаления элемента.</summary>
    private void AfterListChanged()
    {
        RebuildMarkList();
        LoadMarkIntoEditor();
        UpdatePreview();
        Persist();
    }

    private void OnPlacementPicked(object sender, RoutedEventArgs e)
    {
        if (_loading || _syncing || !_ready) return;
        if (sender is not RadioButton radio || radio.Tag is not Placement place) return;
        if (Selected is null) return;

        Selected.Placement = place;
        UpdatePreview();
        Persist();
    }

    private void OnMarkChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || !_ready || Selected is null)
            return;

        var mark = Selected;

        // Округляем: ползунок даёт непрерывное значение, а в файле настроек
        // незачем хранить двадцать знаков после запятой
        if (ReferenceEquals(sender, MarkContentBox))
            mark.Text = MarkContentBox.Text;
        else if (ReferenceEquals(sender, MarkSizeSlider))
            mark.Size = Math.Round(MarkSizeSlider.Value);
        else if (ReferenceEquals(sender, MarkOpacitySlider))
            mark.Opacity = Math.Round(MarkOpacitySlider.Value) / 100.0;
        else if (ReferenceEquals(sender, MarkMarginSlider))
            mark.Margin = Math.Round(MarkMarginSlider.Value);
        else if (ReferenceEquals(sender, FontBox))
            mark.FontFamily = FontBox.SelectedItem as string ?? "Segoe UI";
        else if (ReferenceEquals(sender, BoldCheck))
            mark.Bold = BoldCheck.IsChecked == true;
        else if (ReferenceEquals(sender, ItalicCheck))
            mark.Italic = ItalicCheck.IsChecked == true;
        else if (ReferenceEquals(sender, MarkShadowCheck))
            mark.Shadow = MarkShadowCheck.IsChecked == true;
        else if (ReferenceEquals(sender, ShadowBlurSlider))
            mark.ShadowBlur = Math.Round(ShadowBlurSlider.Value);
        else if (ReferenceEquals(sender, ShadowOpacitySlider))
            mark.ShadowOpacity = Math.Round(ShadowOpacitySlider.Value) / 100.0;
        else
            return;

        // Подпись строки в списке показывает сам текст, поэтому список
        // приходится пересобирать на каждый символ. Элементов мало,
        // а пересборка заодно заново подсвечивает выбранную строку.
        RebuildMarkList();

        MarkShadowPanel.Visibility = mark.Shadow ? Visibility.Visible : Visibility.Collapsed;

        UpdateMarkLabels();
        UpdatePreview();
        Persist();
    }

    /// <summary>Цвет поменялся в поле ввода — пишем его в настройки и в образец.</summary>
    private void OnMarkColorChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || !_ready || Selected is null)
            return;

        if (ReferenceEquals(sender, MarkColorBox))
        {
            Selected.Color = ColorUtil.Normalize(MarkColorBox.Text, "#FFFFFF");
            SetSwatch(MarkColorSwatch, Selected.Color);
        }
        else if (ReferenceEquals(sender, ShadowColorBox))
        {
            Selected.ShadowColor = ColorUtil.Normalize(ShadowColorBox.Text, "#000000");
            SetSwatch(ShadowColorSwatch, Selected.ShadowColor);
        }
        else
        {
            return;
        }

        UpdatePreview();
        Persist();
    }

    /// <summary>Уход с поля: возвращаем канонический вид #RRGGBB.</summary>
    private void OnMarkCommitted(object sender, RoutedEventArgs e)
    {
        if (Selected is null)
            return;

        if (ReferenceEquals(sender, MarkContentBox))
            MarkContentBox.Text = Selected.Text;
        else if (ReferenceEquals(sender, MarkColorBox))
            MarkColorBox.Text = Selected.Color;
        else if (ReferenceEquals(sender, ShadowColorBox))
            ShadowColorBox.Text = Selected.ShadowColor;
    }

    private void OnPickMarkColor(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => ApplyMarkColor("color.text", MarkColorBox);

    private void OnPickShadowColor(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => ApplyMarkColor("color.shadow", ShadowColorBox);

    /// <summary>
    /// Палитра для полей водяного знака. Значение попадает в настройки через
    /// TextChanged того же поля — отдельной записи не делаем.
    /// </summary>
    private void ApplyMarkColor(string titleKey, TextBox box)
    {
        var picker = new ColorPickerWindow(I18n.Current[titleKey], box.Text) { Owner = this };

        if (picker.ShowDialog() != true)
            return;

        box.Text = picker.Selected;
    }

    private void OnPickMarkPicture(object sender, RoutedEventArgs e)
    {
        if (Selected is not { Kind: MarkKind.Picture } mark)
            return;

        var dialog = new OpenFileDialog
        {
            Title = I18n.Current["set.pickPicture"],
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
            InitialDirectory = string.IsNullOrWhiteSpace(mark.Path)
                ? null
                : System.IO.Path.GetDirectoryName(mark.Path),
        };

        if (dialog.ShowDialog(this) != true)
            return;

        mark.Path = dialog.FileName;
        MarkPathLabel.Text = System.IO.Path.GetFileName(dialog.FileName);

        RebuildMarkList();
        UpdatePreview();
        Persist();
    }

    // ---- Перетаскивание по предпросмотру ----

    /// <summary>Знак, за который сейчас тянут.</summary>
    private Mark? _dragging;

    /// <summary>Точка, где начали тянуть — от неё считаем, дрогнули ли мы.</summary>
    private Point _dragOrigin;

    /// <summary>Порог в пикселях, после которого жест считается перетаскиванием, а не кликом.</summary>
    private const double DragThreshold = 4;

    /// <summary>
    /// Ищет знак под курсором: сам элемент несёт Tag, а у строки группы
    /// лежит первый знак этой группы. Поэтому клик по строке целиком
    /// тоже понимается.
    /// </summary>
    private static Mark? MarkUnder(DependencyObject? source)
    {
        for (var node = source; node is not null; node = System.Windows.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is FrameworkElement { Tag: Mark mark })
                return mark;
        }

        return null;
    }

    /// <summary>
    /// Позиция по трём на три. Порядок значений <see cref="Placement"/> как раз
    /// построчный, поэтому из номера строки и колонки получается нужное имя.
    /// </summary>
    private static Placement PlacementAt(Point point)
    {
        int column = point.X < NominalPreviewWidth / 3 ? 0
            : point.X < NominalPreviewWidth * 2 / 3 ? 1 : 2;

        int row = point.Y < NominalPreviewHeight / 3 ? 0
            : point.Y < NominalPreviewHeight * 2 / 3 ? 1 : 2;

        return (Placement)(row * 3 + column);
    }

    private void OnPreviewMarkDown(object sender, MouseButtonEventArgs e)
    {
        if (!_ready || _previewRoot is null)
            return;

        var mark = MarkUnder(e.OriginalSource as DependencyObject);
        if (mark is null)
            return;

        int index = _settings.Watermark.Marks.IndexOf(mark);

        // Клик без перетаскивания просто выбирает элемент. Пересобирать
        // редактор под рукой незачем, если он и так уже открыт.
        if (index >= 0 && index != _selected)
            SelectMark(index);

        _dragging = mark;
        _dragOrigin = e.GetPosition(_previewRoot);
    }

    private void OnPreviewMarkMove(object sender, MouseEventArgs e)
    {
        if (_previewRoot is null)
            return;

        var hovered = MarkUnder(e.OriginalSource as DependencyObject);
        bool pressed = e.LeftButton == MouseButtonState.Pressed;

        if (_dragging is null || !pressed)
        {
            // Курсор подаёт знак, что элемент можно тянуть
            _previewRoot.Cursor = hovered is null ? Cursors.Arrow : Cursors.SizeAll;
            return;
        }

        _previewRoot.Cursor = Cursors.SizeAll;

        var point = e.GetPosition(_previewRoot);
        double dx = point.X - _dragOrigin.X;
        double dy = point.Y - _dragOrigin.Y;

        // Небольшое дрожание мыши не должно перебрасывать знак в соседнюю клетку
        if (Math.Abs(dx) < DragThreshold && Math.Abs(dy) < DragThreshold)
            return;

        var wanted = PlacementAt(point);
        if (_dragging.Placement == wanted)
            return;

        _dragging.Placement = wanted;
        SyncPlacementButtons(wanted);

        // Перерисовываем только слой знаков: оболочка с обработчиками
        // должна пережить перетаскивание
        RefreshPreviewMarks();
        Persist();
    }

    private void OnPreviewMarkUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = null;
    }

    /// <summary>
    /// Открывает внешнюю ссылку. Браузером по умолчанию, потому что просто
    /// так уронить ссылку в системный браузер нельзя — только через оболочку.
    /// </summary>
    private void OnOpenLink(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Ссылка не открылась — молча: в логе это бесполезно,
            // а пользователю всё равно нужен браузер, а не отчёт об ошибке
        }

        e.Handled = true;
    }

    // ---- Свой срез заголовка для отдельных приложений ----

    /// <summary>
    /// Число рядом с ползунком для каждой строки.
    ///
    /// Держим ссылками, а не ищем по дереву: строка знает своё приложение,
    /// и обновление подписи при движении ползунка должно попасть именно в неё.
    /// </summary>
    private readonly Dictionary<string, TextBlock> _heightLabels = new();

    /// <summary>
    /// Строит список приложений со своей высотой среза.
    ///
    /// Список наполняется сам при съёмке, поэтому руками добавлять нечего —
    /// строки только правятся и удаляются. Сортируем по имени: пути длинные,
    /// а читать список нужно сверху вниз.
    /// </summary>
    private void RebuildAppOverrides()
    {
        AppOverrides.Children.Clear();
        _heightLabels.Clear();

        var entries = _settings.TitleBarOverrides
            .OrderBy(pair => System.IO.Path.GetFileName(pair.Key), StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var (app, height) in entries)
        {
            var name = new TextBlock
            {
                Text = System.IO.Path.GetFileName(app),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = app,
            };
            ResourceLink(name, TextBlock.ForegroundProperty, "Text");

            var value = new TextBlock
            {
                Text = Number(height),
                Width = 30,
                Margin = new Thickness(6, 0, 6, 0),
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ResourceLink(value, TextBlock.ForegroundProperty, "Subtle");

            // Своя настройка на весь ряд: тянуть ползунок в строке удобнее,
            // а точное значение всегда видно рядом числом.
            // Стиль не задаём — у ползунка он неявный и применяется сам.
            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 160,
                Width = 100,
                VerticalAlignment = VerticalAlignment.Center,
                Value = height,
                Tag = app,
            };
            slider.ValueChanged += OnAppHeightChanged;

            var remove = new Button
            {
                Style = (Style)FindResource("CloseButton"),
                Margin = new Thickness(10, 0, 0, 0),
                Tag = app,
            };
            remove.Click += OnRemoveAppOverride;

            var row = new Border
            {
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(11, 7, 11, 7),
                Margin = new Thickness(0, 0, 0, 6),
            };
            ResourceLink(row, Control.BackgroundProperty, "RowBg");
            ResourceLink(row, Border.BorderBrushProperty, "CardLine");

            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(name, 0);
            Grid.SetColumn(value, 1);
            Grid.SetColumn(slider, 2);
            Grid.SetColumn(remove, 3);

            content.Children.Add(name);
            content.Children.Add(value);
            content.Children.Add(slider);
            content.Children.Add(remove);

            row.Child = content;
            AppOverrides.Children.Add(row);

            _heightLabels[app] = value;
        }

        bool empty = entries.Count == 0;
        AppOverridesEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        AppOverrides.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// Добавляет приложение вручную.
    ///
    /// Без этого список наполнялся бы только теми приложениями, чьи окна уже
    /// снимались, а добавить заранее — например, чтобы подготовить настройку
    /// до первого снимка — было нечем.
    ///
    /// Начальная высота — системный заголовок: для обычного приложения это
    /// ровно то, что нужно, и дальше её можно подвинуть ползунком.
    ///
    /// Ярлыки не принимаем: ключом служит путь к .exe, а у ярлыка свой, и
    /// запись с ним никогда не совпала бы с реальным окном.
    /// </summary>
    private void OnAddAppOverride(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;

        var dialog = new OpenFileDialog
        {
            Title = I18n.Current["set.addApp"],
            Filter = "Applications|*.exe",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
            return;

        string path = dialog.FileName;

        // Повторное добавление не плодит дубликаты: у ключа тот же путь,
        // и запись просто обновится
        if (!_settings.TitleBarOverrides.ContainsKey(path))
            _settings.TitleBarOverrides[path] = Interop.Native.GetSystemCaptionHeight();

        RebuildAppOverrides();
        Persist();
    }

    private void OnAppHeightChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || !_ready || sender is not Slider { Tag: string app })
            return;

        double height = Math.Round(e.NewValue);
        _settings.TitleBarOverrides[app] = height;

        // Число в строке живёт своей жизнью: ползунок сам своё Text не знает,
        // а список пересобирается только при добавлении и удалении. Без этой
        // строки значение менялось бы в настройках и в кадре, но не на глаз.
        if (_heightLabels.TryGetValue(app, out var label))
            label.Text = Number(height);

        Persist();
    }

    private void OnRemoveAppOverride(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not Button { Tag: string app })
            return;

        _settings.TitleBarOverrides.Remove(app);
        RebuildAppOverrides();
        UpdatePreview();
        Persist();
    }

    private void UpdateMarkLabels()
    {        var mark = Selected;
        if (mark is null)
            return;

        bool text = mark.Kind == MarkKind.Text;

        MarkSizeLabel.Text = text
            ? I18n.Format("set.markFontSize", Number(MarkSizeSlider.Value))
            : I18n.Format("set.pictureHeight", Number(MarkSizeSlider.Value));

        MarkOpacityLabel.Text = text
            ? I18n.Format("set.markOpacity", Number(MarkOpacitySlider.Value))
            : I18n.Format("set.pictureOpacity", Number(MarkOpacitySlider.Value));

        MarkMarginLabel.Text = text
            ? I18n.Format("set.markMargin", Number(MarkMarginSlider.Value))
            : I18n.Format("set.pictureMargin", Number(MarkMarginSlider.Value));

        ShadowBlurLabel.Text = I18n.Format("set.shadowBlur", Number(ShadowBlurSlider.Value));
        ShadowOpacityLabel.Text = I18n.Format("set.shadowOpacity", Number(ShadowOpacitySlider.Value));
    }

    // ---- Обработчики ----

    private void OnBackgroundChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready || sender is not RadioButton { Tag: string tag })
            return;

        _settings.Background = tag switch
        {
            "Solid" => BackgroundKind.Solid,
            "Image" => BackgroundKind.Image,
            _ => BackgroundKind.Gradient,
        };

        UpdateVisibility();
        UpdateBackgroundPreview();
        UpdatePreview();
        Persist();
    }

    /// <summary>
    /// Пересчитывает вид и предпросмотр после правки цвета текстом
    /// или выбором из палитры. Отдельный метод, чтобы не звать обработчик
    /// события с null вместо отправителя.
    /// </summary>
    private void RefreshBackground()
    {
        if (!_ready)
            return;

        UpdateVisibility();
        UpdateBackgroundPreview();
        UpdatePreview();
        Persist();
    }

    private void OnAngleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;

        _settings.GradientAngle = e.NewValue;
        UpdateLabels();
        UpdateBackgroundPreview();
        UpdatePreview();
        Persist();
    }

    private void OnPickSolid(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => ApplyPickedColor("color.solid", SolidColorBox);

    private void OnPickGradientStart(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => ApplyPickedColor("color.start", GradientStartBox);

    private void OnPickGradientEnd(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => ApplyPickedColor("color.end", GradientEndBox);

    /// <summary>
    /// Показывает палитру и записывает выбранный цвет в поле и в образец.
    /// Правка идёт через те же TextBox, что и при вводе руками, поэтому
    /// сохранение и обновление предпросмотра работают одинаково.
    /// </summary>
    private void ApplyPickedColor(string titleKey, TextBox box)
    {
        var picker = new ColorPickerWindow(I18n.Current[titleKey], box.Text)
        {
            Owner = this,
        };

        if (picker.ShowDialog() != true)
            return;

        // TextChanged сам запишет значение в настройки, обновит образец,
        // предпросмотр и сохранит файл — дублировать это здесь не нужно
        box.Text = picker.Selected;
    }

    /// <summary>
    /// Цвет изменился в поле ввода.
    ///
    /// Раньше обработчика не было, и значения цвета никуда не попадали:
    /// предпросмотр читал прямо из полей, а сохранялись старые числа из
    /// настроек — поэтому цвет возвращался при выходе из окна.
    /// </summary>
    private void OnColorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready)
            return;

        if (ReferenceEquals(sender, SolidColorBox))
        {
            _settings.SolidColor = ColorUtil.Normalize(SolidColorBox.Text, "#E9EDF5");
            SetSwatch(SolidSwatch, _settings.SolidColor);
        }
        else if (ReferenceEquals(sender, GradientStartBox))
        {
            _settings.GradientStart = ColorUtil.Normalize(GradientStartBox.Text, "#E9EDF5");
            SetSwatch(GradientStartSwatch, _settings.GradientStart);
        }
        else if (ReferenceEquals(sender, GradientEndBox))
        {
            _settings.GradientEnd = ColorUtil.Normalize(GradientEndBox.Text, "#C3CDE0");
            SetSwatch(GradientEndSwatch, _settings.GradientEnd);
        }
        else
        {
            return;
        }

        UpdateBackgroundPreview();
        UpdatePreview();
        Persist();
    }

    /// <summary>
    /// Уход с поля. Текст приводится к каноническому виду #RRGGBB, иначе
    /// в настройках осталось бы то, что набрал пользователь.
    /// </summary>
    private void OnColorCommitted(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, SolidColorBox))
            SolidColorBox.Text = _settings.SolidColor;
        else if (ReferenceEquals(sender, GradientStartBox))
            GradientStartBox.Text = _settings.GradientStart;
        else if (ReferenceEquals(sender, GradientEndBox))
            GradientEndBox.Text = _settings.GradientEnd;
    }

    private static void SetSwatch(Border swatch, string hex)
    {
        swatch.Background = new SolidColorBrush(ColorUtil.Parse(hex, "#E9EDF5"));
    }

    private void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;

        if (ReferenceEquals(sender, PaddingSlider)) _settings.Padding = Math.Round(e.NewValue);
        else if (ReferenceEquals(sender, BlurSlider)) _settings.ShadowBlur = Math.Round(e.NewValue);
        else if (ReferenceEquals(sender, OpacitySlider)) _settings.ShadowOpacity = Math.Round(e.NewValue) / 100.0;
        else if (ReferenceEquals(sender, OffsetSlider)) _settings.ShadowOffsetY = Math.Round(e.NewValue);

        UpdateLabels();
        UpdatePreview();
        Persist();
    }

    private void OnShadowChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;

        _settings.ShadowEnabled = ShadowCheck.IsChecked == true;
        UpdateLabels();
        UpdatePreview();
        Persist();
    }

    /// <summary>
    /// Срез заголовка снимаемого окна.
    ///
    /// Высота задаётся руками, а не вычисляется: у окон разная рамка, и у
    /// Electron-приложений своя панель внутри клиентской области. Предпросмотр
    /// показывает, сколько именно срезается, поэтому ошибиться можно только
    /// один раз — и сразу видно.
    /// </summary>
    private void OnTitleBarChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;

        _settings.HideTitleBar = HideTitleCheck.IsChecked == true;
        _settings.TitleBarMode = TitleManual.IsChecked == true
            ? TitleBarMode.Manual
            : TitleBarMode.Auto;
        _settings.TitleBarHeight = Math.Round(TitleBarSlider.Value);

        UpdateLabels();
        UpdatePreview();
        Persist();
    }

    /// <summary>
    /// Высота среза для предпросмотра, DIP.
    ///
    /// В автоматическом режиме показываем системный заголовок этой системы —
    /// ровно то, что будет срезано у обычного окна. У Electron-приложений
    /// среза не будет вовсе, но в предпросмотре окна нет, и показать это
    /// негде.
    /// </summary>
    private double PreviewTitleBarDip()
    {
        if (_settings.TitleBarMode == TitleBarMode.Manual)
            return _settings.TitleBarHeight;

        return Interop.Native.GetSystemCaptionHeight();
    }

    private void OnFlagChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;

        _settings.SaveToDisk = SaveCheck.IsChecked == true;
        _settings.RunAtStartup = StartupCheck.IsChecked == true;

        if (StartupCheck.IsChecked == true)
            StartupService.SetEnabled(true);

        Persist();
    }

    private void OnPickBackgroundImage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = I18n.Current["set.pickImage"],
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
        };

        if (dialog.ShowDialog(this) != true)
            return;

        _settings.BackgroundImagePath = dialog.FileName;
        _settings.Background = BackgroundKind.Image;
        BgImage.IsChecked = true;

        UpdateVisibility();
        UpdateBackgroundPreview();
        UpdatePreview();
        Persist();
    }

    /// <summary>
    /// Диалог задания сочетания.
    ///
    /// Экземпляр создаётся заново на каждый вызов: WPF разрешает ShowDialog()
    /// для окна ровно один раз, повторный вызов бросает
    /// InvalidOperationException. Поэтому держать диалог в поле нельзя —
    /// сочетание удалось бы задать только один раз за сеанс.
    ///
    /// Текущее сочетание передаётся в конструктор, а не присваивается
    /// свойством после создания: надпись строится в конструкторе, и
    /// присваивание уже не отразилось бы на экране.
    /// </summary>
    private void OnSetHotKey(object sender, RoutedEventArgs e)
    {
        var dialog = new HotKeyDialog(_settings.HotKey) { Owner = this };

        if (dialog.ShowDialog() != true)
            return;

        _settings.HotKey = dialog.PreparedGesture;
        HotKeyLabel.Text = _settings.HotKey;

        Persist();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Сохраняет настройки и сообщает результат. Применение хоткея идёт
    /// наружу через трей, потому что реестрирует его HotKeyService.
    /// </summary>
    private void Persist()
    {
        SettingsStore.Save(_settings);
        _tray.RaiseSettingsChanged();
    }
}
