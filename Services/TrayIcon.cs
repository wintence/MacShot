using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using MacShot.Settings;

using MacShot.Lang;

namespace MacShot.Services;

/// <summary>
/// Значок в системном трее: единственная точка входа, основного окна нет.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _hotKeyItem;
    private readonly ToolStripMenuItem _captureItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _exitItem;
    private string? _activeGesture;

    public event Action? CaptureRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    /// <summary>Сигнал «настройки изменились» — приложение перерегистрирует хоткей.</summary>
    public event Action? SettingsChanged;

    /// <summary>Позволяет окну настроек попросить перерегистрацию горячей клавиши.</summary>
    public void RaiseSettingsChanged() => SettingsChanged?.Invoke();

    public TrayIcon()
    {
        _hotKeyItem = new ToolStripMenuItem { Enabled = false };

        _captureItem = new ToolStripMenuItem(L.TrayCapture, null, (_, _) => CaptureRequested?.Invoke());
        _settingsItem = new ToolStripMenuItem(L.TraySettings, null, (_, _) => SettingsRequested?.Invoke());
        _exitItem = new ToolStripMenuItem(L.TrayExit, null, (_, _) => ExitRequested?.Invoke());

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_captureItem);
        _menu.Items.Add(_hotKeyItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(_exitItem);

        _icon = new NotifyIcon
        {
            Text = L.AppName,
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = _menu,
        };

        _icon.DoubleClick += (_, _) => CaptureRequested?.Invoke();
    }

    /// <summary>Обновляет тексты меню после смены языка.</summary>
    public void ApplyLanguage()
    {
        _captureItem.Text = L.TrayCapture;
        _settingsItem.Text = L.TraySettings;
        _exitItem.Text = L.TrayExit;
        _icon.Text = L.AppName;
        SetHotKey(_activeGesture);
    }

    public void SetHotKey(string? gesture)
    {
        _activeGesture = gesture;

        _hotKeyItem.Text = string.IsNullOrWhiteSpace(gesture)
            ? I18n.Current["tray.hotkeyNone"]
            : I18n.Format("tray.hotkey", gesture);
    }

    /// <summary>Всплывающее уведомление — для ошибок горячей клавиши.</summary>
    public void Notify(string title, string message, bool isError)
    {
        try
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText = message;
            _icon.BalloonTipIcon = isError ? ToolTipIcon.Error : ToolTipIcon.Info;
            _icon.ShowBalloonTip(4000);
        }
        catch
        {
            // Уведомление — необязательная роскошь
        }
    }

    private static System.Drawing.Icon LoadIcon()
    {
        // Сначала встроенный ресурс: в нём сразу все размеры,
        // поэтому значок остаётся чётким на любом масштабе экрана.
        try
        {
            var uri = new Uri("pack://application:,,,/Resources/MacShotTray.ico");

            if (System.Windows.Application.GetResourceStream(uri) is { Stream: var stream })
            {
                using var buffer = new System.IO.MemoryStream();
                stream.CopyTo(buffer);
                buffer.Position = 0;

                // Clone обязателен: исходный Icon держит поток,
                // а поток закроется вместе с using.
                using var loaded = new System.Drawing.Icon(buffer, SystemInformation.SmallIconSize);
                return (System.Drawing.Icon)loaded.Clone();
            }
        }
        catch
        {
            // Падаем на значок из самого .exe
        }

        try
        {
            // AppContext.BaseDirectory, а не Assembly.Location: в однофайловой
            // сборке Location возвращает пустую строку, и путь к .exe
            // получился бы неверным.
            var path = System.IO.Path.Combine(
                AppContext.BaseDirectory, "MacShot.exe");

            if (System.IO.File.Exists(path))
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
                if (icon is not null)
                    return icon;
            }
        }
        catch
        {
            // Падаем на иконку по умолчанию
        }

        return System.Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
