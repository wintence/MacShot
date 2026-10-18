using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using MacShot.Settings;

namespace MacShot.Render;

/// <summary>
/// Рисует водяной знак поверх готового кадра.
///
/// Один и тот же класс используется и при съёмке, и в предпросмотре
/// настроек — иначе предпросмоток врал бы: показывал одно, а на кадре
/// получалось другое.
///
/// Слой строится на Grid, а не на Canvas: только Grid выполняет выравнивание
/// дочерних элементов. На Canvas пришлось бы считать координаты руками, и
/// любая мелкая правка в размерах текста сдвигала бы знак.
/// </summary>
internal static class WatermarkRenderer
{
    /// <summary>Зазор между элементами, попавшими в одну позицию, DIP.</summary>
    private const double ItemSpacing = 8;

    /// <summary>
    /// Слой со знаком под размер <paramref name="width"/> x <paramref name="height"/>.
    /// Если рисовать нечего, возвращается пустой слой той же размерности —
    /// так вызывающему коду не нужно проверять результат на null.
    /// </summary>
    public static FrameworkElement Build(
        Watermark? mark, double width, double height, bool interactive = false)
    {
        var grid = new Grid
        {
            Width = width,
            Height = height,
            // Знак ничего не перехватывает: это часть готовой картинки,
            // а не окно, которому можно навести мышь. В предпросмотре
            // наоборот — перехват нужен для перетаскивания.
            IsHitTestVisible = interactive,
        };

        if (mark is null)
            return grid;

        // Ключ группировки — позиция. Надпись и картинка, выбранные в одну и ту же
        // точку, попадают в одну группу и встают рядом, вместо того чтобы лежать
        // друг на друге. Порядок групп зафиксирован, иначе перерисовка
        // складывала бы элементы в случайный порядок.
        var groups = mark.Marks
            .Where(m => m.IsRenderable)
            .GroupBy(m => m.Placement)
            .OrderBy(g => (int)g.Key);

        foreach (var group in groups)
        {
            // Ссылка на первый знак группы: по ней предпросмотр поймёт, что
            // тянут, если схватили строку целиком, а не отдельный элемент
            Mark? head = null;

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalFor(group.Key),
                VerticalAlignment = VerticalFor(group.Key),
            };

            bool any = false;

            foreach (var item in group)
            {
                var element = item.Kind == MarkKind.Text
                    ? BuildText(item)
                    : BuildPicture(item);

                if (element is null)
                    continue;

                head ??= item;

                // Отступ задаём каждому элементу отдельно. Иначе пришлось бы
                // выбирать, чей отступ у группы, и правка второго элемента
                // в паре не давала бы видимого результата.
                element.Margin = Margins(item);
                element.Tag = item;
                row.Children.Add(element);
                any = true;
            }

            if (any)
            {
                // Tag ставим после обхода: head наполняется в цикле,
                // а строке нужен уже готовый элемент
                row.Tag = head!;
                grid.Children.Add(row);
            }
        }

        return grid;
    }

    private static FrameworkElement? BuildText(Mark mark)
    {
        var block = new TextBlock
        {
            Text = mark.Text,
            FontSize = mark.Size,
            FontFamily = new FontFamily(FamilyOrFallback(mark.FontFamily)),
            FontWeight = mark.Bold ? FontWeights.Bold : FontWeights.Normal,
            FontStyle = mark.Italic ? FontStyles.Italic : FontStyles.Normal,
            Foreground = new SolidColorBrush(ColorUtil.Parse(mark.Color, "#FFFFFF")),
            Opacity = mark.Opacity,
            // Текст не должен переноситься: перенос ломает расчёт отступа
            // и превращает надпись в блок неизвестной ширины.
            TextWrapping = TextWrapping.NoWrap,
            SnapsToDevicePixels = true,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (mark.Shadow)
        {
            // Эффект, а не вторая копия текста: он повторяет мягкую тень окна
            // и не требует синхронизировать два набора свойств.
            block.Effect = new DropShadowEffect
            {
                BlurRadius = Math.Max(1, mark.ShadowBlur),
                ShadowDepth = Math.Abs(mark.ShadowOffsetY),
                Direction = mark.ShadowOffsetY >= 0 ? 270 : 90,
                Opacity = mark.ShadowOpacity,
                Color = ColorUtil.Parse(mark.ShadowColor, "#000000"),
            };
        }

        return block;
    }

    private static FrameworkElement? BuildPicture(Mark mark)
    {
        BitmapSource source;

        try
        {
            // Кэш обязателен: один и тот же файл попадает в кадр много раз
            // подряд, а BitmapImage без кэша держит файл открытым и не даёт
            // его перезаписать, пока работает приложение.
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(mark.Path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            source = bitmap;
        }
        catch
        {
            // Файл удалён, битый или недоступен. Молча пропускаем картинку:
            // снимок всё равно полезен, а падать из-за логотипа нельзя.
            return null;
        }

        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
            return null;

        // Ширину держим по пропорции: задана высота, вторая сторона считается
        double scale = mark.Size / source.PixelHeight;

        return new Image
        {
            Source = source,
            Width = source.PixelWidth * scale,
            Height = mark.Size,
            Opacity = mark.Opacity,
            Stretch = Stretch.Fill,
            SnapsToDevicePixels = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>
    /// Отступ от края кадра — с той стороны, к которой элемент прижат.
    /// У противоположной стороны отступа нет, иначе содержимое сместилось
    /// бы внутрь кадра вдвое дальше, чем задано.
    ///
    /// Справа добавляется зазор до соседа: элементы одной позиции стоят
    /// в ряд и не должны слипаться.
    /// </summary>
    private static Thickness Margins(Mark mark)
    {
        bool atLeft = HorizontalFor(mark.Placement) == HorizontalAlignment.Left;
        bool atRight = HorizontalFor(mark.Placement) == HorizontalAlignment.Right;
        bool atTop = VerticalFor(mark.Placement) == VerticalAlignment.Top;
        bool atBottom = VerticalFor(mark.Placement) == VerticalAlignment.Bottom;

        return new Thickness(
            left: atLeft ? mark.Margin : 0,
            top: atTop ? mark.Margin : 0,
            // У прижатого к правому краю элемента зазор справа заменяет отступ
            right: atRight ? mark.Margin + ItemSpacing : ItemSpacing,
            bottom: atBottom ? mark.Margin : 0);
    }

    private static HorizontalAlignment HorizontalFor(Placement p) => p switch
    {
        Placement.TopLeft or Placement.MiddleLeft or Placement.BottomLeft =>
            HorizontalAlignment.Left,
        Placement.TopCenter or Placement.Center or Placement.BottomCenter =>
            HorizontalAlignment.Center,
        _ => HorizontalAlignment.Right,
    };

    private static VerticalAlignment VerticalFor(Placement p) => p switch
    {
        Placement.TopLeft or Placement.TopCenter or Placement.TopRight =>
            VerticalAlignment.Top,
        Placement.MiddleLeft or Placement.Center or Placement.MiddleRight =>
            VerticalAlignment.Center,
        _ => VerticalAlignment.Bottom,
    };

    /// <summary>
    /// Шрифт из настроек может отсутствовать в системе — тогда WPF уронил бы
    /// весь кадр молча, без внятной ошибки. Подставляем Segoe UI вместо пустого.
    /// </summary>
    private static string FamilyOrFallback(string? family) =>
        string.IsNullOrWhiteSpace(family) ? "Segoe UI" : family;
}
