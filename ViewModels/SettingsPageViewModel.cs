using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Common.Enums;
using DrugSearcher.Constants;
using DrugSearcher.Models;
using DrugSearcher.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace DrugSearcher.ViewModels;

/// <summary>
/// 设置页面视图模型，管理应用程序的各种设置选项
/// 包括托盘设置、UI设置、应用程序设置等
/// </summary>
public partial class SettingsPageViewModel : ObservableObject
{
    #region 私有字段

    private readonly IUserSettingsService _settingsService;
    private readonly List<SettingDefinition> _defaultDefinitions;

    // 防抖定时器
    private readonly Dictionary<string, CancellationTokenSource> _debounceCancellationTokens = [];

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化设置页面视图模型
    /// </summary>
    /// <param name="settingsService">用户设置服务</param>
    /// <param name="defaultSettingsProvider">默认设置提供程序</param>
    public SettingsPageViewModel(IUserSettingsService settingsService, IDefaultSettingsProvider defaultSettingsProvider)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        var defaultSettingsProvider1 = defaultSettingsProvider ?? throw new ArgumentNullException(nameof(defaultSettingsProvider));
        _defaultDefinitions = defaultSettingsProvider1.GetDefaultDefinitions();

        InitializeAvailableOptions();
        _ = LoadSettingsAsync();
    }

    #endregion

    #region 可观察属性 - 状态控制

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    #endregion

    #region 可观察属性 - 托盘设置

    /// <summary>
    /// 关闭时最小化到托盘
    /// </summary>
    [ObservableProperty]
    private bool _minimizeToTrayOnClose;

    /// <summary>
    /// 显示托盘图标
    /// </summary>
    [ObservableProperty]
    private bool _showTrayIcon;

    /// <summary>
    /// 显示托盘通知
    /// </summary>
    [ObservableProperty]
    private bool _showTrayNotifications;

    #endregion

    #region 可观察属性 - UI设置

    /// <summary>
    /// 主题模式
    /// </summary>
    [ObservableProperty]
    private ThemeMode _themeMode;

    /// <summary>
    /// 主题颜色
    /// </summary>
    [ObservableProperty]
    private ThemeColor _themeColor;

    /// <summary>
    /// 字体大小
    /// </summary>
    [ObservableProperty]
    private int _fontSize = 12;

    /// <summary>
    /// 界面语言
    /// </summary>
    [ObservableProperty]
    private LanguageOption _language = new()
    {
        Code = "zh-CN",
        DisplayName = "简体中文"
    };

    #endregion

    #region 可观察属性 - 应用程序设置

    /// <summary>
    /// 开机自启动
    /// </summary>
    [ObservableProperty]
    private bool _autoStartup;

    #endregion

    #region 可用选项集合

    /// <summary>
    /// 可用的主题模式选项
    /// </summary>
    public ObservableCollection<ThemeMode> AvailableThemeModes { get; } = [];

    /// <summary>
    /// 可用的主题颜色选项
    /// </summary>
    public ObservableCollection<ThemeColor> AvailableThemeColors { get; } = [];

    /// <summary>
    /// 可用的语言选项
    /// </summary>
    public ObservableCollection<LanguageOption> AvailableLanguages { get; } = [];

    #endregion

    #region 命令 - 设置管理

    /// <summary>
    /// 保存设置命令
    /// </summary>
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        await ExecuteWithLoadingAsync("正在保存设置...", async () =>
        {
            await SaveAllSettingsToServiceAsync();
            await ShowSuccessMessageAsync("设置已保存", 2000);
            Debug.WriteLine("设置保存成功");
        });
    }

    /// <summary>
    /// 重置所有设置命令
    /// </summary>
    [RelayCommand]
    private async Task ResetAllSettingsAsync()
    {
        await ExecuteWithLoadingAsync("正在重置所有设置...", async () =>
        {
            await _settingsService.ResetToDefaultsAsync();
            await LoadSettingsAsync();
            await ShowSuccessMessageAsync("所有设置已重置为默认值", 2000);
            Debug.WriteLine("所有设置重置成功");
        });
    }

    /// <summary>
    /// 重置分类设置命令
    /// </summary>
    /// <param name="category">设置分类</param>
    [RelayCommand]
    private async Task ResetCategoryAsync(string? category)
    {
        if (string.IsNullOrEmpty(category))
            return;

        var categoryDisplayName = GetCategoryDisplayName(category);
        await ExecuteWithLoadingAsync($"正在重置{categoryDisplayName}设置...", async () =>
        {
            await _settingsService.ResetCategoryToDefaultsAsync(category);
            await LoadSettingsAsync();
            await ShowSuccessMessageAsync($"{categoryDisplayName}设置已重置", 2000);
            Debug.WriteLine($"分类 {category} 设置重置成功");
        });
    }

    #endregion

    #region 命令 - 导入导出

    /// <summary>
    /// 导出设置命令
    /// </summary>
    [RelayCommand]
    private async Task ExportSettingsAsync()
    {
        await ExecuteWithLoadingAsync("正在导出设置...", async () =>
        {
            var json = await _settingsService.ExportSettingsAsync();
            var success = await SaveSettingsToFileAsync(json);

            if (success)
            {
                await ShowSuccessMessageAsync("设置导出成功", 2000);
            }
            else
            {
                StatusMessage = string.Empty;
            }
        });
    }

    /// <summary>
    /// 导入设置命令
    /// </summary>
    [RelayCommand]
    private async Task ImportSettingsAsync()
    {
        try
        {
            var json = await LoadSettingsFromFileAsync();
            if (string.IsNullOrEmpty(json))
                return;

            await ExecuteWithLoadingAsync("正在导入设置...", async () =>
            {
                await _settingsService.ImportSettingsAsync(json);
                await LoadSettingsAsync();
                await ShowSuccessMessageAsync("设置导入成功", 2000);
            });
        }
        catch (Exception ex)
        {
            await ShowErrorMessageAsync($"导入设置失败: {ex.Message}", 3000);
            Debug.WriteLine($"导入设置失败: {ex.Message}");
        }
    }

    #endregion

    #region 命令 - 即时应用

    /// <summary>
    /// 应用主题命令
    /// </summary>
    [RelayCommand]
    private async Task ApplyThemeAsync()
    {
        try
        {
            await _settingsService.SetSettingAsync(SettingKeys.ThemeColor, ThemeColor);
            await _settingsService.SetSettingAsync(SettingKeys.ThemeMode, ThemeMode);
            Debug.WriteLine($"主题已更改为: {ThemeMode} - {ThemeColor}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用主题失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 应用字体大小命令
    /// </summary>
    [RelayCommand]
    private async Task ApplyFontSizeAsync()
    {
        try
        {
            await _settingsService.SetSettingAsync(SettingKeys.FontSize, FontSize);
            Debug.WriteLine($"字体大小已更改为: {FontSize}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用字体大小失败: {ex.Message}");
        }
    }

    #endregion

    #region 属性变更处理 - 自动保存

    /// <summary>
    /// 主题模式变更时的处理
    /// </summary>
    partial void OnThemeModeChanged(ThemeMode value)
    {
        AutoSaveSettingAsync(SettingKeys.ThemeMode, value, "主题模式");
    }

    /// <summary>
    /// 主题颜色变更时的处理
    /// </summary>
    partial void OnThemeColorChanged(ThemeColor value)
    {
        AutoSaveSettingAsync(SettingKeys.ThemeColor, value, "主题颜色");
    }

    /// <summary>
    /// 字体大小变更时的处理（带防抖）
    /// </summary>
    partial void OnFontSizeChanged(int value)
    {
        DebouncedAutoSaveSettingAsync(SettingKeys.FontSize, value, "字体大小", 500);
    }

    /// <summary>
    /// 关闭时最小化到托盘设置变更时的处理
    /// </summary>
    partial void OnMinimizeToTrayOnCloseChanged(bool value)
    {
        AutoSaveSettingAsync(SettingKeys.MinimizeToTrayOnClose, value, "关闭时最小化到托盘");
    }

    /// <summary>
    /// 显示托盘图标设置变更时的处理
    /// </summary>
    partial void OnShowTrayIconChanged(bool value)
    {
        AutoSaveSettingAsync(SettingKeys.ShowTrayIcon, value, "显示托盘图标");
    }

    /// <summary>
    /// 显示托盘通知设置变更时的处理
    /// </summary>
    partial void OnShowTrayNotificationsChanged(bool value)
    {
        AutoSaveSettingAsync(SettingKeys.ShowTrayNotifications, value, "显示托盘通知");
    }

    /// <summary>
    /// 开机自启动设置变更时的处理
    /// </summary>
    partial void OnAutoStartupChanged(bool value)
    {
        AutoSaveSettingAsync(SettingKeys.AutoStartup, value, "开机自启动");
    }

    /// <summary>
    /// 语言设置变更时的处理
    /// </summary>
    partial void OnLanguageChanged(LanguageOption value)
    {
        AutoSaveSettingAsync(SettingKeys.Language, value.Code, "界面语言");
    }

    #endregion

    #region 私有方法 - 初始化

    /// <summary>
    /// 初始化可用选项
    /// </summary>
    private void InitializeAvailableOptions()
    {
        InitializeThemeOptions();
        InitializeLanguageOptions();
    }

    /// <summary>
    /// 初始化主题选项
    /// </summary>
    private void InitializeThemeOptions()
    {
        // 初始化主题模式选项
        AvailableThemeModes.Clear();
        foreach (var mode in Enum.GetValues<ThemeMode>())
        {
            AvailableThemeModes.Add(mode);
        }

        // 初始化主题颜色选项
        AvailableThemeColors.Clear();
        foreach (var color in Enum.GetValues<ThemeColor>())
        {
            AvailableThemeColors.Add(color);
        }
    }

    /// <summary>
    /// 初始化语言选项
    /// </summary>
    private void InitializeLanguageOptions()
    {
        AvailableLanguages.Clear();
        var languages = new[]
        {
            new LanguageOption { Code = "zh-CN", DisplayName = "简体中文" },
            new LanguageOption { Code = "en-US", DisplayName = "English" },
            new LanguageOption { Code = "zh-TW", DisplayName = "繁體中文" }
        };

        foreach (var language in languages)
        {
            AvailableLanguages.Add(language);
        }
    }

    #endregion

    #region 私有方法 - 设置加载

    /// <summary>
    /// 异步加载设置
    /// </summary>
    private async Task LoadSettingsAsync()
    {
        await ExecuteWithLoadingAsync("正在加载设置...", async () =>
        {
            try
            {
                await LoadAllSettingsFromServiceAsync();
                await ShowSuccessMessageAsync("设置加载完成", 1000);
                Debug.WriteLine("设置加载成功");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载设置失败: {ex.Message}");
                LoadDefaultValues();
                await ShowErrorMessageAsync($"加载设置失败，已使用默认值: {ex.Message}", 3000);
            }
        });
    }

    /// <summary>
    /// 从服务加载所有设置
    /// </summary>
    private async Task LoadAllSettingsFromServiceAsync()
    {
        // 加载托盘设置
        MinimizeToTrayOnClose = await _settingsService.GetSettingAsync(SettingKeys.MinimizeToTrayOnClose, true);
        ShowTrayIcon = await _settingsService.GetSettingAsync(SettingKeys.ShowTrayIcon, true);
        ShowTrayNotifications = await _settingsService.GetSettingAsync(SettingKeys.ShowTrayNotifications, true);

        // 加载UI设置
        ThemeMode = await _settingsService.GetSettingAsync(SettingKeys.ThemeMode, ThemeMode.Light);
        ThemeColor = await _settingsService.GetSettingAsync(SettingKeys.ThemeColor, ThemeColor.Blue);
        FontSize = await _settingsService.GetSettingAsync(SettingKeys.FontSize, 12);

        // 加载语言设置
        await LoadLanguageSettingAsync();

        // 加载应用程序设置
        AutoStartup = await _settingsService.GetSettingAsync(SettingKeys.AutoStartup, false);
    }

    /// <summary>
    /// 加载语言设置
    /// </summary>
    private async Task LoadLanguageSettingAsync()
    {
        var languageCode = await _settingsService.GetSettingAsync(SettingKeys.Language, "zh-CN");
        var foundLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == languageCode);

        if (foundLanguage != null)
        {
            Language = foundLanguage;
        }
        else
        {
            // 如果找不到对应的语言，使用默认语言
            Language = AvailableLanguages.FirstOrDefault() ?? new LanguageOption { Code = "zh-CN", DisplayName = "简体中文" };
        }
    }

    /// <summary>
    /// 加载默认值
    /// </summary>
    private void LoadDefaultValues()
    {
        foreach (var definition in _defaultDefinitions)
        {
            ApplyDefaultValueToProperty(definition);
        }
    }

    /// <summary>
    /// 将默认值应用到属性
    /// </summary>
    /// <param name="definition">设置定义</param>
    private void ApplyDefaultValueToProperty(SettingDefinition definition)
    {
        switch (definition.Key)
        {
            case SettingKeys.ThemeMode:
                ThemeMode = (ThemeMode)(definition.DefaultValue ?? ThemeMode.Light);
                break;
            case SettingKeys.ThemeColor:
                ThemeColor = (ThemeColor)(definition.DefaultValue ?? ThemeColor.Blue);
                break;
            case SettingKeys.FontSize:
                FontSize = (int)(definition.DefaultValue ?? 12);
                break;
            case SettingKeys.Language:
                Language = CreateLanguageOptionFromCode(definition.DefaultValue?.ToString() ?? "zh-CN");
                break;
            case SettingKeys.MinimizeToTrayOnClose:
                MinimizeToTrayOnClose = (bool)(definition.DefaultValue ?? false);
                break;
            case SettingKeys.ShowTrayIcon:
                ShowTrayIcon = (bool)(definition.DefaultValue ?? false);
                break;
            case SettingKeys.ShowTrayNotifications:
                ShowTrayNotifications = (bool)(definition.DefaultValue ?? false);
                break;
            case SettingKeys.AutoStartup:
                AutoStartup = (bool)(definition.DefaultValue ?? false);
                break;
        }
    }

    /// <summary>
    /// 根据语言代码创建语言选项
    /// </summary>
    /// <param name="languageCode">语言代码</param>
    /// <returns>语言选项</returns>
    private static LanguageOption CreateLanguageOptionFromCode(string languageCode)
    {
        var displayName = languageCode switch
        {
            "zh-CN" => "简体中文",
            "en-US" => "English",
            "zh-TW" => "繁體中文",
            _ => languageCode
        };

        return new LanguageOption
        {
            Code = languageCode,
            DisplayName = displayName
        };
    }

    #endregion

    #region 私有方法 - 设置保存

    /// <summary>
    /// 保存所有设置到服务
    /// </summary>
    private async Task SaveAllSettingsToServiceAsync()
    {
        // 保存托盘设置
        await _settingsService.SetSettingAsync(SettingKeys.ShowTrayIcon, ShowTrayIcon);
        await _settingsService.SetSettingAsync(SettingKeys.ShowTrayNotifications, ShowTrayNotifications);
        await _settingsService.SetSettingAsync(SettingKeys.MinimizeToTrayOnClose, MinimizeToTrayOnClose);

        // 保存UI设置
        await _settingsService.SetSettingAsync(SettingKeys.ThemeMode, ThemeMode);
        await _settingsService.SetSettingAsync(SettingKeys.ThemeColor, ThemeColor);
        await _settingsService.SetSettingAsync(SettingKeys.FontSize, FontSize);
        await _settingsService.SetSettingAsync(SettingKeys.Language, Language.Code);

        // 保存应用程序设置
        await _settingsService.SetSettingAsync(SettingKeys.AutoStartup, AutoStartup);
    }

    /// <summary>
    /// 自动保存设置（无防抖）
    /// </summary>
    /// <typeparam name="T">设置值类型</typeparam>
    /// <param name="key">设置键</param>
    /// <param name="value">设置值</param>
    /// <param name="description">设置描述</param>
    private void AutoSaveSettingAsync<T>(string key, T value, string description)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _settingsService.SetSettingAsync(key, value);
                Debug.WriteLine($"自动保存{description}成功: {value}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"自动保存{description}失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 带防抖的自动保存设置
    /// </summary>
    /// <typeparam name="T">设置值类型</typeparam>
    /// <param name="key">设置键</param>
    /// <param name="value">设置值</param>
    /// <param name="description">设置描述</param>
    /// <param name="delayMs">防抖延迟（毫秒）</param>
    private void DebouncedAutoSaveSettingAsync<T>(string key, T value, string description, int delayMs)
    {
        // 取消之前的保存操作
        if (_debounceCancellationTokens.TryGetValue(key, out var existingCts))
        {
            existingCts.Cancel();
            existingCts.Dispose();
        }

        // 创建新的取消令牌
        var newCts = new CancellationTokenSource();
        _debounceCancellationTokens[key] = newCts;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, newCts.Token);

                if (!newCts.Token.IsCancellationRequested)
                {
                    await _settingsService.SetSettingAsync(key, value);
                    Debug.WriteLine($"防抖自动保存{description}成功: {value}");
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"防抖自动保存{description}被取消");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"防抖自动保存{description}失败: {ex.Message}");
            }
            finally
            {
                _debounceCancellationTokens.Remove(key);
                newCts.Dispose();
            }
        }, newCts.Token);
    }

    #endregion

    #region 私有方法 - 文件操作

    /// <summary>
    /// 保存设置到文件
    /// </summary>
    /// <param name="json">设置JSON字符串</param>
    /// <returns>是否成功保存</returns>
    private static async Task<bool> SaveSettingsToFileAsync(string json)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = "json",
            FileName = $"DrugSearcher_Settings_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (saveFileDialog.ShowDialog() != true)
            return false;

        await File.WriteAllTextAsync(saveFileDialog.FileName, json);
        return true;
    }

    /// <summary>
    /// 从文件加载设置
    /// </summary>
    /// <returns>设置JSON字符串，如果取消则返回null</returns>
    private static async Task<string?> LoadSettingsFromFileAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = "json"
        };

        if (openFileDialog.ShowDialog() != true)
            return null;

        return await File.ReadAllTextAsync(openFileDialog.FileName);
    }

    #endregion

    #region 私有方法 - UI辅助

    /// <summary>
    /// 在加载状态下执行操作
    /// </summary>
    /// <param name="loadingMessage">加载消息</param>
    /// <param name="action">要执行的操作</param>
    private async Task ExecuteWithLoadingAsync(string loadingMessage, Func<Task> action)
    {
        IsLoading = true;
        StatusMessage = loadingMessage;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await ShowErrorMessageAsync($"操作失败: {ex.Message}", 3000);
            Debug.WriteLine($"操作失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 显示成功消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="durationMs">显示时长（毫秒）</param>
    private async Task ShowSuccessMessageAsync(string message, int durationMs)
    {
        StatusMessage = message;
        await Task.Delay(durationMs);
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="durationMs">显示时长（毫秒）</param>
    private async Task ShowErrorMessageAsync(string message, int durationMs)
    {
        StatusMessage = message;
        await Task.Delay(durationMs);
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// 获取设置分类的显示名称
    /// </summary>
    /// <param name="category">设置分类</param>
    /// <returns>显示名称</returns>
    private static string GetCategoryDisplayName(string category)
    {
        return category switch
        {
            "Tray" => "托盘",
            "UI" => "界面",
            "Application" => "应用程序",
            _ => category
        };
    }

    #endregion
}

/// <summary>
/// 语言选项模型
/// </summary>
public class LanguageOption
{
    /// <summary>
    /// 语言代码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 返回显示名称
    /// </summary>
    /// <returns>显示名称</returns>
    public override string ToString() => DisplayName;

    /// <summary>
    /// 判断两个语言选项是否相等
    /// </summary>
    /// <param name="obj">比较对象</param>
    /// <returns>是否相等</returns>
    public override bool Equals(object? obj)
    {
        return obj is LanguageOption other && Code == other.Code;
    }

    /// <summary>
    /// 获取哈希码
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode()
    {
        return Code.GetHashCode();
    }
}