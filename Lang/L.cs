namespace MacShot.Lang;

/// <summary>
/// Мост между словарём и разметкой.
///
/// XAML умеет только x:Static, а перевод лежит в словаре с ключами.
/// Этот класс даёт разметке обычные свойства, которые можно повесить
/// как {x:Static local:L.Точка}.
///
/// Свойства читаются при разборе XAML, поэтому после смены языка окно
/// нужно пересоздать — App так и делает.
/// </summary>
public static class L
{
    public static string TrayCapture => I18n.Current["tray.capture"];
    public static string TraySettings => I18n.Current["tray.settings"];
    public static string TrayExit => I18n.Current["tray.exit"];
    public static string AppName => I18n.Current["tray.app"];

    public static string Title => I18n.Current["set.title"];

    public static string Language => I18n.Current["set.language"];
    public static string LanguageRu => I18n.Current["set.languageRu"];
    public static string LanguageEn => I18n.Current["set.languageEn"];

    public static string Bg => I18n.Current["set.bg"];
    public static string BgSolid => I18n.Current["set.bgSolid"];
    public static string BgGradient => I18n.Current["set.bgGradient"];
    public static string BgImage => I18n.Current["set.bgImage"];
    public static string Color => I18n.Current["set.color"];
    public static string Start => I18n.Current["set.start"];
    public static string End => I18n.Current["set.end"];
    public static string PickImage => I18n.Current["set.pickImage"];
    public static string NoFile => I18n.Current["set.noFile"];
    public static string PaletteTip => I18n.Current["set.paletteTip"];

    public static string Window => I18n.Current["set.window"];
    public static string Shadow => I18n.Current["set.shadow"];
    public static string HotKey => I18n.Current["set.hotkey"];
    public static string SetHotKey => I18n.Current["set.setHotkey"];
    public static string Storage => I18n.Current["set.storage"];
    public static string SaveCopy => I18n.Current["set.saveCopy"];
    public static string SaveHint => I18n.Current["set.saveHint"];
    public static string Startup => I18n.Current["set.startup"];
    public static string StartupHint => I18n.Current["set.startupHint"];

    public static string BtnCancel => I18n.Current["btn.cancel"];
    public static string BtnDone => I18n.Current["btn.done"];
    public static string BtnCustom => I18n.Current["btn.custom"];
    public static string HotKeyTitle => I18n.Current["hk.title"];
    public static string HotKeyPrompt => I18n.Current["hk.prompt"];
    public static string HotKeyHint => I18n.Current["hk.hint"];
    public static string ColorHint => I18n.Current["color.hint"];
}