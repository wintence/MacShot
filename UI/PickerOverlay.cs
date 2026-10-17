using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MacShot.Capture;
using MacShot.Interop;

namespace MacShot.UI;

/// <summary>
/// Полноэкранный оверлей режима выбора окна: затемняет экран, подсвечивает
/// окно под курсором и сообщает о выборе кликом. Esc отменяет.
///
/// Почему состояние мыши читается опросом, а не событиями окна:
/// оверлей с AllowsTransparency — это слоёное окно, а Windows ищет его попадание
/// мышью попиксельно по альфе. У прозрачного оверлея альфа нулевая, поэтому
/// WM_MOUSEMOVE до WPF не доходит. Возникает замкнутый круг: событие нужно,
/// чтобы нарисовать подсветку, но подсветка нужна, чтобы событие дошло.
/// Опрос GetCursorPos/GetAsyncKeyState этот круг разрывает.
/// </summary>
internal sealed class PickerOverlay : Window
{
    private readonly Canvas _canvas;
    private readonly Rectangle _dimTop, _dimBottom, _dimLeft, _dimRight;
    private readonly Border _highlight;
    private readonly DispatcherTimer _timer;

    private WindowInfo? _current;
    private bool _active;
    private bool _leftWasDown;

    public event Action<WindowInfo>? Picked;
    public event Action? Cancelled;

    public PickerOverlay()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        Cursor = Cursors.Cross;
        ShowActivated = true;
        Title = "MacShotPicker";

        var dimBrush = new SolidColorBrush(Color.FromArgb(0x8C, 0x00, 0x00, 0x00));
        dimBrush.Freeze();

        _dimTop = MakeDim(dimBrush);
        _dimBottom = MakeDim(dimBrush);
        _dimLeft = MakeDim(dimBrush);
        _dimRight = MakeDim(dimBrush);

        _highlight = new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x4C, 0x6E, 0xF5)),
            BorderThickness = new Thickness(2.5),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            Visibility = Visibility.Hidden,
            IsHitTestVisible = false,
        };

        _canvas = new Canvas
        {
            ClipToBounds = true,
            // Alpha = 1 невидима, но нужна: без неё окно не считается
            // «попадаемым» для мыши и подсветка никогда не появится
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
        };

        _canvas.Children.Add(_dimTop);
        _canvas.Children.Add(_dimBottom);
        _canvas.Children.Add(_dimLeft);
        _canvas.Children.Add(_dimRight);
        _canvas.Children.Add(_highlight);

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
        var hwnd = new WindowInteropHelper(this).Handle;

        // WS_EX_TRANSPARENT обязателен: без него WindowFromPoint возвращает сам
        // оверлей, и окно под курсором перестаёт находиться.
        //
        // WS_EX_NOACTIVATE ставить нельзя — вместе с AllowsTransparency он
        // полностью отключает ввод мыши.
        Native.AddExStyle(hwnd, Native.WS_EX_TOOLWINDOW | Native.WS_EX_TRANSPARENT);
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
        _leftWasDown = false;
        _current = null;
        _highlight.Visibility = Visibility.Hidden;
        SetDim(null);

        Show();
        Activate();

        // Таймер запускаем сами, а не из OnContentRendered: тот событие
        // срабатывает только при самом первом показе окна. Со второго раза
        // оверлей появлялся, но не реагировал ни на клик, ни на Esc.
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_active)
                return;

            _active = true;
            _timer.Start();
            UpdateHighlight();
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
        UpdateHighlight();
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

        UpdateHighlight();

        // Клик ловим по нажатию — так работает и выбор с перетаскиванием курсора
        bool leftDown = IsKeyDown(Native.VK_LBUTTON);
        if (leftDown && !_leftWasDown)
        {
            _leftWasDown = true;

            if (_current is { } target)
            {
                Finish();
                Picked?.Invoke(target);
            }
            else
            {
                // Кликнули мимо окна — отменяем
                Finish();
                Cancelled?.Invoke();
            }
        }
        else if (!leftDown)
        {
            _leftWasDown = false;
        }
    }

    private void UpdateHighlight()
    {
        if (!Native.GetCursorPos(out var cursor))
            return;

        var window = WindowFinder.FindAt(cursor.X, cursor.Y);
        if (window is null)
        {
            _current = null;
            _highlight.Visibility = Visibility.Hidden;
            SetDim(null);
            return;
        }

        if (_current?.Handle == window.Value.Handle)
            return;

        _current = window;

        var origin = PointToScreen(new Point(0, 0));
        var bounds = window.Value.Bounds;

        double left = bounds.Left - origin.X;
        double top = bounds.Top - origin.Y;

        _highlight.Width = bounds.Width;
        _highlight.Height = bounds.Height;
        Canvas.SetLeft(_highlight, left);
        Canvas.SetTop(_highlight, top);
        _highlight.Visibility = Visibility.Visible;

        SetDim(new Rect(left, top, bounds.Width, bounds.Height));
    }

    /// <summary>
    /// Затемняет всё, кроме указанного прямоугольника. Дырка собирается из
    /// четырёх полос — так края получаются ровными, без работы с масками.
    /// </summary>
    private void SetDim(Rect? hole)
    {
        if (hole is not { } r)
        {
            _dimTop.Visibility = _dimBottom.Visibility =
                _dimLeft.Visibility = _dimRight.Visibility = Visibility.Collapsed;
            return;
        }

        double w = _canvas.ActualWidth > 0 ? _canvas.ActualWidth : Width;
        double h = _canvas.ActualHeight > 0 ? _canvas.ActualHeight : Height;

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

    private void Finish()
    {
        _active = false;
        _timer.Stop();
        Hide();
    }

    private static bool IsKeyDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
}
