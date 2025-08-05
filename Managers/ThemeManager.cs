using CommunityToolkit.Mvvm.ComponentModel;
using DrugSearcher.Constants;
using DrugSearcher.Enums;
using DrugSearcher.Helpers;
using DrugSearcher.Models;
using DrugSearcher.Services;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ThemeMode = DrugSearcher.Enums.ThemeMode;

namespace DrugSearcher.Managers;

/// <summary>
/// 主题管理器，负责应用主题的加载、切换和管理
/// 支持明暗模式、自定义颜色主题以及系统主题自动跟随
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public partial class ThemeManager : ObservableObject, IThemeService, IDisposable
{
    #region 字段和属性

    /// <summary>
    /// 当前主题配置
    /// </summary>
    [ObservableProperty]
    public partial ThemeConfig CurrentTheme { get; set; } = new(ThemeMode.Light, ThemeColor.Blue);

    /// <summary>
    /// 缓存所有颜色资源字典，避免重复加载
    /// </summary>
    private readonly Dictionary<ThemeColor, ResourceDictionary> _colorDictionaries = [];

    /// <summary>
    /// 用户设置服务
    /// </summary>
    private readonly IUserSettingsService _settingsService;

    /// <summary>
    /// 是否已释放资源
    /// </summary>
    private bool _disposed;

    private readonly HashSet<WeakReference> _registeredWindows = [];
    private readonly Lock _windowsLock = new();

    /// <summary>
    /// 是否启用Windows 11边框控制
    /// </summary>
    public bool IsWindowBorderControlEnabled { get; private set; } = true;

    #endregion

    #region 事件

    /// <summary>
    /// 主题变更事件
    /// </summary>
    public event EventHandler<ThemeConfig>? ThemeChanged;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化主题管理器
    /// </summary>
    /// <param name="settingsService">用户设置服务</param>
    public ThemeManager(IUserSettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        SubscribeToSystemThemeChanges();
        SubscribeToSettingsChanges();
    }

    #endregion

    #region 初始化方法

    /// <summary>
    /// 初始化主题管理器
    /// </summary>
    // 在Initialize()方法中添加
    public void Initialize()
    {
        try
        {
            InitializeResourceCache();

            _ = LoadAndApplyThemeFromSettingsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"主题管理器初始化失败: {ex.Message}");
            ApplyDefaultTheme();
        }
    }

    /// <summary>
    /// 初始化资源字典缓存
    /// </summary>
    private void InitializeResourceCache()
    {
        if (IsCacheInitialized) return;

        Debug.WriteLine("开始初始化主题资源缓存...");

        try
        {
            var loadedCount = 0;

            // 预加载所有颜色主题
            foreach (var color in Enum.GetValues<ThemeColor>())
            {
                var colorDict = LoadColorResourceDictionary(color);
                if (colorDict != null)
                {
                    _colorDictionaries[color] = colorDict;
                    loadedCount++;
                    Debug.WriteLine($"已缓存颜色主题: {color}");
                }
                else
                {
                    Debug.WriteLine($"警告: 无法加载颜色主题 {color}");
                }
            }

            IsCacheInitialized = true;
            Debug.WriteLine($"主题资源缓存初始化完成 - 成功加载 {loadedCount}/{Enum.GetValues<ThemeColor>().Length} 个颜色主题");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"初始化主题资源缓存失败: {ex.Message}");
            IsCacheInitialized = false;
        }
    }

    /// <summary>
    /// 应用默认主题
    /// </summary>
    private void ApplyDefaultTheme()
    {
        var defaultTheme = new ThemeConfig(ThemeMode.Light, ThemeColor.Blue);
        ApplyTheme(defaultTheme);
    }

    #endregion

    #region 窗口注册方法

    /// <summary>
    /// 注册窗口以进行边框颜色管理
    /// </summary>
    /// <param name="window"> 要注册的窗口</param>
    /// <summary>
    /// 注册窗口以进行边框和标题栏颜色管理
    /// </summary>
    public void RegisterWindow(Window? window)
    {
        if (window == null || !IsWindowBorderControlEnabled)
            return;

        lock (_windowsLock)
        {
            _registeredWindows.Add(new WeakReference(window));

            // 立即应用当前主题的窗口颜色
            ApplyWindowColorsToWindow(window);
        }

        Debug.WriteLine($"已注册窗口进行边框和标题栏管理: {window.GetType().Name}");
    }

    /// <summary>
    /// 注销窗口的颜色管理
    /// </summary>
    /// <param name="window">要注销的窗口</param>
    public void UnregisterWindow(Window? window)
    {
        if (window == null)
            return;

        lock (_windowsLock)
        {
            var toRemove = _registeredWindows
                .Where(wr => wr.Target == window)
                .ToList();

            foreach (var wr in toRemove)
            {
                _registeredWindows.Remove(wr);
            }
        }

        // 重置为系统默认颜色
        var result = WindowColorManager.ResetWindowColors(window);

        Debug.WriteLine($"窗口 {window.GetType().Name} 已注销颜色管理，重置结果: {result.Message}");
    }

    #endregion

    #region 主题颜色提取和应用方法

    // 添加颜色提取方法
    /// <summary>
    /// 从当前主题资源中获取PrimaryBrush颜色
    /// </summary>
    /// <returns>主题色彩，如果无法获取则返回默认蓝色</returns>
    private static Color GetThemeColor(string resourceKey)
    {
        try
        {
            var appResources = Application.Current.Resources;

            if (!appResources.Contains(resourceKey) ||
                appResources[resourceKey] is not Brush brush ||
                brush.GetValue(SolidColorBrush.ColorProperty) is not Color color) return Colors.DodgerBlue;
            Debug.WriteLine($"已提取主题色彩: {color}");
            return color;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取主题色彩失败: {ex.Message}");
            return Colors.DodgerBlue;
        }
    }

    /// <summary>
    /// 应用边框和标题栏颜色到指定窗口
    /// </summary>
    /// <param name="window">目标窗口</param>
    private void ApplyWindowColorsToWindow(Window? window)
    {
        if (window == null || !IsWindowBorderControlEnabled)
            return;

        try
        {
            var themeColor = GetThemeColor("BackgroundBrush");


            var result = WindowColorManager.SetWindowColors(window, themeColor, themeColor);

            if (result.IsFullySuccessful)
            {
                Debug.WriteLine($"已为窗口 {window.GetType().Name} 设置边框和标题栏颜色: 边框={themeColor}, 标题栏={themeColor}");
            }
            else if (result.IsPartiallySuccessful)
            {
                Debug.WriteLine($"为窗口 {window.GetType().Name} 部分设置颜色成功: {result.Message}");
            }
            else
            {
                Debug.WriteLine($"为窗口 {window.GetType().Name} 设置颜色失败: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用窗口颜色失败: {ex.Message}");
        }
    }


    /// <summary>
    /// 更新所有已注册窗口的边框颜色
    /// </summary>
    private void UpdateAllWindowColors()
    {
        if (!IsWindowBorderControlEnabled)
            return;

        lock (_windowsLock)
        {
            var aliveWindows = new List<Window?>();
            var deadReferences = new List<WeakReference>();

            // 收集存活的窗口和死亡的引用
            foreach (var windowRef in _registeredWindows)
            {
                if (windowRef.Target is Window { IsLoaded: true } window)
                {
                    aliveWindows.Add(window);
                }
                else
                {
                    deadReferences.Add(windowRef);
                }
            }

            // 清理死亡的引用
            foreach (var deadRef in deadReferences)
            {
                _registeredWindows.Remove(deadRef);
            }

            // 更新存活窗口的颜色
            foreach (var window in aliveWindows)
            {
                ApplyWindowColorsToWindow(window);
            }

            Debug.WriteLine($"已更新 {aliveWindows.Count} 个窗口的边框和标题栏颜色");
        }
    }

    #endregion

    #region 事件订阅和处理

    /// <summary>
    /// 订阅系统主题变化事件
    /// </summary>
    private void SubscribeToSystemThemeChanges()
    {
        SystemThemeHelper.SystemThemeChanged += OnSystemThemeChanged;
        SystemThemeHelper.StartMonitoring();
    }

    /// <summary>
    /// 订阅设置变化事件
    /// </summary>
    private void SubscribeToSettingsChanges()
    {
        _settingsService.SettingChanged += OnSettingChanged;
        _settingsService.SettingsReloaded += OnSettingsReloaded;
    }

    /// <summary>
    /// 处理设置重新加载事件
    /// </summary>
    private async void OnSettingsReloaded(object? sender, EventArgs e)
    {
        try
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await LoadAndApplyThemeFromSettingsAsync();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理设置重新加载事件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理单个设置变化事件
    /// </summary>
    [SuppressMessage("ReSharper", "AsyncVoidMethod")]
    private async void OnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        if (!IsThemeRelatedSetting(e.Key) || IsSettingValueUnchanged(e))
            return;

        try
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                Debug.WriteLine($"主题设置已变更: {e.Key} = {e.NewValue}");
                await LoadAndApplyThemeFromSettingsAsync();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理设置变化事件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理系统主题变化事件
    /// </summary>
    [SuppressMessage("ReSharper", "AsyncVoidMethod")]
    private async void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (CurrentTheme.Mode != ThemeMode.System)
            return;

        try
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Debug.WriteLine("系统主题已变化，正在更新应用主题...");
                ApplyTheme(CurrentTheme);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理系统主题变化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查是否为主题相关设置
    /// </summary>
    private static bool IsThemeRelatedSetting(string key) => key is SettingKeys.THEME_MODE or SettingKeys.THEME_COLOR;

    /// <summary>
    /// 检查设置值是否未发生变化
    /// </summary>
    private static bool IsSettingValueUnchanged(SettingChangedEventArgs e) =>
        e.NewValue?.ToString() == e.OldValue?.ToString();

    #endregion

    #region 公共主题应用方法

    /// <summary>
    /// 应用完整的主题配置
    /// </summary>
    /// <param name="themeConfig">主题配置</param>
    public void ApplyTheme(ThemeConfig themeConfig)
    {
        try
        {
            EnsureCacheInitialized();

            var resourceDictionaries = GetThemeResourceDictionaries(themeConfig);
            if (resourceDictionaries?.Count > 0)
            {
                UpdateApplicationResources(resourceDictionaries);
                UpdateCurrentTheme(themeConfig);
                _ = SaveThemeToSettingsAsync(themeConfig);

                OnThemeChanged(themeConfig);
                Debug.WriteLine($"已应用主题: {themeConfig}");
            }
            else
            {
                Debug.WriteLine($"警告: 无法获取主题资源 {themeConfig}");
                throw new InvalidOperationException($"无法加载主题资源: {themeConfig}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用主题失败: {ex.Message}");
            // 如果应用主题失败，尝试应用默认主题
            if (!themeConfig.Equals(new ThemeConfig(ThemeMode.Light, ThemeColor.Blue)))
            {
                ApplyDefaultTheme();
            }
        }
    }

    /// <summary>
    /// 只改变明暗模式，保持当前颜色
    /// </summary>
    /// <param name="mode">主题模式</param>
    public void ApplyThemeMode(ThemeMode mode)
    {
        var newConfig = CurrentTheme with { Mode = mode };
        ApplyTheme(newConfig);
    }

    /// <summary>
    /// 只改变颜色，保持当前明暗模式
    /// </summary>
    /// <param name="color">主题颜色</param>
    public void ApplyThemeColor(ThemeColor color)
    {
        var newConfig = CurrentTheme with { Color = color };
        ApplyTheme(newConfig);
    }

    #endregion

    #region 手动控制边框颜色

    /// <summary>
    /// 立即更新所有窗口边框和标题栏为当前主题色
    /// </summary>
    public void RefreshAllWindowColors()
    {
        UpdateAllWindowColors();
    }

    /// <summary>
    /// 手动设置指定窗口的边框和标题栏颜色
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <param name="borderColor">边框颜色</param>
    /// <param name="captionColor">标题栏颜色，如果为null则使用边框颜色</param>
    /// <returns>设置结果</returns>
    public WindowColorResult SetWindowColors(Window? window, Color borderColor, Color? captionColor = null)
    {
        return !IsWindowBorderControlEnabled
            ? new WindowColorResult(false, false, "None", "不支持的系统版本")
            : WindowColorManager.SetWindowColors(window, borderColor, captionColor);
    }

    /// <summary>
    /// 手动设置指定窗口使用当前主题颜色
    /// </summary>
    /// <param name="window">目标窗口</param>
    /// <returns>设置结果</returns>
    public WindowColorResult ApplyCurrentThemeToWindow(Window? window)
    {
        if (!IsWindowBorderControlEnabled)
            return new WindowColorResult(false, false, "None", "不支持的系统版本");

        try
        {
            var color = GetThemeColor("BackgroundBrush");


            return WindowColorManager.SetWindowColors(window, color, color);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用当前主题到窗口失败: {ex.Message}");
            return new WindowColorResult(false, false, "None", ex.Message);
        }
    }

    #endregion

    #region 资源加载和管理方法

    /// <summary>
    /// 确保缓存已初始化
    /// </summary>
    private void EnsureCacheInitialized()
    {
        if (IsCacheInitialized) return;
        Debug.WriteLine("警告: 资源缓存未初始化，正在初始化...");
        InitializeResourceCache();
    }

    /// <summary>
    /// 获取主题资源字典列表
    /// </summary>
    /// <param name="themeConfig">主题配置</param>
    /// <returns>资源字典列表</returns>
    private List<ResourceDictionary>? GetThemeResourceDictionaries(ThemeConfig themeConfig)
    {
        try
        {
            var dictionaries = new List<ResourceDictionary>();

            // 获取颜色资源字典
            var colorDict = GetColorResourceDictionary(themeConfig.Color);
            if (colorDict != null)
            {
                dictionaries.Add(colorDict);
            }

            // 获取模式资源字典
            var modeDict = GetModeResourceDictionary(themeConfig.Mode);
            if (modeDict != null)
            {
                dictionaries.Add(modeDict);
            }

            return dictionaries.Count > 0 ? dictionaries : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取主题资源字典失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 从缓存获取颜色资源字典
    /// </summary>
    /// <param name="color">主题颜色</param>
    /// <returns>颜色资源字典</returns>
    private ResourceDictionary? GetColorResourceDictionary(ThemeColor color)
    {
        if (_colorDictionaries.TryGetValue(color, out var colorDict))
        {
            return colorDict;
        }

        Debug.WriteLine($"警告: 缓存中未找到颜色主题 {color}");

        // 尝试重新加载
        var newColorDict = LoadColorResourceDictionary(color);
        if (newColorDict != null)
        {
            _colorDictionaries[color] = newColorDict;
            return newColorDict;
        }

        return null;
    }

    /// <summary>
    /// 获取模式资源字典
    /// </summary>
    /// <param name="mode">主题模式</param>
    /// <returns>模式资源字典</returns>
    private static ResourceDictionary? GetModeResourceDictionary(ThemeMode mode)
    {
        var actualMode = GetActualThemeMode(mode);
        var modeDict = LoadModeResourceDictionary(actualMode);

        if (modeDict == null)
        {
            Debug.WriteLine($"警告: 无法加载模式主题 {actualMode}");
        }

        return modeDict;
    }

    /// <summary>
    /// 加载颜色资源字典
    /// </summary>
    /// <param name="color">主题颜色</param>
    /// <returns>颜色资源字典</returns>
    private static ResourceDictionary? LoadColorResourceDictionary(ThemeColor color)
    {
        var colorSource = $"/Resources/Colors/{color}Colors.xaml";
        return LoadResourceDictionary(colorSource, $"颜色主题 {color}");
    }

    /// <summary>
    /// 加载明暗模式资源字典
    /// </summary>
    /// <param name="mode">主题模式</param>
    /// <returns>模式资源字典</returns>
    private static ResourceDictionary? LoadModeResourceDictionary(ThemeMode mode)
    {
        var modeSource = mode switch
        {
            ThemeMode.Light => "/Resources/Themes/LightTheme.xaml",
            ThemeMode.Dark => "/Resources/Themes/DarkTheme.xaml",
            _ => "/Resources/Themes/LightTheme.xaml"
        };
        return LoadResourceDictionary(modeSource, $"模式主题 {mode}");
    }

    /// <summary>
    /// 通用资源字典加载方法
    /// </summary>
    /// <param name="source">资源路径</param>
    /// <param name="description">资源描述</param>
    /// <returns>资源字典</returns>
    private static ResourceDictionary? LoadResourceDictionary(string source, string description)
    {
        try
        {
            var dict = new ResourceDictionary
            {
                Source = new Uri(source, UriKind.Relative)
            };

            // 尝试访问字典以验证它是否真的加载成功
            _ = dict.Count;

            return dict;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载 {description} 资源失败 ({source}): {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取实际的主题模式（处理System模式）
    /// </summary>
    /// <param name="mode">原始主题模式</param>
    /// <returns>实际主题模式</returns>
    private static ThemeMode GetActualThemeMode(ThemeMode mode) => mode == ThemeMode.System
        ? (SystemThemeHelper.IsSystemDarkTheme() ? ThemeMode.Dark : ThemeMode.Light)
        : mode;

    /// <summary>
    /// 更新应用程序资源
    /// </summary>
    /// <param name="newThemeDictionaries">新的主题资源字典列表</param>
    private static void UpdateApplicationResources(List<ResourceDictionary> newThemeDictionaries)
    {
        try
        {
            var appResources = Application.Current.Resources;

            // 移除所有旧的主题资源字典
            RemoveOldThemeResources(appResources);

            // 添加新的主题资源字典（按顺序：先颜色，后模式）
            AddNewThemeResources(appResources, newThemeDictionaries);

            Debug.WriteLine($"已更新应用程序资源，包含 {newThemeDictionaries.Count} 个主题字典");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新应用程序资源失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 移除旧的主题资源
    /// </summary>
    /// <param name="appResources">应用程序资源</param>
    private static void RemoveOldThemeResources(ResourceDictionary appResources)
    {
        var oldThemeDictionaries = appResources.MergedDictionaries
            .Where(IsThemeResourceDictionary)
            .ToList();

        foreach (var oldDict in oldThemeDictionaries)
        {
            appResources.MergedDictionaries.Remove(oldDict);
        }

        Debug.WriteLine($"已移除 {oldThemeDictionaries.Count} 个旧主题资源字典");
    }

    /// <summary>
    /// 添加新的主题资源
    /// </summary>
    /// <param name="appResources">应用程序资源</param>
    /// <param name="newThemeDictionaries">新主题资源字典列表</param>
    private static void AddNewThemeResources(ResourceDictionary appResources,
        List<ResourceDictionary> newThemeDictionaries)
    {
        foreach (var newDict in newThemeDictionaries)
        {
            appResources.MergedDictionaries.Insert(0, newDict);
        }
    }

    /// <summary>
    /// 判断是否为主题相关的资源字典
    /// </summary>
    /// <param name="dict">资源字典</param>
    /// <returns>如果是主题资源字典则返回true</returns>
    private static bool IsThemeResourceDictionary(ResourceDictionary dict)
    {
        var source = dict.Source?.OriginalString;
        return source?.Contains("/Themes/") == true || source?.Contains("/Colors/") == true;
    }

    #endregion

    #region 设置加载和保存方法

    /// <summary>
    /// 从设置加载并应用主题
    /// </summary>
    private async Task LoadAndApplyThemeFromSettingsAsync()
    {
        try
        {
            var themeConfig = await LoadThemeFromSettingsAsync();
            ApplyTheme(themeConfig);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"从设置加载并应用主题失败: {ex.Message}");
            ApplyDefaultTheme();
        }
    }

    /// <summary>
    /// 从设置中加载主题配置
    /// </summary>
    /// <returns>主题配置</returns>
    private async Task<ThemeConfig> LoadThemeFromSettingsAsync()
    {
        try
        {
            var themeMode = await _settingsService.GetSettingAsync(SettingKeys.THEME_MODE, ThemeMode.Light);
            var themeColor = await _settingsService.GetSettingAsync(SettingKeys.THEME_COLOR, ThemeColor.Blue);

            var themeConfig = new ThemeConfig(themeMode, themeColor);
            Debug.WriteLine($"从设置加载主题: {themeConfig}");

            return themeConfig;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"从设置加载主题失败: {ex.Message}");
            return new ThemeConfig(ThemeMode.Light, ThemeColor.Blue);
        }
    }

    /// <summary>
    /// 保存主题配置到设置
    /// </summary>
    /// <param name="themeConfig">主题配置</param>
    private async Task SaveThemeToSettingsAsync(ThemeConfig themeConfig)
    {
        try
        {
            await _settingsService.SetSettingAsync(SettingKeys.THEME_MODE, themeConfig.Mode);
            await _settingsService.SetSettingAsync(SettingKeys.THEME_COLOR, themeConfig.Color);

            Debug.WriteLine($"已保存主题设置: {themeConfig}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"保存主题设置失败: {ex.Message}");
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 更新当前主题
    /// </summary>
    /// <param name="themeConfig">新的主题配置</param>
    private void UpdateCurrentTheme(ThemeConfig themeConfig) => CurrentTheme = themeConfig;

    /// <summary>
    /// 触发主题变更事件
    /// </summary>
    /// <param name="themeConfig">新的主题配置</param>
    // 修改OnThemeChanged方法
    private void OnThemeChanged(ThemeConfig themeConfig)
    {
        try
        {
            ThemeChanged?.Invoke(this, themeConfig);

            // 更新所有窗口的边框和标题栏颜色
            UpdateAllWindowColors();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"触发主题变更事件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 取消订阅系统主题变化事件
    /// </summary>
    private void UnsubscribeFromSystemThemeChanges()
    {
        SystemThemeHelper.SystemThemeChanged -= OnSystemThemeChanged;
        SystemThemeHelper.StopMonitoring();
    }

    /// <summary>
    /// 取消订阅设置变化事件
    /// </summary>
    private void UnsubscribeFromSettingsChanges()
    {
        _settingsService.SettingChanged -= OnSettingChanged;
        _settingsService.SettingsReloaded -= OnSettingsReloaded;
    }

    #endregion

    #region 公共缓存管理方法

    /// <summary>
    /// 清除缓存（如果需要重新加载资源）
    /// </summary>
    public void ClearCache()
    {
        try
        {
            _colorDictionaries.Clear();
            IsCacheInitialized = false;
            Debug.WriteLine("主题资源缓存已清除");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清除主题资源缓存失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取颜色缓存统计信息
    /// </summary>
    /// <returns>已缓存的颜色主题数量</returns>
    public int GetColorCacheStats() => _colorDictionaries.Count;

    /// <summary>
    /// 获取缓存初始化状态
    /// </summary>
    /// <returns>如果缓存已初始化则返回true</returns>
    public bool IsCacheInitialized { get; private set; }

    #endregion

    #region IDisposable

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放资源的具体实现
    /// </summary>
    /// <param name="disposing">是否正在释放托管资源</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            try
            {
                // 清理窗口注册并重置颜色
                lock (_windowsLock)
                {
                    foreach (var windowRef in _registeredWindows)
                    {
                        if (windowRef.Target is Window window)
                        {
                            WindowColorManager.ResetWindowColors(window);
                        }
                    }

                    _registeredWindows.Clear();
                }

                // 其他现有的清理代码...
                UnsubscribeFromSystemThemeChanges();
                UnsubscribeFromSettingsChanges();
                ClearCache();

                Debug.WriteLine("ThemeManager 已释放资源");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"释放 ThemeManager 资源失败: {ex.Message}");
            }
        }

        _disposed = true;
    }

    #endregion
}