using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shell;
using MacShot.Services;
using MacShot.Themes;

using MacShot.Lang;

namespace MacShot.UI;

/// <summary>
/// Ввод сочетания клавиш: ловит нажатие и показывает его текстом.
/// </summary>
public sealed class HotKeyDialog : Window
{
    /// <summary>Надпись с текущим сочетанием — обновляется по мере набора.</summary>
    private readonly TextBlock _caption;

    /// <summary>Выбранное сочетание в виде "Ctrl+Shift+8".</summary>
    public string PreparedGesture { get; set; }

    /// <summary>
    /// Текущее сочетание передаётся сразу: надпись строится в конструкторе,
    /// и присваивание свойству после создания уже не отразилось бы на экране.
    /// </summary>
    public HotKeyDialog(string initialGesture)
    {
        PreparedGesture = Services.HotKeyService.Normalize(initialGesture);
        Title = I18n.Current["hk.title"];
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        // Диалог создаётся заново на каждый вызов, поэтому цвета читаются
        // один раз — и те, какая тема выбрана сейчас
        Background = Theme.Palette.Of("Bg");
        Foreground = Theme.Palette.Of("Text");
        FontFamily = new FontFamily("Segoe UI, system-ui");

        // Свой заголовок вместо системного: иначе у тёмного окна остаётся
        // светлая полоса, и она спорит с окном настроек.
        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            GlassFrameThickness = new Thickness(0),
            ResizeBorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var titleBar = TitleBar.Build(this, Title);
        Grid.SetRow(titleBar, 0);
        root.Children.Add(titleBar);

        var content = new StackPanel { Margin = new Thickness(24, 18, 24, 24) };

        content.Children.Add(new TextBlock
        {
            Text = I18n.Current["hk.prompt"],
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Theme.Palette.Of("Text"),
        });

        content.Children.Add(new TextBlock
        {
            Text = I18n.Current["hk.hint"],
            FontSize = 11,
            Foreground = Theme.Palette.Of("Muted"),
            Margin = new Thickness(0, 4, 0, 14),
        });

        _caption = new TextBlock
        {
            Text = Services.HotKeyService.Friendly(PreparedGesture),
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 18),
            Foreground = Theme.Palette.Of("Text"),
        };
        // caption живёт внутри box, в root его второй раз добавлять нельзя:
        // у элемента может быть только один логический родитель
        var box = new Border
        {
            Background = Theme.Palette.Of("Card"),
            BorderBrush = Theme.Palette.Of("CardLine"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 14, 16, 14),
            Child = _caption,
        };
        content.Children.Add(box);
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0),
        };

        var cancel = MakeButton(I18n.Current["btn.cancel"], flat: true);
        cancel.Click += (_, _) => { DialogResult = false; Close(); };

        var ok = MakeButton(I18n.Current["btn.done"], flat: false);
        ok.IsDefault = true;
        ok.Click += (_, _) => { DialogResult = true; Close(); };

        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        content.Children.Add(buttons);

        Grid.SetRow(content, 1);
        root.Children.Add(content);

        Content = root;

        Loaded += (_, _) => Keyboard.Focus(this);
    }

    private static Button MakeButton(string text, bool flat)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(18, 10, 18, 10),
            Margin = new Thickness(8, 0, 0, 0),
            Cursor = Cursors.Hand,
            // Стиль берём из общих ресурсов приложения: иначе кнопка
            // нарисовалась бы системная, и в тёмном окне светлый прямоугольник
            // выглядел бы чужеродно.
            Style = flat
                ? (Style)Application.Current.FindResource("Button")
                : (Style)Application.Current.FindResource("PrimaryButton"),
        };

        return button;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
            e.Handled = true;
            return;
        }

        // Пока модификатор не отпущен, сочетание не записано
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return;
        }

        if (!Services.HotKeyService.TryParse(CurrentModifiers() + "+" + e.Key,
                out _, out _))
        {
            return;
        }

        PreparedGesture = Services.HotKeyService.Normalize(CurrentModifiers() + "+" + e.Key);

        // Показываем набранное сочетание в рамке
        _caption.Text = Services.HotKeyService.Friendly(PreparedGesture);

        e.Handled = true;
    }

    /// <summary>Формирует текущий набор модификаторов.</summary>
    private static string CurrentModifiers()
    {
        var parts = new System.Collections.Generic.List<string>(4);

        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            parts.Add("Ctrl");
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            parts.Add("Alt");
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            parts.Add("Shift");
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            parts.Add("Win");

        return string.Join("+", parts);
    }
}
