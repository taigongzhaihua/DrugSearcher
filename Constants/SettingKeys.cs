namespace DrugSearcher.Constants;

public static class SettingKeys
{
    // 托盘相关设置
    public const string MinimizeToTrayOnClose = "tray.minimize_on_close";
    public const string ShowTrayIcon = "tray.show_icon";
    public const string ShowTrayNotifications = "tray.show_notifications";

    // 主题相关设置
    public const string ThemeName = "ui.theme_name";
    public const string ThemeColor = "ui.theme_color";
    public const string ThemeMode = "ui.theme_mode"; // Light, Dark, System
    public const string FontSize = "ui.font_size";
    public const string Language = "ui.language";

    // 应用行为设置
    public const string AutoStartup = "app.auto_startup";
    public const string CheckUpdates = "app.check_updates";
    public const string UpdateInterval = "app.update_interval";

    // 搜索相关设置
    public const string SearchTimeout = "search.timeout";
    public const string MaxResults = "search.max_results";
    public const string CacheEnabled = "search.cache_enabled";
}

public static class SettingCategories
{
    public const string Tray = "托盘";
    public const string UI = "界面";
    public const string Application = "应用";
    public const string Search = "搜索";
}