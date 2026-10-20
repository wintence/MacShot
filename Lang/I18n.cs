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
            ["set.search"] = "Поиск",
            ["set.noResults"] = "Ничего не найдено",
            ["set.nav.background"] = "Фон",
            ["set.nav.window"] = "Окно",
            ["set.nav.marks"] = "Знаки",
            ["set.nav.general"] = "Общие",
            ["set.language"] = "Язык",
            ["set.languageRu"] = "Русский",
            ["set.languageEn"] = "English",
            ["set.theme"] = "Оформление",
            ["set.themeDark"] = "Тёмная",
            ["set.themeLight"] = "Светлая",
            ["set.bg"] = "Фон",
            ["set.bgSolid"] = "Однотонный цвет",
            ["set.bgGradient"] = "Градиент",
            ["set.bgImage"] = "Своё изображение",
            ["set.color"] = "Цвет",
            ["set.start"] = "Начало",
            ["set.end"] = "Конец",
            ["set.angle"] = "Направление: {0}°",
            ["set.pickImage"] = "Выбрать изображение…",
            ["set.noFile"] = "Файл не выбран",
            ["set.paletteTip"] = "Выбрать из палитры",
            ["set.window"] = "Окно",
            ["set.padding"] = "Отступ: {0}",
            ["set.shadow"] = "Тень",
            ["set.blur"] = "Размытие: {0}",
            ["set.opacity"] = "Плотность: {0}%",
            ["set.offset"] = "Смещение: {0}",
            ["set.titleCard"] = "Заголовок окна",
            ["set.hideTitleBar"] = "Срезать заголовок",
            ["set.titleAuto"] = "Определять автоматически",
            ["set.titleManual"] = "Задать вручную",
            ["set.titleBarHeight"] = "Высота: {0}",
            ["set.hideTitleBarHint"] = "В автоматическом режиме срезается ровно системный заголовок, а у окон без него не срезается ничего. Ручной режим — если у приложения своя панель сверху.",
            ["set.perApp"] = "Своя высота для приложений",
            ["set.addApp"] = "Добавить",
            ["set.perAppEmpty"] = "Список пуст. Добавьте приложение кнопкой выше — или снимите его окно при включённом срезке, и строка появится сама.",
            ["set.hotkey"] = "Горячая клавиша",
            ["set.setHotkey"] = "Изменить…",
            ["set.storage"] = "Сохранение и запуск",
            ["set.saveCopy"] = "Сохранять копию в «Изображения»",
            ["set.startup"] = "Запускать при входе в Windows",
            ["set.hideSettings"] = "Скрыть настройки",
            ["set.showSettings"] = "Показать настройки",
            ["set.repository"] = "github.com/wintence/MacShot",

            // Водяной знак
            ["set.addText"] = "Добавить текст",
            ["set.addPicture"] = "Добавить картинку",
            ["set.pickPicture"] = "Выбрать картинку…",
            ["set.markText"] = "Надпись",
            ["set.markPicture"] = "Картинка",
            ["set.markDefaultText"] = "Текст",
            ["set.btnRemove"] = "Удалить",
            ["set.markShadow"] = "Тень у надписи",
            ["set.placement"] = "Положение",
            ["set.font"] = "Шрифт",
            ["set.bold"] = "Жирный",
            ["set.italic"] = "Курсив",
            ["set.markFontSize"] = "Кегль: {0}",
            ["set.markOpacity"] = "Непрозрачность: {0}%",
            ["set.markMargin"] = "Отступ от края: {0}",
            ["set.pictureHeight"] = "Высота: {0}",
            ["set.pictureOpacity"] = "Непрозрачность: {0}%",
            ["set.pictureMargin"] = "Отступ от края: {0}",
            ["set.shadowBlur"] = "Размытие тени: {0}",
            ["set.shadowOpacity"] = "Плотность тени: {0}%",
            ["set.preview"] = "Предпросмотр",

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
            ["set.search"] = "Search",
            ["set.noResults"] = "Nothing found",
            ["set.nav.background"] = "Background",
            ["set.nav.window"] = "Window",
            ["set.nav.marks"] = "Watermarks",
            ["set.nav.general"] = "General",
            ["set.language"] = "Language",
            ["set.languageRu"] = "Русский",
            ["set.languageEn"] = "English",
            ["set.theme"] = "Appearance",
            ["set.themeDark"] = "Dark",
            ["set.themeLight"] = "Light",
            ["set.bg"] = "Background",
            ["set.bgSolid"] = "Solid color",
            ["set.bgGradient"] = "Gradient",
            ["set.bgImage"] = "Custom image",
            ["set.color"] = "Color",
            ["set.start"] = "Start",
            ["set.end"] = "End",
            ["set.angle"] = "Angle: {0}°",
            ["set.pickImage"] = "Choose image…",
            ["set.noFile"] = "No file selected",
            ["set.paletteTip"] = "Pick from palette",
            ["set.window"] = "Window",
            ["set.padding"] = "Padding: {0}",
            ["set.shadow"] = "Shadow",
            ["set.blur"] = "Blur: {0}",
            ["set.opacity"] = "Density: {0}%",
            ["set.offset"] = "Offset: {0}",
            ["set.titleCard"] = "Window title bar",
            ["set.hideTitleBar"] = "Cut the title bar",
            ["set.titleAuto"] = "Detect automatically",
            ["set.titleManual"] = "Set manually",
            ["set.titleBarHeight"] = "Height: {0}",
            ["set.hideTitleBarHint"] = "In automatic mode exactly the system title bar is cut, and windows without one lose nothing. Use manual mode if the app draws its own bar on top.",
            ["set.perApp"] = "Height per application",
            ["set.addApp"] = "Add",
            ["set.perAppEmpty"] = "The list is empty. Add an app with the button above, or take a screenshot of it with the cut turned on and the row will appear by itself.",
            ["set.hotkey"] = "Hotkey",
            ["set.setHotkey"] = "Change…",
            ["set.storage"] = "Storage and startup",
            ["set.saveCopy"] = "Save a copy to Pictures",
            ["set.startup"] = "Start when I sign in to Windows",
            ["set.hideSettings"] = "Hide settings",
            ["set.showSettings"] = "Show settings",
            ["set.repository"] = "github.com/wintence/MacShot",

            ["set.addText"] = "Add text",
            ["set.addPicture"] = "Add picture",
            ["set.pickPicture"] = "Choose picture…",
            ["set.markText"] = "Text",
            ["set.markPicture"] = "Picture",
            ["set.markDefaultText"] = "Text",
            ["set.btnRemove"] = "Remove",
            ["set.markShadow"] = "Text shadow",
            ["set.placement"] = "Position",
            ["set.font"] = "Font",
            ["set.bold"] = "Bold",
            ["set.italic"] = "Italic",
            ["set.markFontSize"] = "Font size: {0}",
            ["set.markOpacity"] = "Opacity: {0}%",
            ["set.markMargin"] = "Margin: {0}",
            ["set.pictureHeight"] = "Height: {0}",
            ["set.pictureOpacity"] = "Opacity: {0}%",
            ["set.pictureMargin"] = "Margin: {0}",
            ["set.shadowBlur"] = "Shadow blur: {0}",
            ["set.shadowOpacity"] = "Shadow opacity: {0}%",
            ["set.preview"] = "Preview",

            ["app.alreadyRunning"] = "MacShot is already running — see the tray icon.",
        };

        return new Dictionary<UiLanguage, Dictionary<string, string>>
        {
            [UiLanguage.Russian] = ru,
            [UiLanguage.English] = en,
        };
    }
}