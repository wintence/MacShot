using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MacShot.Capture;
using MacShot.Interop;
using MacShot.Render;
using MacShot.Settings;
using MacShot.UI;

using MacShot.Lang;

namespace MacShot.Services;

/// <summary>
/// Сценарий целиком: горячая клавиша или пункт меню -> выбор окна ->
/// снимок -> буфер обмена и, по желанию, файл.
/// </summary>
internal sealed class CaptureController : IDisposable
{
    private readonly PickerOverlay _overlay = new();
    private readonly HotKeyService _hotKey = new();
    private readonly AppSettings _settings;
    private readonly TrayIcon _tray;

    /// <summary>Последний готовый снимок — его показывает окно настроек.</summary>
    public BitmapSource? LastShot { get; private set; }

    public CaptureController(AppSettings settings, TrayIcon tray)
    {
        _settings = settings;
        _tray = tray;

        _overlay.Picked += OnPicked;
        _overlay.Cancelled += () => _tray.Notify(L.AppName, I18n.Current["notify.cancelled"], false);
    }

    public void RegisterHotKey()
    {
        bool ok = _hotKey.Register(_settings.HotKey, BeginCapture);

        AppLog.Write($"hotkey {_settings.HotKey}: " +
                     (ok ? "registered" : "FAILED - " + _hotKey.LastError));

        if (ok)
        {
            _tray.SetHotKey(_settings.HotKey);
            return;
        }

        _tray.SetHotKey(null);
        _tray.Notify(L.AppName, _hotKey.LastError ?? I18n.Current["notify.hotkeyFailed"], true);
    }

    public void BeginCapture()
    {
        _overlay.Begin();
    }

    private void OnPicked(WindowInfo window)
    {
        try
        {
            TakeShot(window);
        }
        catch (CaptureException ex)
        {
            _tray.Notify(L.AppName, ex.Message, true);
        }
        catch (Exception ex)
        {
            _tray.Notify(L.AppName, I18n.Format("notify.captureFailed", ex.Message), true);
        }
    }

    private void TakeShot(WindowInfo window)
    {
        // Окно могло закрыться или свернуться, пока пользователь выбирал
        if (WindowFinder.HasClosed(window.Handle))
        {
            _tray.Notify(L.AppName, I18n.Current["notify.closed"], true);
            return;
        }

        if (Native.IsIconic(window.Handle))
        {
            _tray.Notify(L.AppName, I18n.Current["notify.minimized"], true);
            return;
        }

        var capture = WindowCapturer.Capture(window.Handle);

        // Масштаб берём у самого окна: при переносе между экранами с разным
        // DPI координаты и размеры иначе будут неверными.
        var scale = Native.GetScale(window.Handle);

        var prepared = ShotRenderer.PrepareWindow(
            (WriteableBitmap)capture.Bitmap, _settings, scale, window.Handle);

        // Имя приложения ищется до рендера: подпись над окном — часть кадра,
        // и без имени её просто не будет.
        string appName = AppNameFor(window.Handle);
        _settings.LastAppName = appName;

        var shot = ShotRenderer.Render(prepared, _settings, scale, appName);

        LastShot = shot;

        RememberAppTitleBar(_settings, window.Handle, scale);

        bool copied = ClipboardService.TrySetImage(shot);

        string savedPath = string.Empty;
        if (_settings.SaveToDisk)
        {
            savedPath = ClipboardService.SaveToPictures(
                shot, window.Title, _settings.Format, _settings.JpegQuality);
        }

        BuildStatus(window.Title, capture.Method, copied, savedPath);
    }

    /// <summary>
    /// Запоминает приложение, чтобы его можно было настроить отдельно.
    ///
    /// Запись появляется сама после первого снимка этого окна и сразу с той
    /// высотой, которая на самом деле применилась. Иначе список в настройках
    /// был бы пустым, и настраивать было бы нечего — выбирать приложения
    /// вручную нечем.
    ///
    /// Существующую запись не трогаем: её уже могли отредактировать, и
    /// переписывать каждым снимком значило бы забывать правку.
    /// </summary>
    public static void RememberAppTitleBar(AppSettings settings, IntPtr hwnd, double scale)
    {
        if (!settings.HideTitleBar)
            return;

        string app = Native.GetAppPath(hwnd);

        if (string.IsNullOrEmpty(app) || settings.TitleBarOverrides.ContainsKey(app))
            return;

        settings.TitleBarOverrides[app] =
            Render.ShotRenderer.MeasureTitleBar(settings, scale, hwnd);

        SettingsStore.Save(settings);
    }

    /// <summary>
    /// Название приложения для подписи над окном.
    ///
    /// Берём описание файла, а не имя процесса: у Telegram это
    /// «Telegram Desktop», а не «Telegram.exe». Если описания нет, годится
    /// название продукта, и лишь в последнюю очередь — имя файла.
    /// </summary>
    internal static string AppNameFor(IntPtr hwnd)
    {
        try
        {
            string path = Native.GetAppPath(hwnd);

            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return string.Empty;

            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);

            string name = info.FileDescription ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                name = info.ProductName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                name = System.IO.Path.GetFileNameWithoutExtension(path);

            return name.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private string BuildStatus(string title, CaptureMethod method, bool copied, string savedPath)
    {
        var parts = new System.Collections.Generic.List<string> { title };

        parts.Add(copied ? I18n.Current["status.copied"] : I18n.Current["status.clipboardBusy"]);

        if (savedPath.Length > 0)
            parts.Add(I18n.Current["status.saved"]);

        // Метод захвата полезно знать: если окно перекрыто другим и у него
        // сработал резервный путь, пользователь увидит это по качеству снимка.
        if (method == CaptureMethod.ScreenCopy)
            parts.Add(I18n.Current["status.screenCopy"]);

        return string.Join(" — ", parts);
    }

    public void Dispose()
    {
        _hotKey.Dispose();
    }
}
