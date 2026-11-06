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
    private readonly Image _frozenImage;
    private readonly Rectangle _dimTop, _dimBottom, _dimLeft, _dimRight;
    private readonly Border _highlight;
    private readonly DispatcherTimer _timer;

    private WindowInfo? _current;
    private bool _active;
    private bool _leftWasDown;
    private IntPtr _hwnd;
    private FrozenScreen? _frozen;

    public event Action<WindowInfo, FrozenScreen>? Picked;
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
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0x97, 0xFF)),
            BorderThickness = new Thickness(2.5),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(8),
            Visibility = Visibility.Hidden,
            IsHitTestVisible = false,
        };

        // Замороженный кадр экрана: нижний слой оверлея. Пока пользователь
        // выбирает окно, живой экран продолжает двигаться, но виден только
        // этот статичный снимок — экран «остановлен».
        _frozenImage = new Image
        {
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            Visibility = Visibility.Hidden,
        };

        _canvas = new Canvas
        {
            ClipToBounds = true,
            // Alpha = 1 невидима, но нужна: без неё окно не считается
            // «попадаемым» для мыши и подсветка никогда не появится
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
        };

        _canvas.Children.Add(_frozenImage);
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
        _hwnd = new WindowInteropHelper(this).Handle;

        // WS_EX_TRANSPARENT здесь ставить нельзя: с ним клики и протяжки
        // проваливаются в окно под оверлеем. На сайте это выделяет текст,
        // в списках — элементы, в редакторах — фрагменты. Оверлей обязан
        // забирать ввод себе: тогда под ним ничего не происходит.
        //
        // Из-за этого WindowFromPoint возвращает сам оверлей, поэтому окно
        // под курсором ищется по Z-порядку начиная со следующего окна
        // (см. WindowFinder.FindAt).
        //
        // WS_EX_NOACTIVATE ставить нельзя — вместе с AllowsTransparency он
        // полностью отключает ввод мыши.
        Native.AddExStyle(_hwnd, Native.WS_EX_TOOLWINDOW);
    }

    /// <summary>
    /// Показывает оверлей на весь виртуальный экран. Кадр задаётся заранее:
    /// снимается до показа оверлея, чтобы в него не попало затемнение.
    /// </summary>
    public void Begin(FrozenScreen frozen)
    {
        _frozen = frozen;
        _frozenImage.Source = frozen.Bitmap;

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

            PlaceFrozenImage();
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

        PlaceFrozenImage();
        _active = true;
        _timer.Start();
        UpdateHighlight();
    }

    /// <summary>
    /// Кладёт замороженный кадр под затемнение. Физические пиксели кадра
    /// переводятся в DIP канвы через PointFromScreen — картинка ложится
    /// точно при любом масштабе монитора.
    /// </summary>
    private void PlaceFrozenImage()
    {
        if (_frozen is not { } frozen)
            return;

        if (_frozenImage.Source is null)
            _frozenImage.Source = frozen.Bitmap;

        var topLeft = PointFromScreen(new Point(frozen.Frame.Left, frozen.Frame.Top));
        var bottomRight = PointFromScreen(new Point(frozen.Frame.Right, frozen.Frame.Bottom));

        // Повторный Begin пересчитывает размеры заново: окно переиспользуется
        _frozenImage.Width = Math.Max(1, bottomRight.X - topLeft.X);
        _frozenImage.Height = Math.Max(1, bottomRight.Y - topLeft.Y);
        Canvas.SetLeft(_frozenImage, topLeft.X);
        Canvas.SetTop(_frozenImage, topLeft.Y);
        _frozenImage.Visibility = Visibility.Visible;
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

            if (_current is { } target && _frozen is { } frozen)
            {
                Finish();
                Picked?.Invoke(target, frozen);
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

        var window = WindowFinder.FindAt(cursor.X, cursor.Y, _hwnd);
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

        // Физические пиксели окна -> DIP канвы: PointFromScreen учитывает
        // масштаб монитора, поэтому подсветка ложится точно под окно и на
        // 100%, и на 150–200%. Так подсветка совпадает и с замороженным кадром.
        var bounds = window.Value.Bounds;
        var from = PointFromScreen(new Point(bounds.Left, bounds.Top));
        var to = PointFromScreen(new Point(bounds.Right, bounds.Bottom));

        var dip = new Rect(from, to);

        _highlight.Width = dip.Width;
        _highlight.Height = dip.Height;
        Canvas.SetLeft(_highlight, dip.Left);
        Canvas.SetTop(_highlight, dip.Top);
        _highlight.Visibility = Visibility.Visible;

        SetDim(dip);
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

        // Кадр больше не нужен: замороженный снимок экрана занимает
        // десятки мегабайт — держим его только пока оверлей открыт
        _frozenImage.Source = null;
        _frozenImage.Visibility = Visibility.Hidden;
        _frozen = null;

        Hide();
    }

    private static bool IsKeyDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
}
