using System;
using System.IO;
using Microsoft.Win32;

namespace MacShot.Services;

/// <summary>
/// Автозапуск через ветку реестра пользователя HKCU.
/// Такой способ не требует прав администратора и не трогает планировщик.
/// </summary>
internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MacShot";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
                return key?.GetValue(ValueName) is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    public static void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null)
                return;

            if (enabled)
                key.SetValue(ValueName, ExecutablePath());
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Нет доступа к реестру — просто не включаем автозапуск
        }
    }

    private static string ExecutablePath()
    {
        var path = Environment.ProcessPath;
        return !string.IsNullOrWhiteSpace(path) ? $"\"{path}\"" : "MacShot.exe";
    }
}
