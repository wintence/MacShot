using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using MacShot.Services;
using MacShot.Settings;
using MacShot.Themes;
using MacShot.UI;

using MacShot.Lang;

namespace MacShot;

public partial class App : Application
{
    private TrayIcon? _tray;
    private CaptureController? _controller;
    private SettingsWindow? _settingsWindow;
    private AppSettings _settings = new();
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Режим проверки: снять активное окно и сохранить результат в файл.
        // Нужен, чтобы убедиться, что захват и оформление работают, не кликая
        // по экрану. Использование: MacShot.exe --capture out.png
        for (int i = 0; i < e.Args.Length - 1; i++)
        {
            if (e.Args[i] == "--capture")
            {
                RunSelfTest(e.Args[i + 1]);
                return;
            }
        }

        // Режим проверки оверлея: сразу открыть выбор окна, без горячей клавиши.
        if (e.Args.Contains("--pick"))
        {
            StartNormally();
            BeginCapture();
            return;
        }

        // Режим проверки настроек: открыть окно без обращения к трею.
        if (e.Args.Contains("--settings"))
        {
            StartNormally();
            ShowSettings();
            return;
        }

        StartNormally();
    }

    /// <summary>Обычный запуск: трей, горячая клавиша, защита от второго экземпляра.</summary>
    private void StartNormally()
    {
        // Мьютекс проверяем через WaitOne, а не через конструктор.
        // new Mutex(true, name) не берёт владение, если мьютекс уже существует,
        // поэтому после аварийного завершения прошлого экземпляра объект
        // остаётся в системе брошенным, и все последующие запуски
        // получают isFirst == false навсегда.
        _singleInstance = new Mutex(false, "MacShot.SingleInstance");

        bool isFirst;
        try
        {
            isFirst = _singleInstance.WaitOne(0, false);
        }
        catch (AbandonedMutexException)
        {
            // Предыдущий экземпляр не завершился штатно.
            // WaitOne при этом уже передал нам владение — продолжаем.
            isFirst = true;
        }

        if (!isFirst)
        {
            MessageBox.Show(I18n.Current["app.alreadyRunning"],
                "MacShot", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;

        _settings = SettingsStore.Load();

        // Язык применяем до создания трея и окон — иначе первый кадр
        // покажется на старом языке
        I18n.Use(_settings.Language);

        // С темой так же: кисти кладутся в ресурсы приложения, и окно,
        // созданное раньше, взяло бы уже несуществующую палитру
        Theme.Apply(_settings.Theme);

        _tray = new TrayIcon();
        _tray.CaptureRequested += BeginCapture;
        _tray.SettingsRequested += ShowSettings;
        _tray.ExitRequested += ExitApp;

        _controller = new CaptureController(_settings, _tray);
        _tray.SettingsChanged += () => _controller.RegisterHotKey();

        _controller.RegisterHotKey();

        // Главного окна нет: приложение живёт только в трее
        NotifyStartup();
    }

    /// <summary>
    /// Снимает активное окно и сохраняет оформленный результат.
    /// Нужно, чтобы проверить захват и рендер без участия человека.
    /// </summary>
    private void RunSelfTest(string outputPath)
    {
        try
        {
            // Берём самое большое подходящее окно: в момент запуска на переднем
            // плане может оказаться собственное служебное окно приложения,
            // у которого нулевой размер.
            var windows = Capture.WindowFinder.Enumerate();
            var target = windows
                .OrderByDescending(w => (long)w.Bounds.Width * w.Bounds.Height)
                .FirstOrDefault();

            if (target.Handle == IntPtr.Zero)
            {
                File.WriteAllText(outputPath + ".txt", "Подходящих окон не найдено.");
                Shutdown();
                return;
            }

            var settings = SettingsStore.Load();
            var capture = Capture.WindowCapturer.Capture(target.Handle);
            var scale = Interop.Native.GetScale(target.Handle);

            var prepared = Render.ShotRenderer.PrepareWindow(
                (WriteableBitmap)capture.Bitmap, settings, scale, target.Handle);

            var shot = Render.ShotRenderer.Render(
                prepared, settings, scale,
                Services.CaptureController.AppNameFor(target.Handle));

            // Снимок получился — значит, приложение тоже попало в список
            // своих высот, ровно так же, как при съёмке по горячей клавише
            Services.CaptureController.RememberAppTitleBar(settings, target.Handle, scale);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(shot));
            using (var stream = File.Create(outputPath))
                encoder.Save(stream);

            var report = $"OK{Environment.NewLine}" +
                         $"Окно: {target.Title}{Environment.NewLine}" +
                         $"Метод: {capture.Method}{Environment.NewLine}" +
                         $"Размер окна: {capture.Bounds.Width}x{capture.Bounds.Height}{Environment.NewLine}" +
                         $"Масштаб: {scale}{Environment.NewLine}" +
                         $"Итог: {shot.PixelWidth}x{shot.PixelHeight}";

            File.WriteAllText(outputPath + ".txt", report);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(outputPath + ".txt", "ОШИБКА: " + ex); } catch { }
        }
        finally
        {
            Shutdown();
        }
    }

    private void NotifyStartup() => AppLog.Write("startup");

    private void BeginCapture()
    {
        try
        {
            _controller?.BeginCapture();
        }
        catch (Exception ex)
        {
            _tray?.Notify("MacShot", ex.Message, true);
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, _tray!) { WindowStartupLocation = WindowStartupLocation.CenterScreen };
        _settingsWindow.LanguageChanged += ReopenSettingsInNewLanguage;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Язык сменили в окне настроек. Строки разметки читаются через
    /// x:Static при разборе XAML, поэтому на лету они не обновляются —
    /// окно закрывается и открывается заново уже на новом языке.
    /// </summary>
    private void ReopenSettingsInNewLanguage()
    {
        I18n.Use(_settings.Language);
        _tray?.ApplyLanguage();

        var old = _settingsWindow;
        _settingsWindow = null;
        old?.Close();

        ShowSettings();
    }

    private void ExitApp()
    {
        SettingsStore.Save(_settings);
        _controller?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        Shutdown();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var log = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MacShot", "MacShot.log");

            Directory.CreateDirectory(Path.GetDirectoryName(log)!);
            File.AppendAllText(log, $"[{DateTime.Now:HH:mm:ss:fff}] {e.Exception}{Environment.NewLine}");
        }
        catch
        {
            // Если и лог недоступен — просто показываем ошибку
        }

        _tray?.Notify("MacShot", I18n.Format("notify.captureFailed", e.Exception.Message), true);
        e.Handled = true;
    }
}
