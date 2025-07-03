using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Common.Enums;
using DrugSearcher.Common.Helpers;
using DrugSearcher.Managers;
using DrugSearcher.Models;
using System.ComponentModel;
using System.Diagnostics;

namespace DrugSearcher.ViewModels;

/// <summary>
/// 主窗口视图模型，管理主窗口的UI状态和用户交互
/// 包括主题切换、按钮图标更新、颜色选择等功能
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    #region 私有字段

    private readonly ThemeManager _themeManager;
    private bool _disposed;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化主窗口视图模型
    /// </summary>
    /// <param name="themeManager">主题管理器</param>
    public MainWindowViewModel(ThemeManager themeManager)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));

        InitializeColorSelectionStates();
        SubscribeToThemeEvents();
        InitializeUIElements();
    }

    #endregion

    #region 可观察属性 - 窗口标题

    /// <summary>
    /// 应用程序标题
    /// </summary>
    [ObservableProperty]
    private string _title = "药物查询器";

    #endregion

    #region 可观察属性 - 按钮图标

    /// <summary>
    /// 主题切换按钮图标
    /// </summary>
    [ObservableProperty]
    private string _themeButtonText = "\ue611";

    /// <summary>
    /// 首页按钮图标
    /// </summary>
    [ObservableProperty]
    private string _homeButtonText = "\ue61a";

    /// <summary>
    /// 设置按钮图标
    /// </summary>
    [ObservableProperty]
    private string _settingsButtonText = "\ue61c";

    #endregion

    #region 可观察属性 - 工具提示

    /// <summary>
    /// 主题按钮工具提示
    /// </summary>
    [ObservableProperty]
    private string _themeTooltip = "当前主题：浅色";

    #endregion

    #region 可观察属性 - 主题状态

    /// <summary>
    /// 各颜色主题的选择状态
    /// </summary>
    [ObservableProperty]
    private Dictionary<ThemeColor, bool> _colorSelectionStates = [];

    /// <summary>
    /// 当前主题模式的显示名称
    /// </summary>
    [ObservableProperty]
    private string _currentThemeModeName = "浅色";

    /// <summary>
    /// 当前主题颜色的显示名称
    /// </summary>
    [ObservableProperty]
    private string _currentThemeColorName = "蓝色";

    #endregion

    #region 公共属性

    /// <summary>
    /// 可用的主题模式列表
    /// </summary>
    public static IEnumerable<ThemeMode> AvailableThemeModes => Enum.GetValues<ThemeMode>();

    /// <summary>
    /// 可用的主题颜色列表
    /// </summary>
    public static IEnumerable<ThemeColor> AvailableThemeColors => Enum.GetValues<ThemeColor>();

    /// <summary>
    /// 当前主题配置
    /// </summary>
    public ThemeConfig CurrentTheme => _themeManager.CurrentTheme;

    #endregion

    #region 命令 - 主题切换

    /// <summary>
    /// 切换主题模式命令
    /// </summary>
    [RelayCommand]
    private void SwitchTheme()
    {
        try
        {
            var nextTheme = GetNextThemeMode(_themeManager.CurrentTheme.Mode);

            Debug.WriteLine($"切换主题模式: {_themeManager.CurrentTheme.Mode} -> {nextTheme}");
            _themeManager.ApplyThemeMode(nextTheme);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"切换主题模式失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 切换主题颜色命令
    /// </summary>
    /// <param name="newColor">新的主题颜色</param>
    [RelayCommand]
    private void SwitchThemeColor(object? newColor)
    {
        try
        {
            if (newColor == null)
            {
                Debug.WriteLine("主题颜色参数为空");
                return;
            }

            if (TryParseThemeColor(newColor, out var themeColor))
            {
                Debug.WriteLine($"切换主题颜色: {_themeManager.CurrentTheme.Color} -> {themeColor}");
                _themeManager.ApplyThemeColor(themeColor);
            }
            else
            {
                Debug.WriteLine($"无效的主题颜色参数: {newColor}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"切换主题颜色失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 应用指定主题模式命令
    /// </summary>
    /// <param name="mode">主题模式</param>
    [RelayCommand]
    private void ApplyThemeMode(ThemeMode mode)
    {
        try
        {
            Debug.WriteLine($"应用主题模式: {mode}");
            _themeManager.ApplyThemeMode(mode);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用主题模式失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 应用指定主题颜色命令
    /// </summary>
    /// <param name="color">主题颜色</param>
    [RelayCommand]
    private void ApplyThemeColor(ThemeColor color)
    {
        try
        {
            Debug.WriteLine($"应用主题颜色: {color}");
            _themeManager.ApplyThemeColor(color);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用主题颜色失败: {ex.Message}");
        }
    }

    #endregion

    #region 私有方法 - 初始化

    /// <summary>
    /// 初始化颜色选择状态
    /// </summary>
    private void InitializeColorSelectionStates()
    {
        try
        {
            ColorSelectionStates.Clear();

            foreach (var color in Enum.GetValues<ThemeColor>())
            {
                ColorSelectionStates[color] = _themeManager.CurrentTheme.Color == color;
            }

            Debug.WriteLine($"初始化颜色选择状态完成，当前选中: {_themeManager.CurrentTheme.Color}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"初始化颜色选择状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 订阅主题相关事件
    /// </summary>
    private void SubscribeToThemeEvents()
    {
        try
        {
            // 监听主题管理器的变化
            _themeManager.PropertyChanged += OnThemeManagerPropertyChanged;

            // 监听系统主题变化
            SystemThemeHelper.SystemThemeChanged += OnSystemThemeChanged;

            Debug.WriteLine("主题事件订阅完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"订阅主题事件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 初始化UI元素
    /// </summary>
    private void InitializeUIElements()
    {
        try
        {
            UpdateButtonIcons();
            UpdateThemeTooltip();
            UpdateThemeDisplayNames();

            Debug.WriteLine("UI元素初始化完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"初始化UI元素失败: {ex.Message}");
        }
    }

    #endregion

    #region 私有方法 - 事件处理

    /// <summary>
    /// 处理主题管理器属性变化事件
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void OnThemeManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            if (e.PropertyName == nameof(ThemeManager.CurrentTheme))
            {
                Debug.WriteLine($"主题配置已变更: {_themeManager.CurrentTheme}");

                UpdateButtonIcons();
                UpdateThemeTooltip();
                UpdateThemeDisplayNames();
                UpdateColorSelectionStates();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理主题管理器属性变化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理系统主题变化事件
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        try
        {
            Debug.WriteLine("系统主题已变化，更新UI元素");

            UpdateButtonIcons();
            UpdateThemeTooltip();
            UpdateThemeDisplayNames();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理系统主题变化失败: {ex.Message}");
        }
    }

    #endregion

    #region 私有方法 - UI更新

    /// <summary>
    /// 更新按钮图标
    /// </summary>
    private void UpdateButtonIcons()
    {
        try
        {
            var currentMode = _themeManager.CurrentTheme.Mode;
            var isSystemDark = SystemThemeHelper.IsSystemDarkTheme();

            ThemeButtonText = GetThemeButtonIcon(currentMode, isSystemDark);
            HomeButtonText = GetHomeButtonIcon(currentMode, isSystemDark);
            SettingsButtonText = GetSettingsButtonIcon(currentMode, isSystemDark);

            Debug.WriteLine($"按钮图标已更新 - 模式: {currentMode}, 系统深色: {isSystemDark}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新按钮图标失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新主题工具提示
    /// </summary>
    private void UpdateThemeTooltip()
    {
        try
        {
            var themeModeName = GetThemeModeDisplayName(_themeManager.CurrentTheme.Mode);
            var themeColorName = GetThemeColorDisplayName(_themeManager.CurrentTheme.Color);

            ThemeTooltip = $"当前主题：{themeModeName} - {themeColorName}";

            Debug.WriteLine($"主题工具提示已更新: {ThemeTooltip}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新主题工具提示失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新主题显示名称
    /// </summary>
    private void UpdateThemeDisplayNames()
    {
        try
        {
            CurrentThemeModeName = GetThemeModeDisplayName(_themeManager.CurrentTheme.Mode);
            CurrentThemeColorName = GetThemeColorDisplayName(_themeManager.CurrentTheme.Color);

            Debug.WriteLine($"主题显示名称已更新: {CurrentThemeModeName} - {CurrentThemeColorName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新主题显示名称失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新颜色选择状态
    /// </summary>
    private void UpdateColorSelectionStates()
    {
        try
        {
            var currentColor = _themeManager.CurrentTheme.Color;

            foreach (var color in Enum.GetValues<ThemeColor>())
            {
                ColorSelectionStates[color] = color == currentColor;
            }

            // 触发属性变更通知
            OnPropertyChanged(nameof(ColorSelectionStates));

            Debug.WriteLine($"颜色选择状态已更新，当前选中: {currentColor}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新颜色选择状态失败: {ex.Message}");
        }
    }

    #endregion

    #region 私有方法 - 图标获取

    /// <summary>
    /// 获取主题按钮图标
    /// </summary>
    /// <param name="mode">主题模式</param>
    /// <param name="isSystemDark">系统是否为深色主题</param>
    /// <returns>图标字符</returns>
    private static string GetThemeButtonIcon(ThemeMode mode, bool isSystemDark)
    {
        return mode switch
        {
            ThemeMode.Light => "\ue611",      // 太阳符号
            ThemeMode.Dark => "\ue616",       // 月亮符号
            ThemeMode.System => isSystemDark ? "\ue60b" : "\ue610", // 自动/设置符号
            _ => "\ue611"
        };
    }

    /// <summary>
    /// 获取首页按钮图标
    /// </summary>
    /// <param name="mode">主题模式</param>
    /// <param name="isSystemDark">系统是否为深色主题</param>
    /// <returns>图标字符</returns>
    private static string GetHomeButtonIcon(ThemeMode mode, bool isSystemDark)
    {
        return mode switch
        {
            ThemeMode.Light => "\ue61a",      // 家符号
            ThemeMode.Dark => "\ue619",       // 家符号（深色）
            ThemeMode.System => isSystemDark ? "\ue619" : "\ue61a", // 家符号（跟随系统）
            _ => "\ue61a"
        };
    }

    /// <summary>
    /// 获取设置按钮图标
    /// </summary>
    /// <param name="mode">主题模式</param>
    /// <param name="isSystemDark">系统是否为深色主题</param>
    /// <returns>图标字符</returns>
    private static string GetSettingsButtonIcon(ThemeMode mode, bool isSystemDark)
    {
        return mode switch
        {
            ThemeMode.Light => "\ue61c",      // 设置符号
            ThemeMode.Dark => "\ue61b",       // 设置符号（深色）
            ThemeMode.System => isSystemDark ? "\ue61b" : "\ue61c", // 设置符号（跟随系统）
            _ => "\ue61c"
        };
    }

    #endregion

    #region 私有方法 - 显示名称获取

    /// <summary>
    /// 获取主题模式的显示名称
    /// </summary>
    /// <param name="mode">主题模式</param>
    /// <returns>显示名称</returns>
    private static string GetThemeModeDisplayName(ThemeMode mode)
    {
        return mode switch
        {
            ThemeMode.Light => "浅色",
            ThemeMode.Dark => "深色",
            ThemeMode.System => "跟随系统",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取主题颜色的显示名称
    /// </summary>
    /// <param name="color">主题颜色</param>
    /// <returns>显示名称</returns>
    private static string GetThemeColorDisplayName(ThemeColor color)
    {
        return color switch
        {
            ThemeColor.Blue => "蓝色",
            ThemeColor.Green => "绿色",
            ThemeColor.Purple => "紫色",
            ThemeColor.Red => "红色",
            ThemeColor.Orange => "橙色",
            ThemeColor.Pink => "粉色",
            _ => color.ToString()
        };
    }

    #endregion

    #region 私有方法 - 辅助功能

    /// <summary>
    /// 获取下一个主题模式
    /// </summary>
    /// <param name="currentMode">当前主题模式</param>
    /// <returns>下一个主题模式</returns>
    private static ThemeMode GetNextThemeMode(ThemeMode currentMode)
    {
        return currentMode switch
        {
            ThemeMode.Light => ThemeMode.Dark,
            ThemeMode.Dark => ThemeMode.System,
            ThemeMode.System => ThemeMode.Light,
            _ => ThemeMode.Light
        };
    }

    /// <summary>
    /// 尝试解析主题颜色
    /// </summary>
    /// <param name="colorObject">颜色对象</param>
    /// <param name="themeColor">解析出的主题颜色</param>
    /// <returns>是否解析成功</returns>
    private static bool TryParseThemeColor(object colorObject, out ThemeColor themeColor)
    {
        // 如果已经是ThemeColor类型，直接使用
        if (colorObject is ThemeColor directColor)
        {
            themeColor = directColor;
            return true;
        }

        // 尝试从字符串解析
        var colorString = colorObject.ToString();
        if (!string.IsNullOrEmpty(colorString))
        {
            return Enum.TryParse(colorString, true, out themeColor);
        }

        themeColor = default;
        return false;
    }

    /// <summary>
    /// 取消订阅主题相关事件
    /// </summary>
    private void UnsubscribeFromThemeEvents()
    {
        try
        {
            _themeManager.PropertyChanged -= OnThemeManagerPropertyChanged;
            SystemThemeHelper.SystemThemeChanged -= OnSystemThemeChanged;

            Debug.WriteLine("主题事件订阅已取消");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"取消订阅主题事件失败: {ex.Message}");
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            UnsubscribeFromThemeEvents();
            _disposed = true;

            Debug.WriteLine("MainWindowViewModel 资源已释放");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放 MainWindowViewModel 资源失败: {ex.Message}");
        }
    }

    #endregion
}