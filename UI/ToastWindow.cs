using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Interop;
using MacShot.Interop;

namespace MacShot.UI;

/// <summary>
/// Плашка внизу экрана: короткое подтверждение «скриншот сделан».
///
/// Снимок уходит в буфер молча, и по шелесту трея не всегда понятно,
/// получилось ли. Плашка появляется после успешного снимка всех трёх
/// режимов и сама исчезает.
///
/// Окно неактивируемое и прозрачное для кликов: уведомление не должно
/// ни красть фокус, ни мешать следующим действиям — в том числе сразу
/// после снимка запускать следующий.
/// </summary>
internal sealed class ToastWindow : Window
{
    private const int VisibleMs = 2200;
    private const int FadeMs = 180;

    private readonly TextBlock _text;
    private readonly Border _pill;
    private readonly DispatcherTimer _hideTimer;
    private bool _busy;

    public ToastWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        // Точное имя из списка служебных в WindowFinder: сама плашка
        // не должна предлагаться для снимка
        Title = "MacShotToast";
        Opacity = 0;

        // Цвета фиксированные, как у системного HUD: плашка должна читаться
        // и на тёмном, и на светлом рабочем столе, и в обеих темах приложения
        _text = new TextBlock
        {
            FontSize = 13,
            Foreground = Brushes.White,
        };

        _pill = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x22, 0x22, 0x26)),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(18, 10, 18, 10),
            Child = _text,
        };

        Content = _pill;

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(VisibleMs) };
        _hideTimer.Tick += (_, _) => FadeOut();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var hwnd = new WindowInteropHelper(this).Handle;

        // TOOLWINDOW — не мигает в Alt-Tab; NOACTIVATE — не крадёт фокус;
        // TRANSPARENT — клики проходят сквозь: плашка ничего не блокирует
        Native.AddExStyle(hwnd,
            Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE | Native.WS_EX_TRANSPARENT);
    }

    /// <summary>Показывает сообщение и убирает плашку сама.</summary>
    public void ShowMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        _text.Text = message;

        if (_busy)
        {
            // Плашка уже видна: обновляем текст и перезапускаем таймер,
            // не гася и не поднимая окно заново
            Position();
            _hideTimer.Stop();
            _hideTimer.Start();
            return;
        }

        _busy = true;

        // Показ при нулевой прозрачности: размеры окна считаются при
        // показе, позицию ставим сразу после этого — появления не видно
        Show();

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            Position();
            _hideTimer.Start();
            FadeTo(1);
        });
    }

    /// <summary>Внизу по центру основного монитора, над панелью задач.</summary>
    private void Position()
    {
        var area = SystemParameters.WorkArea;

        Left = area.Left + Math.Max(0, (area.Width - ActualWidth) / 2);
        Top = area.Bottom - ActualHeight - 56;
    }

    private void FadeOut()
    {
        _hideTimer.Stop();
        FadeTo(0, HideAfterFade);
    }

    private void HideAfterFade()
    {
        Hide();
        _busy = false;
    }

    private void FadeTo(double opacity, Action? onDone = null)
    {
        var animation = new DoubleAnimation
        {
            To = opacity,
            Duration = TimeSpan.FromMilliseconds(FadeMs),
        };

        if (onDone is { } done)
            animation.Completed += (_, _) => done();

        BeginAnimation(OpacityProperty, animation);
    }
}
