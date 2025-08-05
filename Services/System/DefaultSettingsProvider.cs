using DrugSearcher.Constants;
using DrugSearcher.Enums;
using DrugSearcher.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows.Input;

namespace DrugSearcher.Services;

/// <summary>
/// 默认设置提供程序，定义和管理应用程序的默认设置配置
/// 包括设置定义、默认值、验证规则等
/// </summary>
public class DefaultSettingsProvider : IDefaultSettingsProvider
{
    #region 公共方法

    /// <summary>
    /// 获取所有设置的默认定义
    /// </summary>
    /// <returns>设置定义列表</returns>
    public List<SettingDefinition> GetDefaultDefinitions()
    {
        try
        {
            var definitions = CreateSettingDefinitions();
            Debug.WriteLine($"已创建 {definitions.Count} 个设置定义");
            return definitions;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取默认设置定义失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 获取默认设置项（用于数据库初始化）
    /// </summary>
    /// <returns>设置项列表</returns>
    public List<SettingItem> GetDefaultSettingItems()
    {
        try
        {
            var definitions = GetDefaultDefinitions();
            var settingItems = ConvertDefinitionsToSettingItems(definitions);

            Debug.WriteLine($"已创建 {settingItems.Count} 个默认设置项");
            return settingItems;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取默认设置项失败: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region 私有方法 - 设置定义创建

    /// <summary>
    /// 创建所有设置定义
    /// </summary>
    /// <returns>设置定义列表</returns>
    private static List<SettingDefinition> CreateSettingDefinitions() => [
            ..CreateTraySettings(),
            ..CreateUiSettings(),
            ..CreateHotKeySettings(),
            ..CreateApplicationSettings()
        ];

    /// <summary>
    /// 创建托盘相关设置定义
    /// </summary>
    /// <returns>托盘设置定义列表</returns>
    private static List<SettingDefinition> CreateTraySettings() => [
            new()
            {
                Key = SettingKeys.MINIMIZE_TO_TRAY_ON_CLOSE,
                ValueType = typeof(bool),
                DefaultValue = true,
                Description = "关闭窗口时最小化到托盘",
                Category = SettingCategories.TRAY,
                IsReadOnly = false,
                Validator = value => value is bool
            },


            new()
            {
                Key = SettingKeys.SHOW_TRAY_ICON,
                ValueType = typeof(bool),
                DefaultValue = true,
                Description = "显示系统托盘图标",
                Category = SettingCategories.TRAY,
                IsReadOnly = false,
                Validator = value => value is bool
            },


            new()
            {
                Key = SettingKeys.SHOW_TRAY_NOTIFICATIONS,
                ValueType = typeof(bool),
                DefaultValue = true,
                Description = "显示托盘通知",
                Category = SettingCategories.TRAY,
                IsReadOnly = false,
                Validator = value => value is bool
            }
        ];

    /// <summary>
    /// 创建UI相关设置定义
    /// </summary>
    /// <returns>UI设置定义列表</returns>
    private static List<SettingDefinition> CreateUiSettings() => [
            new()
            {
                Key = SettingKeys.THEME_MODE,
                ValueType = typeof(ThemeMode),
                DefaultValue = ThemeMode.Light,
                Description = "主题模式（浅色/深色/跟随系统）",
                Category = SettingCategories.UI,
                IsReadOnly = false,
                Validator = ValidateThemeMode
            },


            new()
            {
                Key = SettingKeys.THEME_COLOR,
                ValueType = typeof(ThemeColor),
                DefaultValue = ThemeColor.Blue,
                Description = "主题颜色",
                Category = SettingCategories.UI,
                IsReadOnly = false,
                Validator = ValidateThemeColor
            },


            new()
            {
                Key = SettingKeys.FONT_SIZE,
                ValueType = typeof(int),
                DefaultValue = 12,
                Description = "字体大小",
                Category = SettingCategories.UI,
                IsReadOnly = false,
                Validator = ValidateFontSize
            },


            new()
            {
                Key = SettingKeys.LANGUAGE,
                ValueType = typeof(string),
                DefaultValue = "zh-CN",
                Description = "界面语言",
                Category = SettingCategories.UI,
                IsReadOnly = false,
                Validator = ValidateLanguage
            }
        ];
    /// <summary>
    /// 创建快捷键相关设置定义
    /// </summary>
    private static List<SettingDefinition> CreateHotKeySettings() => [
            new()
            {
            Key = SettingKeys.HOT_KEY_SHOW_MAIN_WINDOW,
            ValueType = typeof(string), // 改为string类型，存储JSON
            DefaultValue = JsonSerializer.Serialize(new HotKeySetting(Key.F1, ModifierKeys.Alt)),
            Description = "显示主窗口的快捷键",
            Category = SettingCategories.HOT_KEY,
            IsReadOnly = false,
            Validator = null // 移除验证器，在DynamicSettingsService中处理
        },

        new()
        {
            Key = SettingKeys.HOT_KEY_QUICK_SEARCH,
            ValueType = typeof(string),
            DefaultValue = JsonSerializer.Serialize(new HotKeySetting(Key.F2, ModifierKeys.Control | ModifierKeys.Alt)),
            Description = "快速搜索的快捷键",
            Category = SettingCategories.HOT_KEY,
            IsReadOnly = false,
            Validator = null
        },

        new()
        {
            Key = SettingKeys.HOT_KEY_SEARCH,
            ValueType = typeof(string),
            DefaultValue = JsonSerializer.Serialize(new HotKeySetting(Key.F, ModifierKeys.Control)),
            Description = "搜索的快捷键",
            Category = SettingCategories.HOT_KEY,
            IsReadOnly = false,
            Validator = null
        },

        new()
        {
            Key = SettingKeys.HOT_KEY_REFRESH,
            ValueType = typeof(string),
            DefaultValue = JsonSerializer.Serialize(new HotKeySetting(Key.F5, ModifierKeys.None)),
            Description = "刷新的快捷键",
            Category = SettingCategories.HOT_KEY,
            IsReadOnly = false,
            Validator = null
        },

        new()
        {
            Key = SettingKeys.HOT_KEY_SETTINGS,
            ValueType = typeof(string),
            DefaultValue = JsonSerializer.Serialize(new HotKeySetting(Key.S, ModifierKeys.Control)),
            Description = "设置的快捷键",
            Category = SettingCategories.HOT_KEY,
            IsReadOnly = false,
            Validator = null
        },

        new()
        {
            Key = SettingKeys.HOT_KEY_EXIT,
            ValueType = typeof(string),
            DefaultValue = JsonSerializer.Serialize(new HotKeySetting(Key.Q, ModifierKeys.Control)),
            Description = "退出的快捷键",
            Category = SettingCategories.HOT_KEY,
            IsReadOnly = false,
            Validator = null
        }
        ];
    /// <summary>
    /// 创建应用程序相关设置定义
    /// </summary>
    /// <returns>应用程序设置定义列表</returns>
    private static List<SettingDefinition> CreateApplicationSettings() => [
            new()
            {
                Key = SettingKeys.AUTO_STARTUP,
                ValueType = typeof(bool),
                DefaultValue = false,
                Description = "开机自启动",
                Category = SettingCategories.APPLICATION,
                IsReadOnly = false,
                Validator = value => value is bool
            }
        ];

    #endregion

    #region 私有方法 - 验证器

    /// <summary>
    /// 验证主题模式
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <returns>是否有效</returns>
    private static bool ValidateThemeMode(object? value) => value is ThemeMode themeMode && Enum.IsDefined(themeMode);

    /// <summary>
    /// 验证主题颜色
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <returns>是否有效</returns>
    private static bool ValidateThemeColor(object? value) => value is ThemeColor themeColor && Enum.IsDefined(themeColor);

    /// <summary>
    /// 验证字体大小
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <returns>是否有效</returns>
    private static bool ValidateFontSize(object? value) => value switch
    {
        double doubleSize => doubleSize is >= FontSizeConstraints.MIN_SIZE and <= FontSizeConstraints.MAX_SIZE,
        int intSize => intSize is >= FontSizeConstraints.MIN_SIZE and <= FontSizeConstraints.MAX_SIZE,
        _ => false
    };

    /// <summary>
    /// 验证语言设置
    /// </summary>
    /// <param name="value">要验证的值</param>
    /// <returns>是否有效</returns>
    private static bool ValidateLanguage(object? value)
    {
        if (value is not string language)
            return false;

        if (string.IsNullOrWhiteSpace(language))
            return false;

        // 检查是否为支持的语言
        var supportedLanguages = GetSupportedLanguages();
        return supportedLanguages.Contains(language);
    }

    /// <summary>
    /// 获取支持的语言列表
    /// </summary>
    /// <returns>支持的语言代码列表</returns>
    private static HashSet<string> GetSupportedLanguages() => [
            "zh-CN", // 简体中文
            "en-US", // 英语
            "zh-TW"
        ];

    #endregion

    #region 私有方法 - 转换

    /// <summary>
    /// 将设置定义转换为设置项
    /// </summary>
    /// <param name="definitions">设置定义列表</param>
    /// <returns>设置项列表</returns>
    private static List<SettingItem> ConvertDefinitionsToSettingItems(List<SettingDefinition> definitions)
    {
        var settingItems = new List<SettingItem>();
        var currentTime = DateTime.UtcNow;

        foreach (var definition in definitions)
        {
            try
            {
                var settingItem = ConvertDefinitionToSettingItem(definition, currentTime);
                settingItems.Add(settingItem);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"转换设置定义 '{definition.Key}' 时出错: {ex.Message}");
                // 继续处理其他设置
            }
        }

        return settingItems;
    }

    /// <summary>
    /// 将单个设置定义转换为设置项
    /// </summary>
    /// <param name="definition">设置定义</param>
    /// <param name="currentTime">当前时间</param>
    /// <returns>设置项</returns>
    private static SettingItem ConvertDefinitionToSettingItem(SettingDefinition definition, DateTime currentTime) => new()
    {
        Key = definition.Key,
        ValueType = definition.ValueType.Name,
        Value = ConvertValueToString(definition.DefaultValue),
        Description = definition.Description,
        Category = definition.Category,
        IsReadOnly = definition.IsReadOnly,
        UserId = null, // 全局设置
        CreatedAt = currentTime,
        UpdatedAt = currentTime
    };

    /// <summary>
    /// 将值转换为字符串表示
    /// </summary>
    /// <param name="value">要转换的值</param>
    /// <returns>字符串表示</returns>
    private static string? ConvertValueToString(object? value)
    {
        try
        {
            return value switch
            {
                null => null,
                bool b => b.ToString().ToLowerInvariant(),
                double d => d.ToString(CultureInfo.InvariantCulture),
                decimal dec => dec.ToString(CultureInfo.InvariantCulture),
                float f => f.ToString(CultureInfo.InvariantCulture),
                DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToString("O", CultureInfo.InvariantCulture),
                Enum e => e.ToString(),
                _ => value.ToString()
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"转换值到字符串时出错: {ex.Message}, 值: {value}");
            return value?.ToString();
        }
    }

    #endregion
}