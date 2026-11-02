using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MacShot.Interop;

namespace MacShot.UI;

/// <summary>
/// Полноэкранный оверлей выделения области: затемняет экран, по нажатию
/// начинает растягивать рамку, по отпусканию сообщает прямоугольник.
/// Esc отменяет в любой момент.
///
/// Механика та же, что у PickerOverlay: прозрачное слоёное окно не получает
/// события мыши (Windows ищет попадание по альфе), поэтому и курсор, и кнопки
/// опрашиваются через GetCursorPos/GetAsyncKeyState.
///
/// Координаты хранятся в физических пикселях — так их отдаёт GetCursorPos
/// и так их ждёт BitBlt в ScreenCapturer. В DIP экрана они переводятся
/// через PointFromScreen уже только для отрисовки, поэтому рамка ложится
/// точно при любом масштабе монитора.
/// </summary>
internal sealed class RegionOverlay : Window
{
    /// <summary>Меньше этого — клик, а не выделение: считаем отменой.</summary>
    private const int MinSidePx = 16;

    private readonly Canvas _canvas;
    private readonly Rectangle _dimTop, _dimBottom, _dimLeft, _dimRight;
    private readonly Border _selection;
    private readonly TextBlock _size;
    private readonly DispatcherTimer _timer;

    private bool _active;
    private bool _dragging;
    private bool _leftWasDown;
    private Native.POINT _anchor;

    /// <summary>Выделенный прямоугольник в физических пикселях экрана.</summary>
    public event Action<Native.RECT>? Picked;
    public event Action? Cancelled;

    public RegionOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Cursor = Cursors.Cross;
        ShowActivated = true;
        Title = "MacShotRegion";

        var dimBrush = new SolidColorBrush(Color.FromArgb(0x8C, 0x00, 0x00, 0x00));
        dimBrush.Freeze();

        _dimTop = MakeDim(dimBrush);
        _dimBottom = MakeDim(dimBrush);
        _dimLeft = MakeDim(dimBrush);
        _dimRight = MakeDim(dimBrush);

        _selection = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x97, 0xFF)),
            BorderThickness = new Thickness(2),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(2),
            Visibility = Visibility.Hidden,
            IsHitTestVisible = false,
        };

        // Подсказка «1234 × 567»: фон чуть темнее затемнения, чтобы читалась
        // и на светлом, и на тёмном содержимом
        var sizeChip = new SolidColorBrush(Color.FromArgb(0xE6, 0x18, 0x1C, 0x22));
        sizeChip.Freeze();

        _size = new TextBlock
        {
            Background = sizeChip,
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            Padding = new Thickness(8, 4, 8, 4),
            Visibility = Visibility.Hidden,
            IsHitTestVisible = false,
        };

        _canvas = new Canvas
        {
            ClipToBounds = true,
            // Alpha = 1 невидима, но нужна: без неё окно не считается
            // «попадаемым» для мыши (см. комментарий у PickerOverlay)
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
        };

        _canvas.Children.Add(_dimTop);
        _canvas.Children.Add(_dimBottom);
        _canvas.Children.Add(_dimLeft);
        _canvas.Children.Add(_dimRight);
        _canvas.Children.Add(_selection);
        _canvas.Children.Add(_size);

        Content = _canvas;

        _timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(25) };
        _timer.Tick += (_, _) => Poll();
    }

    private static Rectangle MakeDim(Brush brush) => new Rectangle
    {
        Fill = brush,
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false,
    };

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Как и у PickerOverlay, WS_EX_TRANSPARENT здесь нельзя: с ним
        // протяжка рамки уходила бы в окно под оверлеем и выделяла там
        // текст или элементы. Оверлей должен забирать ввод себе.
        var hwnd = new WindowInteropHelper(this).Handle;
        Native.AddExStyle(hwnd, Native.WS_EX_TOOLWINDOW);
    }

    /// <summary>Показывает оверлей на весь виртуальный экран.</summary>
    public void Begin()
    {
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        WindowState = WindowState.Normal;

        _active = false;
        _dragging = false;
        _leftWasDown = false;
        _anchor = default;

        _selection.Visibility = Visibility.Hidden;
        _size.Visibility = Visibility.Hidden;

        Show();
        Activate();

        SetDim(null);

        // Таймер стартуем сами: OnContentRendered приходит только на первом
        // показе окна (проверено на PickerOverlay)
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_active)
                return;

            _active = true;
            _timer.Start();
        }));
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // Подстраховка на случай, если событие придёт раньше BeginInvoke
        if (_active)
            return;

        _active = true;
        _timer.Start();
    }

    private void Poll()
    {
        if (!_active)
            return;

        if (IsKeyDown(Native.VK_ESCAPE))
        {
            Finish();
            Cancelled?.Invoke();
            return;
        }

        if (!Native.GetCursorPos(out var cursor))
            return;

        bool leftDown = IsKeyDown(Native.VK_LBUTTON);

        if (leftDown && !_leftWasDown && !_dragging)
        {
            // Начало растягивания: якорь и рамка живут в физических пикселях
            _dragging = true;
            _anchor = cursor;
            UpdateSelection(cursor);
        }
        else if (_dragging && leftDown)
        {
            UpdateSelection(cursor);
        }
        else if (_dragging && !leftDown)
        {
            // Отпускание кнопки завершает выделение
            Complete(cursor);
            return;
        }

        _leftWasDown = leftDown;
    }

    private void Complete(Native.POINT cursor)
    {
        var rect = Normalize(_anchor, cursor);

        // Оверлей прячем до захвата: иначе затемнение и рамка попали бы в снимок
        Finish();

        if (rect.Width >= MinSidePx && rect.Height >= MinSidePx)
            Picked?.Invoke(rect);
        else
            Cancelled?.Invoke();
    }

    private void UpdateSelection(Native.POINT cursor)
    {
        var rect = Normalize(_anchor, cursor);

        // Физика -> DIP окна: PointFromScreen учитывает масштаб монитора
        var from = PointFromScreen(new Point(rect.Left, rect.Top));
        var to = PointFromScreen(new Point(rect.Right, rect.Bottom));

        var dip = new Rect(from, to);

        _selection.Width = dip.Width;
        _selection.Height = dip.Height;
        Canvas.SetLeft(_selection, dip.Left);
        Canvas.SetTop(_selection, dip.Top);
        _selection.Visibility = Visibility.Visible;

        _size.Text = $"{rect.Width} × {rect.Height}";
        _size.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        // Чип сидит внутри рамки у правого нижнего угла
        double x = Math.Max(4, dip.Right - _size.DesiredSize.Width - 6);
        double y = Math.Max(4, dip.Bottom - _size.DesiredSize.Height - 6);

        Canvas.SetLeft(_size, x);
        Canvas.SetTop(_size, y);
        _size.Visibility = Visibility.Visible;

        SetDim(dip);
    }

    /// <summary>
    /// Затемняет всё, кроме выделенной области (null — затемнить весь экран,
    /// когда выделение ещё не началось). Дырка собирается из четырёх полос.
    /// </summary>
    private void SetDim(Rect? hole)
    {
        double w = _canvas.ActualWidth > 0 ? _canvas.ActualWidth : Width;
        double h = _canvas.ActualHeight > 0 ? _canvas.ActualHeight : Height;

        if (hole is not { } r)
        {
            Place(_dimTop, 0, 0, w, h);
            _dimBottom.Visibility = _dimLeft.Visibility = _dimRight.Visibility = Visibility.Collapsed;
            return;
        }

        Place(_dimTop, 0, 0, w, Math.Max(0, r.Top));
        Place(_dimBottom, 0, r.Bottom, w, Math.Max(0, h - r.Bottom));
        Place(_dimLeft, 0, r.Top, Math.Max(0, r.Left), r.Height);
        Place(_dimRight, r.Right, r.Top, Math.Max(0, w - r.Right), r.Height);
    }

    private static void Place(Rectangle rect, double x, double y, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            rect.Visibility = Visibility.Collapsed;
            return;
        }

        rect.Visibility = Visibility.Visible;
        rect.Width = width;
        rect.Height = height;
        Canvas.SetLeft(rect, x);
        Canvas.SetTop(rect, y);
    }

    private static Native.RECT Normalize(Native.POINT a, Native.POINT b)
    {
        return new Native.RECT
        {
            Left = Math.Min(a.X, b.X),
            Top = Math.Min(a.Y, b.Y),
            Right = Math.Max(a.X, b.X),
            Bottom = Math.Max(a.Y, b.Y),
        };
    }

    private void Finish()
    {
        _active = false;
        _dragging = false;
        _timer.Stop();
        Hide();
    }

    private static bool IsKeyDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
}
