using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MacShot.Settings;

using MacShot.Lang;

namespace MacShot.UI;

/// <summary>
/// Выбор цвета из палитры.
///
/// Это фон под окном, поэтому палитра подобрана под такие задачи:
/// много светлых нейтральных тонов (тень на них читается лучше всего),
/// несколько сдержанных подсветок и несколько насыщенных — на выбор.
///
/// Палитра дополнена системным диалогом: шестнадцать свотчей не покрывают
/// всех желаний, а свой цвет всё равно нужен иногда.
/// </summary>
internal sealed class ColorPickerWindow : Window
{
    /// <summary>Готовая палитра: светлые нейтральные, подсветки, насыщенные, тёмные.</summary>
    private static readonly string[] Palette =
    {
        // Светлые нейтральные — на них тень от окна видна лучше всего
        "#FFFFFF", "#F6F6F8", "#F0F2F5", "#E9EDF5", "#E3E6EA", "#D5DAE0",
        "#C3CDE0", "#B4BDCC", "#A0A8B4", "#8A8F99",
        // Сдержанные подсветки
        "#DCE6F5", "#E5E0F5", "#F5E6DC", "#DCF0E5", "#F0DCEA", "#E2F0F5",
        // Насыщенные
        "#4C8DFF", "#5B8DEF", "#7C4DFF", "#00BFA5", "#4CAF50",
        "#FFB020", "#FF7043", "#EF5350", "#EC407A", "#8BC34A",
        // Тёмные
        "#2A2A33", "#3A3A47", "#1C1C22",
    };

    private static readonly Brush Bg = Frozen("#17171B");
    private static readonly Brush Card = Frozen("#212128");
    private static readonly Brush Line = Frozen("#2E2E38");
    private static readonly Brush Text = Frozen("#E9E9EF");
    private static readonly Brush Muted = Frozen("#8E8E99");
    private static readonly Brush Accent = Frozen("#4C8DFF");

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush(ColorUtil.Parse(hex, "#000000"));
        brush.Freeze();
        return brush;
    }

    /// <summary>Выбранный цвет в виде #RRGGBB.</summary>
    public string Selected { get; private set; }

    /// <summary>Образец выбранного цвета. Нужен обработчикам ниже.</summary>
    private readonly Border _preview = new();

    public ColorPickerWindow(string title, string current)
    {
        Selected = ColorUtil.Normalize(current, "#E9EDF5");

        Title = title;
        Width = 372;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Bg;
        FontFamily = new FontFamily("Segoe UI, system-ui");
        ShowInTaskbar = false;

        var root = new StackPanel { Margin = new Thickness(20) };

        root.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Text,
            Margin = new Thickness(0, 0, 0, 4),
        });

        var hint = new TextBlock
        {
            Text = I18n.Current["color.hint"],
            FontSize = 11,
            Foreground = Muted,
            Margin = new Thickness(0, 0, 0, 16),
        };
        root.Children.Add(hint);

        // --- Палитра ---
        var grid = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var hex in Palette)
            grid.Children.Add(MakeSwatch(hex));
        root.Children.Add(grid);

        // --- Текущий цвет и кнопки ---
        root.Children.Add(new Separator { Margin = new Thickness(0, 18, 0, 16) });

        _preview.Height = 34;
        _preview.CornerRadius = new CornerRadius(8);
        _preview.BorderBrush = Line;
        _preview.BorderThickness = new Thickness(1);
        _preview.Background = Frozen(Selected);
        _preview.Margin = new Thickness(0, 0, 0, 16);
        root.Children.Add(_preview);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var cancel = MakeButton(I18n.Current["btn.cancel"], flat: true);
        cancel.Click += (_, _) => { DialogResult = false; Close(); };

        var custom = MakeButton(I18n.Current["btn.custom"], flat: true);
        custom.Click += (_, _) => PickCustom();

        var ok = MakeButton(I18n.Current["btn.done"], flat: false);
        ok.IsDefault = true;
        ok.Click += (_, _) => { DialogResult = true; Close(); };

        buttons.Children.Add(cancel);
        buttons.Children.Add(custom);
        buttons.Children.Add(ok);
        root.Children.Add(buttons);

        Content = root;
    }

    private Border MakeSwatch(string hex)
    {
        var color = ColorUtil.Parse(hex, "#000000");

        // Обводка у тёмных свотчей, иначе на тёмной теме они сливаются с фоном
        bool dark = color.R + color.G + color.B < 260;

        var swatch = new Border
        {
            Width = 36,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 8),
            CornerRadius = new CornerRadius(6),
            Background = Frozen(hex),
            BorderBrush = dark ? Frozen("#4A4A58") : Line,
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            ToolTip = hex,
        };

        var mark = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(2),
            BorderBrush = Frozen("#FFFFFF"),
            Visibility = string.Equals(hex, Selected, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var inner = new Grid();
        inner.Children.Add(mark);
        swatch.Child = inner;

        swatch.MouseLeftButtonUp += (_, _) =>
        {
            Selected = hex.ToUpperInvariant();
            _preview.Background = Frozen(Selected);
            foreach (var child in ((WrapPanel)swatch.Parent).Children)
                if (child is Border b && b.Child is Grid g && g.Children.Count == 1 && g.Children[0] is Border b2)
                    b2.Visibility = b == swatch ? Visibility.Visible : Visibility.Collapsed;
        };

        return swatch;
    }

    private void PickCustom()
    {
        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            AnyColor = true,
            Color = System.Drawing.ColorTranslator.FromHtml(Selected),
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            return;

        var c = dialog.Color;
        Selected = string.Format(
            CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);

        _preview.Background = Frozen(Selected);
    }

    private static Button MakeButton(string text, bool flat)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(16, 8, 16, 8),
            Margin = new Thickness(8, 0, 0, 0),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(0),
            Foreground = flat ? Text : Frozen("#0B1220"),
            Background = flat ? Frozen("#2C2C36") : Accent,
        };

        return button;
    }
}