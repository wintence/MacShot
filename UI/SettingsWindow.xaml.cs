using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using MacShot.Render;
using MacShot.Services;
using MacShot.Settings;

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

    private void Load()
    {
        _ready = false;

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

        SaveCheck.IsChecked = _settings.SaveToDisk;
        StartupCheck.IsChecked = _settings.RunAtStartup;

        HotKeyLabel.Text = _settings.HotKey;

        LangRu.IsChecked = _settings.Language == UiLanguage.Russian;
        LangEn.IsChecked = _settings.Language == UiLanguage.English;

        // Образцы повторяют сохранённый цвет, а не значение из разметки
        SetSwatch(SolidSwatch, _settings.SolidColor);
        SetSwatch(GradientStartSwatch, _settings.GradientStart);
        SetSwatch(GradientEndSwatch, _settings.GradientEnd);

        _ready = true;

        UpdateVisibility();
        UpdateLabels();
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
            ? "Файл не выбран"
            : System.IO.Path.GetFileName(_settings.BackgroundImagePath);
    }

    private void UpdateLabels()
    {
        PaddingLabel.Text = I18n.Format("set.padding", PaddingSlider.Value);
        AngleLabel.Text = I18n.Format("set.angle", AngleSlider.Value);
        BlurLabel.Text = I18n.Format("set.blur", BlurSlider.Value);
        OpacityLabel.Text = I18n.Format("set.opacity", OpacitySlider.Value);
        OffsetLabel.Text = I18n.Format("set.offset", OffsetSlider.Value);

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
    private void UpdatePreview()
    {
        Brush brush;

        switch (_settings.Background)
        {
            case BackgroundKind.Solid:
                brush = new SolidColorBrush(ColorUtil.Parse(SolidColorBox.Text, "#E9EDF5"));
                break;

            case BackgroundKind.Image when FileOk(_settings.BackgroundImagePath):
                brush = TryImageBrush(_settings.BackgroundImagePath);
                break;

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
                brush = gradient;
                break;
        }

        BackgroundPreview.Background = brush;
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
        UpdatePreview();
        Persist();
    }

    private void OnAngleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;

        _settings.GradientAngle = e.NewValue;
        UpdateLabels();
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
    /// <summary>
    /// Цвет изменился в поле ввода.
    ///
    /// Раньше обработчика не было, и значения цвета никуда не попадали:
    /// предпросмотр читал прямо из полей, а сохранялись старые числа из
    /// настроек — поэтому цвет возвращался при выходе из окна.
    /// </summary>
    private void OnColorTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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

        UpdatePreview();
        Persist();
    }

    /// <summary>
    /// Уход с поля. Текст приводится к каноническому виду #RRGGBB, иначе
    /// в настройках осталось бы то, что набрал пользователь.
    /// </summary>
    private void OnColorCommitted(object sender, System.Windows.RoutedEventArgs e)
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

    private void ApplyPickedColor(string titleKey, TextBox box)
    {
        var picker = new ColorPickerWindow(I18n.Current[titleKey], box.Text);

        picker.Owner = this;
        if (picker.ShowDialog() != true)
            return;

        // TextChanged сам запишет значение в настройки, обновит образец,
        // предпросмотр и сохранит файл — дублировать это здесь не нужно
        box.Text = picker.Selected;
    }

    private void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready)
            return;

        if (ReferenceEquals(sender, PaddingSlider)) _settings.Padding = e.NewValue;
        else if (ReferenceEquals(sender, BlurSlider)) _settings.ShadowBlur = e.NewValue;
        else if (ReferenceEquals(sender, OpacitySlider)) _settings.ShadowOpacity = e.NewValue / 100;
        else if (ReferenceEquals(sender, OffsetSlider)) _settings.ShadowOffsetY = e.NewValue;

        UpdateLabels();
        Persist();
    }

    private void OnShadowChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready)
            return;

        _settings.ShadowEnabled = ShadowCheck.IsChecked == true;
        UpdateLabels();
        Persist();
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
            Title = "Выберите фон",
            Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.webp",
        };

        if (dialog.ShowDialog(this) != true)
            return;

        _settings.BackgroundImagePath = dialog.FileName;
        _settings.Background = BackgroundKind.Image;
        BgImage.IsChecked = true;

        UpdateVisibility();
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
