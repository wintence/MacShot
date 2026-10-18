using System;
using System.Collections.Generic;
using System.Globalization;

namespace MacShot.Lang;

/// <summary>Язык интерфейса. Пока поддерживаются только два.</summary>
internal enum UiLanguage
{
    Russian = 0,
    English = 1,
}

/// <summary>
/// Переводы интерфейса.
///
/// Отдельная сборка ресурсов (.resx) here не нужна: строк мало, а
/// XAML всё равно пришлось бы тянуть через x:Static. Словарь рядом
/// с ключами читается проще, чем спрятанные в .resources файлы.
///
/// Ключи хранятся вплотную к переводум, чтобы пропущенную строку
/// было видно сразу, а не в рантайме.
/// </summary>
internal sealed class I18n
{
    private readonly Dictionary<UiLanguage, Dictionary<string, string>> _table;
    private readonly UiLanguage _language;

    private I18n(UiLanguage language)
    {
        _language = language;
        _table = Build();
    }

    public static I18n Current { get; private set; } = new(UiLanguage.English);

    /// <summary>Сработало после смены языка — окна перечитывают строки.</summary>
    public static event Action? Changed;

    public static void Use(UiLanguage language)
    {
        if (Current._language == language)
            return;

        Current = new I18n(language);
        Changed?.Invoke();
    }

    public static UiLanguage CurrentLanguage => Current._language;

    /// <summary>
    /// Перевод по ключу. Неизвестный ключ возвращается самому ключу —
    /// на экране будет видно, что строка забыта, а не молча пропадёт.
    /// </summary>
    public string this[string key]
    {
        get
        {
            if (_table.TryGetValue(_language, out var dict) && dict.TryGetValue(key, out var text))
                return text;

            if (_table.TryGetValue(UiLanguage.Russian, out var ru) && ru.TryGetValue(key, out var fallback))
                return fallback;

            return key;
        }
    }

    /// <summary>Перевод с подстановкой {0}, {1}…</summary>
    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Current[key], args);

    // ---------------- Словарь ----------------

    private static Dictionary<UiLanguage, Dictionary<string, string>> Build()
    {
        var ru = new Dictionary<string, string>
        {
            // Трей
            ["tray.capture"] = "Снять окно",
            ["tray.settings"] = "Настройки…",
            ["tray.exit"] = "Выход",
            ["tray.hotkey"] = "Горячая клавиша: {0}",
            ["tray.hotkeyNone"] = "Горячая клавиша не назначена",
            ["tray.app"] = "MacShot",

            // Уведомления
            ["notify.cancelled"] = "Выбор отменён.",
            ["notify.closed"] = "Окно закрылось до того, как снимок сделан.",
            ["notify.minimized"] = "Окно свёрнуто — разверните его и повторите.",
            ["notify.captureFailed"] = "Не удалось снять окно: {0}",
            ["notify.hotkeyBusy"] = "Сочетание уже занято другой программой.",
            ["notify.hotkeyInvalid"] = "Не удалось разобрать сочетание клавиш.",
            ["notify.hotkeyFailed"] = "Не удалось занять сочетание клавиш.",

            // Итог захвата
            ["status.copied"] = "скопировано в буфер",
            ["status.clipboardBusy"] = "буфер занят",
            ["status.saved"] = "сохранено в «Изображения»",
            ["status.screenCopy"] = "(окно было перекрыто, взят кадр с экрана)",

            // Ошибки захвата
            ["cap.notFound"] = "Окно не найдено.",
            ["cap.invisible"] = "Окно закрылось или стало невидимым.",
            ["cap.minimized"] = "Окно свёрнуто — разверните его и повторите.",
            ["cap.noArea"] = "У окна нет видимой области.",
            ["cap.imageFailed"] = "Не удалось получить изображение окна: {0}",
            ["cap.printFalse"] = "PrintWindow вернул false ({0})",
            ["cap.printTimeout"] = "PrintWindow не ответил за {0} мс",
            ["cap.zeroBounds"] = "у окна нулевые границы",

            // Диалог горячей клавиши
            ["hk.title"] = "Задать горячую клавишу",
            ["hk.prompt"] = "Нажмите сочетание клавиш",
            ["hk.hint"] = "Нужен хотя бы один модификатор: Ctrl, Alt, Shift или Win",

            // Палитра
            ["color.solid"] = "Цвет фона",
            ["color.start"] = "Начало градиента",
            ["color.end"] = "Конец градиента",
            ["color.text"] = "Цвет надписи",
            ["color.shadow"] = "Цвет тени надписи",
            ["color.hint"] = "Цвет фона под окном",

            // Общие кнопки
            ["btn.cancel"] = "Отмена",
            ["btn.done"] = "Готово",
            ["btn.custom"] = "Свой цвет…",

            // Окно настроек
            ["set.title"] = "MacShot — настройки",
            ["set.language"] = "ЯЗЫК",
            ["set.languageRu"] = "Русский",
            ["set.languageEn"] = "English",
            ["set.theme"] = "ОФОРМЛЕНИЕ",
            ["set.themeDark"] = "Тёмная",
            ["set.themeLight"] = "Светлая",
            ["set.bg"] = "ФОН",
            ["set.bgSolid"] = "Однотонный цвет",
            ["set.bgGradient"] = "Градиент",
            ["set.bgImage"] = "Своё изображение",
            ["set.color"] = "ЦВЕТ",
            ["set.start"] = "НАЧАЛО",
            ["set.end"] = "КОНЕЦ",
            ["set.angle"] = "НАПРАВЛЕНИЕ: {0}°",
            ["set.pickImage"] = "Выбрать изображение…",
            ["set.noFile"] = "Файл не выбран",
            ["set.paletteTip"] = "Выбрать из палитры",
            ["set.window"] = "ОКНО",
            ["set.padding"] = "ОТСТУП: {0}",
            ["set.shadow"] = "Тень",
            ["set.blur"] = "РАЗМЫТИЕ: {0}",
            ["set.opacity"] = "ПЛОТНОСТЬ: {0}%",
            ["set.offset"] = "СМЕЩЕНИЕ: {0}",
            ["set.hideTitleBar"] = "Скрыть заголовок окна",
            ["set.titleAuto"] = "Определять автоматически",
            ["set.titleManual"] = "Задать вручную",
            ["set.titleBarHeight"] = "ВЫСОТА ЗАГОЛОВКА: {0}",
            ["set.hideTitleBarHint"] = "В автоматическом режиме срезается ровно системный заголовок, а у окон без него не срезается ничего. Ручной режим — если у приложения своя панель сверху.",
            ["set.perApp"] = "СВОЯ ВЫСОТА ДЛЯ ПРИЛОЖЕНИЙ",
            ["set.addApp"] = "Добавить приложение",
            ["set.perAppEmpty"] = "Список пуст. Добавьте приложение кнопкой выше — или снимите его окно при включённом срезке, и строка появится сама.",
            ["set.hotkey"] = "ГОРЯЧАЯ КЛАВИША",
            ["set.setHotkey"] = "Задать…",
            ["set.storage"] = "СОХРАНЕНИЕ И ЗАПУСК",
            ["set.saveCopy"] = "Сохранять копию в «Изображения»",
            ["set.saveHint"] = "Дополнительно к буферу обмена",
            ["set.startup"] = "Запускать при входе в Windows",
            ["set.startupHint"] = "Запись в HKCU\\...\\Run, права администратора не нужны",
            ["set.hideSettings"] = "Скрыть настройки",
            ["set.showSettings"] = "Показать настройки",
            ["set.repository"] = "github.com/wintence/MacShot",

            // Водяной знак
            ["set.watermark"] = "ВОДЯНОЙ ЗНАК",
            ["set.addText"] = "Добавить надпись",
            ["set.addPicture"] = "Добавить картинку",
            ["set.pickPicture"] = "Выбрать картинку…",
            ["set.markPicture"] = "Картинка",
            ["set.markDefaultText"] = "Текст",
            ["set.btnRemove"] = "Удалить",
            ["set.markEmpty"] = "Пока ничего не добавлено. Добавьте надпись или картинку — они появятся на предпросмотре слева.",
            ["set.markShadow"] = "Тень у надписи",
            ["set.placement"] = "ПОЛОЖЕНИЕ",
            ["set.font"] = "ШРИФТ",
            ["set.bold"] = "Жирный",
            ["set.italic"] = "Курсив",
            ["set.markFontSize"] = "КЕГЛЬ: {0}",
            ["set.markOpacity"] = "НЕПРОЗРАЧНОСТЬ: {0}%",
            ["set.markMargin"] = "ОТСТУП ОТ КРАЯ: {0}",
            ["set.pictureHeight"] = "ВЫСОТА: {0}",
            ["set.pictureOpacity"] = "НЕПРОЗРАЧНОСТЬ: {0}%",
            ["set.pictureMargin"] = "ОТСТУП ОТ КРАЯ: {0}",
            ["set.shadowBlur"] = "РАЗМЫТИЕ ТЕНИ: {0}",
            ["set.shadowOpacity"] = "ПЛОТНОСТЬ ТЕНИ: {0}%",
            ["set.preview"] = "ПРЕДПРОСМОТР",

            // Прочее
            ["app.alreadyRunning"] = "MacShot уже запущен — значок в трее.",
        };

        var en = new Dictionary<string, string>
        {
            ["tray.capture"] = "Capture window",
            ["tray.settings"] = "Settings…",
            ["tray.exit"] = "Exit",
            ["tray.hotkey"] = "Hotkey: {0}",
            ["tray.hotkeyNone"] = "No hotkey assigned",
            ["tray.app"] = "MacShot",

            ["notify.cancelled"] = "Selection cancelled.",
            ["notify.closed"] = "The window closed before the screenshot was taken.",
            ["notify.minimized"] = "The window is minimized — restore it and try again.",
            ["notify.captureFailed"] = "Could not capture the window: {0}",
            ["notify.hotkeyBusy"] = "This combination is already taken by another program.",
            ["notify.hotkeyInvalid"] = "Could not parse the key combination.",
            ["notify.hotkeyFailed"] = "Could not assign the key combination.",

            ["status.copied"] = "copied to clipboard",
            ["status.clipboardBusy"] = "clipboard busy",
            ["status.saved"] = "saved to Pictures",
            ["status.screenCopy"] = "(window was covered, taken from screen)",

            ["cap.notFound"] = "Window not found.",
            ["cap.invisible"] = "The window closed or became invisible.",
            ["cap.minimized"] = "The window is minimized — restore it and try again.",
            ["cap.noArea"] = "The window has no visible area.",
            ["cap.imageFailed"] = "Could not get the window image: {0}",
            ["cap.printFalse"] = "PrintWindow returned false ({0})",
            ["cap.printTimeout"] = "PrintWindow did not respond within {0} ms",
            ["cap.zeroBounds"] = "the window has zero bounds",

            ["hk.title"] = "Set hotkey",
            ["hk.prompt"] = "Press a key combination",
            ["hk.hint"] = "At least one modifier is required: Ctrl, Alt, Shift or Win",

            ["color.solid"] = "Background color",
            ["color.start"] = "Gradient start",
            ["color.end"] = "Gradient end",
            ["color.text"] = "Text color",
            ["color.shadow"] = "Text shadow color",
            ["color.hint"] = "Background behind the window",

            ["btn.cancel"] = "Cancel",
            ["btn.done"] = "Done",
            ["btn.custom"] = "Custom color…",

            ["set.title"] = "MacShot — Settings",
            ["set.language"] = "LANGUAGE",
            ["set.languageRu"] = "Русский",
            ["set.languageEn"] = "English",
            ["set.theme"] = "APPEARANCE",
            ["set.themeDark"] = "Dark",
            ["set.themeLight"] = "Light",
            ["set.bg"] = "BACKGROUND",
            ["set.bgSolid"] = "Solid color",
            ["set.bgGradient"] = "Gradient",
            ["set.bgImage"] = "Custom image",
            ["set.color"] = "COLOR",
            ["set.start"] = "START",
            ["set.end"] = "END",
            ["set.angle"] = "ANGLE: {0}°",
            ["set.pickImage"] = "Choose image…",
            ["set.noFile"] = "No file selected",
            ["set.paletteTip"] = "Pick from palette",
            ["set.window"] = "WINDOW",
            ["set.padding"] = "PADDING: {0}",
            ["set.shadow"] = "Shadow",
            ["set.blur"] = "BLUR: {0}",
            ["set.opacity"] = "OPACITY: {0}%",
            ["set.offset"] = "OFFSET: {0}",
            ["set.hideTitleBar"] = "Hide the window title bar",
            ["set.titleAuto"] = "Detect automatically",
            ["set.titleManual"] = "Set manually",
            ["set.titleBarHeight"] = "TITLE BAR HEIGHT: {0}",
            ["set.hideTitleBarHint"] = "In automatic mode exactly the system title bar is cut, and windows without one lose nothing. Use manual mode if the app draws its own bar on top.",
            ["set.perApp"] = "HEIGHT PER APPLICATION",
            ["set.addApp"] = "Add application",
            ["set.perAppEmpty"] = "The list is empty. Add an app with the button above, or take a screenshot of it with the cut turned on and the row will appear by itself.",
            ["set.hotkey"] = "HOTKEY",
            ["set.setHotkey"] = "Change…",
            ["set.storage"] = "STORAGE AND STARTUP",
            ["set.saveCopy"] = "Save a copy to Pictures",
            ["set.saveHint"] = "In addition to the clipboard",
            ["set.startup"] = "Start when I sign in to Windows",
            ["set.startupHint"] = "Written to HKCU\\...\\Run, no admin rights needed",
            ["set.hideSettings"] = "Hide settings",
            ["set.showSettings"] = "Show settings",
            ["set.repository"] = "github.com/wintence/MacShot",

            ["set.watermark"] = "WATERMARK",
            ["set.addText"] = "Add text",
            ["set.addPicture"] = "Add picture",
            ["set.pickPicture"] = "Choose picture…",
            ["set.markText"] = "Text",
            ["set.markPicture"] = "Picture",
            ["set.markDefaultText"] = "Text",
            ["set.btnRemove"] = "Remove",
            ["set.markEmpty"] = "Nothing added yet. Add a text or a picture — they show up in the preview on the left.",
            ["set.markShadow"] = "Text shadow",
            ["set.placement"] = "POSITION",
            ["set.font"] = "FONT",
            ["set.bold"] = "Bold",
            ["set.italic"] = "Italic",
            ["set.markFontSize"] = "FONT SIZE: {0}",
            ["set.markOpacity"] = "OPACITY: {0}%",
            ["set.markMargin"] = "MARGIN: {0}",
            ["set.pictureHeight"] = "HEIGHT: {0}",
            ["set.pictureOpacity"] = "OPACITY: {0}%",
            ["set.pictureMargin"] = "MARGIN: {0}",
            ["set.shadowBlur"] = "SHADOW BLUR: {0}",
            ["set.shadowOpacity"] = "SHADOW OPACITY: {0}%",
            ["set.preview"] = "PREVIEW",

            ["app.alreadyRunning"] = "MacShot is already running — see the tray icon.",
        };

        return new Dictionary<UiLanguage, Dictionary<string, string>>
        {
            [UiLanguage.Russian] = ru,
            [UiLanguage.English] = en,
        };
    }
}