using System;
using System.IO;

namespace MacShot.Services;

/// <summary>
/// Небольшой журнал запуска и важных событий. Нужен для диагностики:
/// без него ошибки горячей клавиши и захвата негде увидеть.
/// </summary>
internal static class AppLog
{
    private static readonly object Gate = new();

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MacShot", "MacShot.log");

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                File.AppendAllText(
                    Path,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Журнал — необязателен, его отсутствие не должно ломать работу
        }
    }
}
