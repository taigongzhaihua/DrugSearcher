namespace DrugSearcher.Constants;

public static class SettingKeys
{
    // 托盘相关设置
    public const string MINIMIZE_TO_TRAY_ON_CLOSE = "tray.minimize_on_close";
    public const string SHOW_TRAY_ICON = "tray.show_icon";
    public const string SHOW_TRAY_NOTIFICATIONS = "tray.show_notifications";

    // 主题相关设置
    public const string THEME_NAME = "ui.theme_name";
    public const string THEME_COLOR = "ui.theme_color";
    public const string THEME_MODE = "ui.theme_mode"; // Light, Dark, System
    public const string FONT_SIZE = "ui.font_size";
    public const string LANGUAGE = "ui.language";

    // 应用行为设置
    public const string AUTO_STARTUP = "app.auto_startup";
    public const string CHECK_UPDATES = "app.check_updates";
    public const string UPDATE_INTERVAL = "app.update_interval";

    // 搜索相关设置
    public const string SEARCH_TIMEOUT = "search.timeout";
    public const string MAX_RESULTS = "search.max_results";
    public const string CACHE_ENABLED = "search.cache_enabled";

    // 快捷键相关设置
    public const string HOT_KEY_SHOW_MAIN_WINDOW = "hotkey.show_main_window";
    public const string HOT_KEY_QUICK_SEARCH = "hotkey.quick_search";
    public const string HOT_KEY_SEARCH = "hotkey.search";
    public const string HOT_KEY_REFRESH = "hotkey.refresh";
    public const string HOT_KEY_SETTINGS = "hotkey.settings";
    public const string HOT_KEY_EXIT = "hotkey.exit";
}