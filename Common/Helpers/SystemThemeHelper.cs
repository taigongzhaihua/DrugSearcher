using Microsoft.Win32;
using System.Diagnostics;

namespace DrugSearcher.Helpers;

/// <summary>
/// 系统主题帮助类，用于检测和监听Windows系统主题变化
/// 支持检测明暗主题模式和系统主题变更事件
/// </summary>
public static class SystemThemeHelper
{
    #region 常量定义

    /// <summary>
    /// 系统主题注册表键路径
    /// </summary>
    private const string REGISTRY_KEY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>
    /// 应用主题注册表值名称
    /// </summary>
    private const string REGISTRY_VALUE_NAME = "AppsUseLightTheme";

    /// <summary>
    /// 系统主题注册表值名称（用于系统级主题检测）
    /// </summary>
    private const string SYSTEM_REGISTRY_VALUE_NAME = "SystemUsesLightTheme";

    /// <summary>
    /// 浅色主题注册表值
    /// </summary>
    private const int LIGHT_THEME_VALUE = 1;

    /// <summary>
    /// 深色主题注册表值
    /// </summary>
    private const int DARK_THEME_VALUE = 0;

    #endregion

    #region 私有字段

    /// <summary>
    /// 系统事件处理器实例
    /// </summary>
    private static SystemEventHandler? _systemEventHandler;

    /// <summary>
    /// 是否正在监听系统主题变化
    /// </summary>
    private static bool _isMonitoring;

    /// <summary>
    /// 上次检测到的主题状态（用于避免重复触发事件）
    /// </summary>
    private static bool? _lastKnownDarkThemeState;

    #endregion

    #region 事件

    /// <summary>
    /// 系统主题变化事件
    /// </summary>
    public static event EventHandler? SystemThemeChanged;

    #endregion

    #region 公共方法

    /// <summary>
    /// 检测系统是否使用深色主题
    /// </summary>
    /// <returns>如果系统使用深色主题则返回true，否则返回false</returns>
    public static bool IsSystemDarkTheme()
    {
        try
        {
            // 优先检查应用主题设置
            var appThemeIsDark = IsAppThemeDark();
            if (appThemeIsDark.HasValue)
            {
                Debug.WriteLine($"检测到应用主题: {(appThemeIsDark.Value ? "深色" : "浅色")}");
                return appThemeIsDark.Value;
            }

            // 如果应用主题设置不可用，检查系统主题设置
            var systemThemeIsDark = IsSystemThemeDark();
            if (systemThemeIsDark.HasValue)
            {
                Debug.WriteLine($"检测到系统主题: {(systemThemeIsDark.Value ? "深色" : "浅色")}");
                return systemThemeIsDark.Value;
            }

            Debug.WriteLine("无法检测主题，默认返回浅色主题");
            return false; // 默认返回浅色主题
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检测系统主题时出错: {ex.Message}");
            return false; // 发生异常时默认返回浅色主题
        }
    }

    /// <summary>
    /// 检测系统是否使用浅色主题
    /// </summary>
    /// <returns>如果系统使用浅色主题则返回true，否则返回false</returns>
    public static bool IsSystemLightTheme() => !IsSystemDarkTheme();

    /// <summary>
    /// 获取当前系统主题的详细信息
    /// </summary>
    /// <returns>包含主题信息的结构</returns>
    public static SystemThemeInfo GetSystemThemeInfo()
    {
        try
        {
            var appThemeIsDark = IsAppThemeDark();
            var systemThemeIsDark = IsSystemThemeDark();

            return new SystemThemeInfo
            {
                IsAppThemeDark = appThemeIsDark,
                IsSystemThemeDark = systemThemeIsDark,
                IsDetectionSuccessful = appThemeIsDark.HasValue || systemThemeIsDark.HasValue,
                EffectiveThemeIsDark = IsSystemDarkTheme()
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取系统主题信息时出错: {ex.Message}");
            return new SystemThemeInfo
            {
                IsAppThemeDark = null,
                IsSystemThemeDark = null,
                IsDetectionSuccessful = false,
                EffectiveThemeIsDark = false
            };
        }
    }

    /// <summary>
    /// 开始监听系统主题变化
    /// </summary>
    public static void StartMonitoring()
    {
        if (_isMonitoring)
        {
            Debug.WriteLine("系统主题监听已在运行中");
            return;
        }

        try
        {
            Debug.WriteLine("开始监听系统主题变化...");

            _systemEventHandler = new SystemEventHandler();
            _lastKnownDarkThemeState = IsSystemDarkTheme();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _isMonitoring = true;

            Debug.WriteLine($"系统主题监听已启动，当前主题: {(_lastKnownDarkThemeState.Value ? "深色" : "浅色")}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动系统主题监听失败: {ex.Message}");
            _isMonitoring = false;
            _systemEventHandler = null;
        }
    }

    /// <summary>
    /// 停止监听系统主题变化
    /// </summary>
    public static void StopMonitoring()
    {
        if (!_isMonitoring)
        {
            Debug.WriteLine("系统主题监听未在运行");
            return;
        }

        try
        {
            Debug.WriteLine("停止系统主题监听...");

            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _systemEventHandler = null;
            _isMonitoring = false;
            _lastKnownDarkThemeState = null;

            Debug.WriteLine("系统主题监听已停止");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"停止系统主题监听失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取监听状态
    /// </summary>
    /// <returns>如果正在监听则返回true</returns>
    public static bool IsMonitoring => _isMonitoring;

    /// <summary>
    /// 手动触发主题检测（用于测试或强制刷新）
    /// </summary>
    public static void RefreshThemeState()
    {
        try
        {
            Debug.WriteLine("手动刷新主题状态...");
            CheckAndNotifyThemeChange();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"手动刷新主题状态失败: {ex.Message}");
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 检测应用主题是否为深色
    /// </summary>
    /// <returns>如果是深色主题返回true，浅色主题返回false，无法检测返回null</returns>
    private static bool? IsAppThemeDark() => GetThemeValueFromRegistry(REGISTRY_VALUE_NAME);

    /// <summary>
    /// 检测系统主题是否为深色
    /// </summary>
    /// <returns>如果是深色主题返回true，浅色主题返回false，无法检测返回null</returns>
    private static bool? IsSystemThemeDark() => GetThemeValueFromRegistry(SYSTEM_REGISTRY_VALUE_NAME);

    /// <summary>
    /// 从注册表获取主题值
    /// </summary>
    /// <param name="valueName">注册表值名称</param>
    /// <returns>如果是深色主题返回true，浅色主题返回false，无法检测返回null</returns>
    private static bool? GetThemeValueFromRegistry(string valueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY_PATH);
            if (key == null)
            {
                Debug.WriteLine($"无法打开注册表键: {REGISTRY_KEY_PATH}");
                return null;
            }

            var registryValueObject = key.GetValue(valueName);
            switch (registryValueObject)
            {
                case null:
                    Debug.WriteLine($"注册表值不存在: {valueName}");
                    return null;
                case int registryValue:
                    {
                        // 0表示深色主题，1表示浅色主题
                        var isDark = registryValue == DARK_THEME_VALUE;
                        Debug.WriteLine($"注册表值 {valueName}: {registryValue} ({(isDark ? "深色" : "浅色")})");
                        return isDark;
                    }
                default:
                    Debug.WriteLine($"注册表值类型不正确: {registryValueObject.GetType()}");
                    return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"读取注册表值 {valueName} 时出错: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 检查主题变化并通知
    /// </summary>
    private static void CheckAndNotifyThemeChange()
    {
        try
        {
            var currentDarkThemeState = IsSystemDarkTheme();

            // 只有在主题真正发生变化时才触发事件
            if (_lastKnownDarkThemeState.HasValue &&
                _lastKnownDarkThemeState.Value != currentDarkThemeState)
            {
                Debug.WriteLine($"检测到主题变化: {(_lastKnownDarkThemeState.Value ? "深色" : "浅色")} -> {(currentDarkThemeState ? "深色" : "浅色")}");

                _lastKnownDarkThemeState = currentDarkThemeState;
                OnSystemThemeChanged();
            }
            else if (!_lastKnownDarkThemeState.HasValue)
            {
                // 首次检测，记录状态但不触发事件
                _lastKnownDarkThemeState = currentDarkThemeState;
                Debug.WriteLine($"首次检测主题状态: {(currentDarkThemeState ? "深色" : "浅色")}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检查主题变化时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 触发系统主题变化事件
    /// </summary>
    private static void OnSystemThemeChanged()
    {
        try
        {
            Debug.WriteLine("触发系统主题变化事件");
            SystemThemeChanged?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"触发系统主题变化事件时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理用户偏好设置变化事件
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        try
        {
            // 检查是否为主题相关的变化
            if (IsThemeRelatedPreferenceChange(e.Category))
            {
                Debug.WriteLine($"检测到用户偏好设置变化: {e.Category}");

                // 延迟一段时间再检查，确保注册表已更新
                Task.Delay(100).ContinueWith(_ => CheckAndNotifyThemeChange());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理用户偏好设置变化时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查是否为主题相关的偏好设置变化
    /// </summary>
    /// <param name="category">用户偏好设置类别</param>
    /// <returns>如果是主题相关的变化则返回true</returns>
    private static bool IsThemeRelatedPreferenceChange(UserPreferenceCategory category) => category is UserPreferenceCategory.General or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Color;

    #endregion

    #region 内部类和结构

    /// <summary>
    /// 系统主题信息结构
    /// </summary>
    public readonly struct SystemThemeInfo
    {
        /// <summary>
        /// 应用主题是否为深色（如果无法检测则为null）
        /// </summary>
        public bool? IsAppThemeDark { get; init; }

        /// <summary>
        /// 系统主题是否为深色（如果无法检测则为null）
        /// </summary>
        public bool? IsSystemThemeDark { get; init; }

        /// <summary>
        /// 是否成功检测到主题信息
        /// </summary>
        public bool IsDetectionSuccessful { get; init; }

        /// <summary>
        /// 最终生效的主题是否为深色
        /// </summary>
        public bool EffectiveThemeIsDark { get; init; }

        /// <summary>
        /// 获取主题信息的字符串表示
        /// </summary>
        /// <returns>主题信息字符串</returns>
        public readonly override string ToString() => $"App: {(IsAppThemeDark?.ToString() ?? "Unknown")}, " +
                                                      $"System: {(IsSystemThemeDark?.ToString() ?? "Unknown")}, " +
                                                      $"Effective: {(EffectiveThemeIsDark ? "Dark" : "Light")}, " +
                                                      $"Success: {IsDetectionSuccessful}";
    }

    /// <summary>
    /// 系统事件处理器（用于管理事件订阅的生命周期）
    /// </summary>
    private sealed class SystemEventHandler
    {
        /// <summary>
        /// 创建时间戳（用于调试）
        /// </summary>
        public DateTime CreatedAt { get; } = DateTime.Now;

        /// <summary>
        /// 获取处理器信息的字符串表示
        /// </summary>
        /// <returns>处理器信息字符串</returns>
        public override string ToString() => $"SystemEventHandler created at {CreatedAt:yyyy-MM-dd HH:mm:ss}";
    }

    #endregion
}