using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacShot.Themes;

namespace MacShot.UI;

/// <summary>
/// Свой заголовок окна для окон, которые собираются кодом.
///
/// Системную строку заголовка нельзя перекрасить в цвет темы: на машине
/// с Windhawk её перерисовывает мод, и подсказка DWM пропадает. Поэтому
/// заголовок рисует программа — он всегда того же цвета, что и окно.
///
/// Окно настроек собирает такой же заголовок в разметке, там он отдельный:
/// у него три кружка, а у диалогов один — закрыть.
/// </summary>
internal static class TitleBar
{
    /// <summary>Заголовок с иконкой, названием и кружком закрытия.</summary>
    public static FrameworkElement Build(Window window, string title)
    {
        var bar = new Grid
        {
            Height = 34,
            Background = Theme.Palette.Of("Bg"),
        };

        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        };

        left.Children.Add(new Image
        {
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Source = AppIcon(),
        });

        left.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12,
            Foreground = Theme.Palette.Of("Muted"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        });

        Grid.SetColumn(left, 0);
        bar.Children.Add(left);

        var close = new Button
        {
            Style = (Style)Application.Current.TryFindResource("TrafficButton"),
            Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x5F, 0x57)),
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = Cursors.Hand,
        };

        close.Click += (_, _) => window.Close();
        Grid.SetColumn(close, 1);
        bar.Children.Add(close);

        bar.MouseLeftButtonDown += (_, e) =>
        {
            // Кружок закрытия не должен двигать окно
            if (e.OriginalSource is DependencyObject source && HasButton(source))
                return;

            try
            {
                window.DragMove();
            }
            catch (InvalidOperationException)
            {
                // Кнопку отпустили раньше, чем перетаскивание началось
            }
        };

        return bar;
    }

    /// <summary>Иконка программы: та же, что и в трее и в заголовке окна.</summary>
    public static ImageSource? AppIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return null;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
                return null;

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16));

            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase)
                return true;

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
