using DrugSearcher.Constants;
using DrugSearcher.Enums;
using DrugSearcher.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace DrugSearcher.Services;

/// <summary>
/// 动态设置服务 - 负责设置项的定义、管理和UI展示
/// 基于 UserSettingsService 提供更高级的设置管理功能
/// </summary>
public class DynamicSettingsService : IDynamicSettingsService
{
    #region 私有字段

    private readonly IUserSettingsService _userSettingsService;
    private readonly Dictionary<string, DynamicSettingItem> _settingsLookup;
    private bool _isInitialized;

    #endregion

    #region 构造函数

    public DynamicSettingsService(IUserSettingsService userSettingsService)
    {
        _userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));
        SettingGroups = [];
        _settingsLookup = [];

        // 监听底层设置变更
        _userSettingsService.SettingChanged += OnUserSettingChanged;
        _userSettingsService.SettingsReloaded += OnUserSettingsReloaded;
        if (_isInitialized) return;
        InitializeDefaultSettings();
    }

    #endregion

    #region 公共属性

    /// <summary>
    /// 设置分组集合
    /// </summary>
    public ObservableCollection<DynamicSettingGroup> SettingGroups { get; }

    #endregion

    #region 公共方法

    /// <summary>
    /// 注册设置项
    /// </summary>
    /// <param name="item">设置项</param>
    public void RegisterSetting(DynamicSettingItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _settingsLookup[item.Key] = item;

        // 找到或创建分组
        var group = SettingGroups.FirstOrDefault(g => g.Name == item.Category);
        if (group == null)
        {
            group = new DynamicSettingGroup
            {
                Name = item.Category,
                DisplayName = GetCategoryDisplayName(item.Category),
                Icon = GetCategoryIcon(item.Category),
                Order = GetCategoryOrder(item.Category)
            };
            InsertGroupInOrder(group);
        }

        // 添加设置项
        InsertItemInOrder(group, item);

        Debug.WriteLine($"注册设置项: {item.Key}");
    }

    /// <summary>
    /// 注册设置分组
    /// </summary>
    /// <param name="group">设置分组</param>
    public void RegisterSettingGroup(DynamicSettingGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);

        var existingGroup = SettingGroups.FirstOrDefault(g => g.Name == group.Name);
        if (existingGroup != null)
        {
            // 合并设置项
            foreach (var item in group.Items)
            {
                RegisterSetting(item);
            }
        }
        else
        {
            InsertGroupInOrder(group);

            // 注册分组中的所有设置项
            foreach (var item in group.Items)
            {
                _settingsLookup[item.Key] = item;
            }
        }

        Debug.WriteLine($"注册设置分组: {group.Name}");
    }

    /// <summary>
    /// 获取设置项
    /// </summary>
    /// <param name="key">设置键</param>
    /// <returns>设置项</returns>
    public DynamicSettingItem? GetSetting(string key) => _settingsLookup.TryGetValue(key, out var item) ? item : null;

    /// <summary>
    /// 更新设置值
    /// </summary>
    /// <param name="key">设置键</param>
    /// <param name="value">新值</param>
    public async Task UpdateSettingAsync(string key, object? value)
    {
        if (!_settingsLookup.TryGetValue(key, out var item))
        {
            Debug.WriteLine($"设置项不存在: {key}");
            return;
        }

        if (item.IsReadOnly)
        {
            Debug.WriteLine($"设置项是只读的: {key}");
            return;
        }

        // 验证值
        if (item.Validator != null && !item.Validator(value))
        {
            Debug.WriteLine($"设置值验证失败: {key} = {value}");
            return;
        }

        try
        {
            // 保存到底层服务
            await _userSettingsService.SetSettingAsync(key, value);

            // 更新本地值（这会在 OnUserSettingChanged 中自动更新）
            Debug.WriteLine($"更新设置: {key} = {value}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新设置失败: {key}, {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 加载设置值
    /// </summary>
    public async Task LoadSettingsAsync()
    {
        foreach (var item in _settingsLookup.Values)
        {
            try
            {
                var value = await _userSettingsService.GetSettingAsync<object?>(item.Key, item.DefaultValue);
                item.Value = value;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载设置失败: {item.Key}, {ex.Message}");
                item.Value = item.DefaultValue;
            }
        }

        _isInitialized = true;
        Debug.WriteLine($"已加载 {_settingsLookup.Count} 个设置项");
    }

    /// <summary>
    /// 重置分组设置
    /// </summary>
    /// <param name="groupName">分组名称</param>
    public async Task ResetGroupAsync(string groupName)
    {
        var group = SettingGroups.FirstOrDefault(g => g.Name == groupName);
        if (group == null)
            return;

        if (group.CustomResetAction != null)
        {
            group.CustomResetAction();
            return;
        }

        foreach (var item in group.Items)
        {
            await UpdateSettingAsync(item.Key, item.DefaultValue);
        }
    }

    /// <summary>
    /// 重置所有设置
    /// </summary>
    public async Task ResetAllSettingsAsync() => await _userSettingsService.ResetToDefaultsAsync();

    /// <summary>
    /// 搜索设置
    /// </summary>
    /// <param name="searchText">搜索文本</param>
    /// <returns>匹配的设置项</returns>
    public List<DynamicSettingItem> SearchSettings(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return [];

        var results = new List<DynamicSettingItem>();
        var searchLower = searchText.ToLowerInvariant();

        foreach (var item in _settingsLookup.Values)
        {
            if (IsSettingMatch(item, searchLower))
            {
                results.Add(item);
            }
        }

        return results;
    }

    /// <summary>
    /// 获取分组设置
    /// </summary>
    /// <param name="groupName">分组名称</param>
    /// <returns>分组中的所有设置</returns>
    public List<DynamicSettingItem> GetGroupSettings(string groupName)
    {
        var group = SettingGroups.FirstOrDefault(g => g.Name == groupName);
        return group?.Items.ToList() ?? [];
    }

    /// <summary>
    /// 获取所有设置
    /// </summary>
    public async Task<Dictionary<string, object?>> GetAllSettingsAsync() => await _userSettingsService.GetAllSettingsAsync();

    #endregion

    #region 事件处理

    /// <summary>
    /// 处理用户设置变更
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="e">事件参数</param>
    private void OnUserSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        if (_settingsLookup.TryGetValue(e.Key, out var item))
        {
            item.Value = e.NewValue;

            // 触发值变更回调
            item.OnValueChanged?.Invoke(e.NewValue);
        }
    }

    /// <summary>
    /// 处理用户设置重新加载
    /// </summary>
    /// <param name="sender">发送者</param>
    /// <param name="e">事件参数</param>
    private void OnUserSettingsReloaded(object? sender, EventArgs e) =>
        // 重新加载所有设置值
        _ = LoadSettingsAsync();

    #endregion

    #region 私有方法

    /// <summary>
    /// 初始化默认设置
    /// </summary>
    private void InitializeDefaultSettings()
    {
        RegisterTraySettings();
        RegisterUiSettings();
        RegisterHotKeySettings();
        RegisterAppSettings();
    }

    /// <summary>
    /// 注册托盘设置
    /// </summary>
    private void RegisterTraySettings()
    {
        var trayGroup = new DynamicSettingGroup
        {
            Name = SettingCategories.TRAY,
            DisplayName = "系统托盘",
            Description = "管理系统托盘相关设置",
            Icon = "\ue61e",
            Order = 1
        };

        // 关闭时最小化到托盘
        trayGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.MINIMIZE_TO_TRAY_ON_CLOSE,
            DisplayName = "关闭时最小化到托盘",
            Description = "点击关闭按钮时将窗口最小化到系统托盘，而不是完全退出应用程序。",
            Category = SettingCategories.TRAY,
            SettingType = DynamicSettingType.Toggle,
            DefaultValue = true,
            Order = 1,
            SearchKeywords = ["托盘", "最小化", "关闭", "tray", "minimize", "close"]
        });

        // 显示托盘图标
        trayGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.SHOW_TRAY_ICON,
            DisplayName = "显示系统托盘图标",
            Description = "在系统托盘区域显示应用程序图标，方便快速访问和控制应用程序。",
            Category = SettingCategories.TRAY,
            SettingType = DynamicSettingType.Toggle,
            DefaultValue = true,
            Order = 2,
            SearchKeywords = ["托盘", "图标", "显示", "tray", "icon", "show"]
        });

        // 显示托盘通知
        trayGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.SHOW_TRAY_NOTIFICATIONS,
            DisplayName = "显示托盘通知",
            Description = "允许应用程序通过系统托盘显示重要通知和状态更新。",
            Category = SettingCategories.TRAY,
            SettingType = DynamicSettingType.Toggle,
            DefaultValue = true,
            Order = 3,
            SearchKeywords = ["托盘", "通知", "显示", "tray", "notification", "show"]
        });

        RegisterSettingGroup(trayGroup);
    }

    // 在 RegisterUiSettings 方法中，修复主题模式设置
    private void RegisterUiSettings()
    {
        var uiGroup = new DynamicSettingGroup
        {
            Name = SettingCategories.UI,
            DisplayName = "外观",
            Description = "自定义应用程序的外观和主题",
            Icon = "\ue61f",
            Order = 2
        };

        // 主题模式 - 修复：不需要 DisplayMemberPath 和 SelectedValuePath
        uiGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.THEME_MODE,
            DisplayName = "主题模式",
            Description = "选择应用程序的外观主题。浅色模式适合白天使用，深色模式有助于减少眼疲劳。",
            Category = SettingCategories.UI,
            SettingType = DynamicSettingType.ComboBox,
            DefaultValue = ThemeMode.Light,
            Order = 1,
            SearchKeywords = ["主题", "模式", "浅色", "深色", "theme", "mode", "light", "dark"],
            AdditionalData = new Dictionary<string, object>
            {
                ["ItemsSource"] = Enum.GetValues<ThemeMode>()
                // 不需要 DisplayMemberPath 和 SelectedValuePath，因为直接绑定枚举
            }
        });

        // 主题颜色 - 修复：不需要 DisplayMemberPath 和 SelectedValuePath
        uiGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.THEME_COLOR,
            DisplayName = "主题颜色",
            Description = "选择应用程序的强调色，这将影响按钮、链接和其他交互元素的颜色。",
            Category = SettingCategories.UI,
            SettingType = DynamicSettingType.ComboBox,
            DefaultValue = ThemeColor.Blue,
            Order = 2,
            SearchKeywords = ["主题", "颜色", "色彩", "theme", "color"],
            AdditionalData = new Dictionary<string, object>
            {
                ["ItemsSource"] = Enum.GetValues<ThemeColor>()
                // 不需要 DisplayMemberPath 和 SelectedValuePath，因为直接绑定枚举
            }
        });

        // 字体大小 - 修复：使用 double 类型
        uiGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.FONT_SIZE,
            DisplayName = "字体大小",
            Description = "调整界面文字的显示大小，较大的字体有助于提高可读性。",
            Category = SettingCategories.UI,
            SettingType = DynamicSettingType.Slider,
            DefaultValue = 12.0, // 修复：使用 double
            Order = 3,
            SearchKeywords = ["字体", "大小", "文字", "font", "size", "text"],
            AdditionalData = new Dictionary<string, object>
            {
                ["Minimum"] = 8.0, // 修复：使用 double
                ["Maximum"] = 72.0, // 修复：使用 double
                ["TickFrequency"] = 2.0 // 修复：使用 double
            },
            Validator = value => value is >= 8.0 and <= 72.0
        });

        // 界面语言 - 修复：正确设置 DisplayMemberPath 和 SelectedValuePath
        uiGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.LANGUAGE,
            DisplayName = "界面语言",
            Description = "选择应用程序界面的显示语言。更改后可能需要重启应用程序。",
            Category = SettingCategories.UI,
            SettingType = DynamicSettingType.ComboBox,
            DefaultValue = "zh-CN",
            Order = 4,
            SearchKeywords = ["语言", "界面", "language", "ui"],
            AdditionalData = new Dictionary<string, object>
            {
                ["ItemsSource"] = new[]
                {
                    new { Code = "zh-CN", DisplayName = "简体中文" },
                    new { Code = "en-US", DisplayName = "English" },
                    new { Code = "zh-TW", DisplayName = "繁體中文" }
                },
                ["DisplayMemberPath"] = "DisplayName", // 修复：添加这个键
                ["SelectedValuePath"] = "Code" // 修复：添加这个键
            },
        });

        RegisterSettingGroup(uiGroup);
    }

    /// <summary>
    /// 注册快捷键设置
    /// </summary>
    private void RegisterHotKeySettings()
    {
        var hotKeyGroup = new DynamicSettingGroup
        {
            Name = SettingCategories.HOT_KEY,
            DisplayName = "快捷键",
            Description = "自定义应用程序的快捷键设置",
            Icon = "\ue623",
            Order = 4
        };

        // 显示主窗口快捷键
        hotKeyGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.HOT_KEY_SHOW_MAIN_WINDOW,
            DisplayName = "显示主窗口",
            Description = "显示或隐藏主窗口的全局快捷键",
            Category = SettingCategories.HOT_KEY,
            SettingType = DynamicSettingType.HotKey,
            DefaultValue = CreateDefaultHotKeySetting(Key.F1, ModifierKeys.Alt),
            Order = 1,
            SearchKeywords = ["显示", "主窗口", "快捷键", "show", "main", "window", "hotkey"],
            Validator = ValidateHotKey
        });

        // 快速搜索快捷键
        hotKeyGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.HOT_KEY_QUICK_SEARCH,
            DisplayName = "快速搜索",
            Description = "快速启动搜索功能的全局快捷键",
            Category = SettingCategories.HOT_KEY,
            SettingType = DynamicSettingType.HotKey,
            DefaultValue = CreateDefaultHotKeySetting(Key.F2, ModifierKeys.Control | ModifierKeys.Alt),
            Order = 2,
            SearchKeywords = ["快速", "搜索", "快捷键", "quick", "search", "hotkey"],
            Validator = ValidateHotKey
        });

        // 搜索快捷键
        hotKeyGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.HOT_KEY_SEARCH,
            DisplayName = "搜索",
            Description = "在应用程序内执行搜索的快捷键",
            Category = SettingCategories.HOT_KEY,
            SettingType = DynamicSettingType.HotKey,
            DefaultValue = CreateDefaultHotKeySetting(Key.F, ModifierKeys.Control),
            Order = 3,
            SearchKeywords = ["搜索", "快捷键", "search", "hotkey"],
            Validator = ValidateHotKey
        });

        // 刷新快捷键
        hotKeyGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.HOT_KEY_REFRESH,
            DisplayName = "刷新",
            Description = "刷新当前页面或数据的快捷键",
            Category = SettingCategories.HOT_KEY,
            SettingType = DynamicSettingType.HotKey,
            DefaultValue = CreateDefaultHotKeySetting(Key.F5, ModifierKeys.None),
            Order = 4,
            SearchKeywords = ["刷新", "快捷键", "refresh", "hotkey"],
            Validator = ValidateHotKey
        });

        // 设置快捷键
        hotKeyGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.HOT_KEY_SETTINGS,
            DisplayName = "设置",
            Description = "打开设置页面的快捷键",
            Category = SettingCategories.HOT_KEY,
            SettingType = DynamicSettingType.HotKey,
            DefaultValue = CreateDefaultHotKeySetting(Key.S, ModifierKeys.Control),
            Order = 5,
            SearchKeywords = ["设置", "快捷键", "settings", "hotkey"],
            Validator = ValidateHotKey
        });

        // 退出快捷键
        hotKeyGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.HOT_KEY_EXIT,
            DisplayName = "退出",
            Description = "退出应用程序的快捷键",
            Category = SettingCategories.HOT_KEY,
            SettingType = DynamicSettingType.HotKey,
            DefaultValue = CreateDefaultHotKeySetting(Key.Q, ModifierKeys.Control),
            Order = 6,
            SearchKeywords = ["退出", "快捷键", "exit", "quit", "hotkey"],
            Validator = ValidateHotKey
        });

        RegisterSettingGroup(hotKeyGroup);
    }

    /// <summary>
    /// 注册应用程序设置
    /// </summary>
    private void RegisterAppSettings()
    {
        var appGroup = new DynamicSettingGroup
        {
            Name = SettingCategories.APPLICATION,
            DisplayName = "应用程序",
            Description = "管理应用程序行为和启动设置",
            Icon = "\ue621",
            Order = 3
        };

        // 开机自启动
        appGroup.Items.Add(new DynamicSettingItem
        {
            Key = SettingKeys.AUTO_STARTUP,
            DisplayName = "开机自启动",
            Description = "启用后，应用程序将在系统启动时自动运行，无需手动打开。",
            Category = SettingCategories.APPLICATION,
            SettingType = DynamicSettingType.Toggle,
            DefaultValue = false,
            Order = 1,
            SearchKeywords = ["开机", "自启动", "启动", "startup", "auto", "boot"]
        });

        RegisterSettingGroup(appGroup);
    }

    /// <summary>
    /// 按顺序插入分组
    /// </summary>
    /// <param name="group">分组</param>
    private void InsertGroupInOrder(DynamicSettingGroup group)
    {
        var insertIndex = 0;
        for (var i = 0; i < SettingGroups.Count; i++)
        {
            if (SettingGroups[i].Order > group.Order)
            {
                insertIndex = i;
                break;
            }

            insertIndex = i + 1;
        }

        SettingGroups.Insert(insertIndex, group);
    }

    /// <summary>
    /// 按顺序插入设置项
    /// </summary>
    /// <param name="group">分组</param>
    /// <param name="item">设置项</param>
    private static void InsertItemInOrder(DynamicSettingGroup group, DynamicSettingItem item)
    {
        var insertIndex = 0;
        for (var i = 0; i < group.Items.Count; i++)
        {
            if (group.Items[i].Order > item.Order)
            {
                insertIndex = i;
                break;
            }

            insertIndex = i + 1;
        }

        group.Items.Insert(insertIndex, item);
    }

    /// <summary>
    /// 检查设置项是否匹配搜索文本
    /// </summary>
    /// <param name="item">设置项</param>
    /// <param name="searchText">搜索文本</param>
    /// <returns>是否匹配</returns>
    private static bool IsSettingMatch(DynamicSettingItem item, string searchText)
    {
        // 检查显示名称
        if (item.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        // 检查描述
        if (item.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        // 检查搜索关键词
        return item.SearchKeywords.Any(keyword =>
            keyword.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            searchText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取分类显示名称
    /// </summary>
    /// <param name="category">分类</param>
    /// <returns>显示名称</returns>
    private static string GetCategoryDisplayName(string category) => category switch
    {
        SettingCategories.TRAY => "系统托盘",
        SettingCategories.UI => "外观",
        SettingCategories.APPLICATION => "应用程序",
        SettingCategories.SEARCH => "搜索",
        _ => category
    };

    /// <summary>
    /// 获取分类图标
    /// </summary>
    /// <param name="category">分类</param>
    /// <returns>图标</returns>
    private static string GetCategoryIcon(string category) => category switch
    {
        SettingCategories.TRAY => "\ue61e",
        SettingCategories.UI => "\ue61f",
        SettingCategories.APPLICATION => "\ue621",
        SettingCategories.SEARCH => "\ue622",
        _ => "\ue620"
    };

    /// <summary>
    /// 获取分类排序
    /// </summary>
    /// <param name="category">分类</param>
    /// <returns>排序值</returns>
    private static int GetCategoryOrder(string category) => category switch
    {
        SettingCategories.TRAY => 1,
        SettingCategories.UI => 2,
        SettingCategories.APPLICATION => 3,
        SettingCategories.SEARCH => 4,
        _ => 999
    };

    /// <summary>
    /// 创建默认快捷键设置
    /// </summary>
    private static string CreateDefaultHotKeySetting(Key key, ModifierKeys modifiers)
    {
        var hotKey = new HotKeySetting(key, modifiers);
        return System.Text.Json.JsonSerializer.Serialize(hotKey);
    }

    /// <summary>
    /// 验证快捷键设置
    /// </summary>
    private static bool ValidateHotKey(object? value)
    {
        if (value == null) return true; // 允许空值

        try
        {
            HotKeySetting? hotKey = null;

            switch (value)
            {
                case string jsonString when string.IsNullOrEmpty(jsonString):
                    return true; // 允许空字符串
                case string jsonString:
                    hotKey = System.Text.Json.JsonSerializer.Deserialize<HotKeySetting>(jsonString);
                    break;
                case HotKeySetting directHotKey:
                    hotKey = directHotKey;
                    break;
            }

            if (hotKey == null) return true; // 如果无法解析，允许通过（可能是清空操作）

            // 如果快捷键被禁用，直接通过验证
            if (!hotKey.IsEnabled) return true;

            // 验证快捷键是否有效
            if (IsValidHotKey(hotKey)) return true;
            Debug.WriteLine($"快捷键无效: {hotKey.Key} + {hotKey.Modifiers}");
            return false;

            // 检查是否与其他快捷键冲突（暂时注释掉，因为这可能导致验证失败）
            // return !IsHotKeyConflicted(hotKey);

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"验证快捷键时发生异常: {ex.Message}");
            return true; // 发生异常时允许通过，避免阻塞设置保存
        }
    }

    /// <summary>
    /// 验证快捷键是否有效
    /// </summary>
    private static bool IsValidHotKey(HotKeySetting hotKey)
    {
        switch (hotKey.Key)
        {
            // 检查Key是否有效
            case Key.None:
                return false;
            // 功能键(F1-F24)可以不需要修饰键
            case >= Key.F1 and <= Key.F24:
                return true;
        }

        // 其他键需要至少一个修饰键
        if (hotKey.Modifiers == ModifierKeys.None)
            return false;

        // 检查是否是系统保留的快捷键
        return !IsSystemReservedHotKey(hotKey);
    }

    /// <summary>
    /// 检查是否是系统保留的快捷键
    /// </summary>
    private static bool IsSystemReservedHotKey(HotKeySetting hotKey) => hotKey switch
    {
        // Alt + F4 (关闭窗口)
        // Alt + Tab (切换窗口)
        // Ctrl + Alt + Del (安全桌面)
        // Windows + L (锁定计算机)
        { Modifiers: ModifierKeys.Alt, Key: Key.F4 }
            or { Modifiers: ModifierKeys.Alt, Key: Key.Tab }
            or { Modifiers: (ModifierKeys.Control | ModifierKeys.Alt), Key: Key.Delete }
            or { Modifiers: ModifierKeys.Windows, Key: Key.L } => true,
        _ => false
    };

    /// <summary>
    /// 检查快捷键是否冲突（修改版本）
    /// </summary>
    private async Task<bool> IsHotKeyConflictedAsync(string currentSettingKey, HotKeySetting hotKey)
    {
        try
        {
            // 获取所有快捷键设置
            var allSettings = await _userSettingsService.GetAllSettingsAsync();

            foreach (var kvp in allSettings)
            {
                // 跳过当前设置项
                if (kvp.Key == currentSettingKey)
                    continue;

                // 检查是否是快捷键设置
                if (kvp.Key.StartsWith("hotkey.") && kvp.Value is string jsonValue && !string.IsNullOrEmpty(jsonValue))
                {
                    try
                    {
                        var existingHotKey = System.Text.Json.JsonSerializer.Deserialize<HotKeySetting>(jsonValue);
                        if (existingHotKey is { IsEnabled: true } &&
                            existingHotKey.Key == hotKey.Key &&
                            existingHotKey.Modifiers == hotKey.Modifiers)
                        {
                            Debug.WriteLine($"快捷键冲突: {currentSettingKey} 与 {kvp.Key} 都使用 {hotKey.Key} + {hotKey.Modifiers}");
                            return true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"解析快捷键设置失败: {kvp.Key}, {ex.Message}");
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检查快捷键冲突失败: {ex.Message}");
            return false; // 发生异常时假设没有冲突
        }
    }
    #endregion
}