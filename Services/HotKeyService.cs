using DrugSearcher.Constants;
using DrugSearcher.Managers;
using DrugSearcher.Models;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;

namespace DrugSearcher.Services;

/// <summary>
/// 快捷键服务实现
/// </summary>
public class HotKeyService : IHotKeyService
{
    #region 私有字段

    private readonly HotKeyManager _hotKeyManager;
    private readonly IUserSettingsService _settingsService;
    private readonly Dictionary<string, int> _globalHotKeyIds; // 存储全局快捷键ID
    private readonly Dictionary<string, HotKeyInfo> _registeredHotKeys; // 存储已注册的快捷键信息
    private bool _disposed;
    private bool _isInitialized;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化快捷键服务
    /// </summary>
    /// <param name="hotKeyManager">快捷键管理器</param>
    /// <param name="settingsService">设置服务</param>
    public HotKeyService(HotKeyManager hotKeyManager, IUserSettingsService settingsService)
    {
        _hotKeyManager = hotKeyManager ?? throw new ArgumentNullException(nameof(hotKeyManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _globalHotKeyIds = [];
        _registeredHotKeys = [];

        // 监听设置变更
        _settingsService.SettingChanged += OnSettingChanged;

        Debug.WriteLine("快捷键服务初始化完成");
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 注册默认快捷键
    /// </summary>
    public async void RegisterDefaultHotKeys()
    {
        if (_isInitialized)
        {
            Debug.WriteLine("快捷键服务已初始化，跳过重复注册");
            return;
        }

        try
        {
            Debug.WriteLine("开始注册默认快捷键...");

            // 从设置中加载快捷键配置
            await RegisterHotKeyFromSetting(SettingKeys.HotKeyShowMainWindow, OnShowMainWindow, "显示主窗口", true);
            await RegisterHotKeyFromSetting(SettingKeys.HotKeyQuickSearch, OnQuickSearch, "快速搜索", true);
            await RegisterHotKeyFromSetting(SettingKeys.HotKeySearch, OnSearch, "搜索", false);
            await RegisterHotKeyFromSetting(SettingKeys.HotKeyRefresh, OnRefresh, "刷新", false);
            await RegisterHotKeyFromSetting(SettingKeys.HotKeySettings, OnSettings, "设置", false);
            await RegisterHotKeyFromSetting(SettingKeys.HotKeyExit, OnExit, "退出", false);

            _isInitialized = true;
            Debug.WriteLine("默认快捷键注册完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"注册默认快捷键失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从设置中注册快捷键
    /// </summary>
    private async Task RegisterHotKeyFromSetting(string settingKey, Action callback, string description, bool isGlobal)
    {
        try
        {
            var hotKeyJson = await _settingsService.GetSettingAsync<string>(settingKey);
            if (string.IsNullOrEmpty(hotKeyJson))
            {
                Debug.WriteLine($"快捷键设置为空: {settingKey}");
                return;
            }

            var hotKeySetting = JsonSerializer.Deserialize<HotKeySetting>(hotKeyJson);
            if (hotKeySetting == null)
            {
                Debug.WriteLine($"快捷键设置解析失败: {settingKey}");
                return;
            }

            if (!hotKeySetting.IsEnabled)
            {
                Debug.WriteLine($"快捷键已禁用: {settingKey}");
                return;
            }

            // 检查快捷键是否已被其他设置项注册
            if (IsHotKeyRegisteredByOtherSetting(settingKey, hotKeySetting))
            {
                Debug.WriteLine($"快捷键冲突，跳过注册: {settingKey} - {hotKeySetting}");
                return;
            }

            if (isGlobal)
            {
                var hotKeyId = _hotKeyManager.RegisterGlobalHotKey(hotKeySetting.Key, hotKeySetting.Modifiers, callback, description);
                if (hotKeyId != -1)
                {
                    _globalHotKeyIds[settingKey] = hotKeyId;

                    // 记录注册的快捷键信息
                    _registeredHotKeys[settingKey] = new HotKeyInfo
                    {
                        Id = hotKeyId,
                        Name = settingKey,
                        Key = hotKeySetting.Key,
                        Modifiers = hotKeySetting.Modifiers,
                        Description = description,
                        IsGlobal = true,
                        IsEnabled = true,
                        Callback = callback
                    };

                    Debug.WriteLine($"全局快捷键注册成功: {settingKey} - {hotKeySetting}");
                }
                else
                {
                    Debug.WriteLine($"全局快捷键注册失败: {settingKey} - {hotKeySetting}");
                }
            }
            else
            {
                var success = _hotKeyManager.RegisterLocalHotKey(settingKey, hotKeySetting.Key, hotKeySetting.Modifiers, callback, description);
                if (success)
                {
                    // 记录注册的快捷键信息
                    _registeredHotKeys[settingKey] = new HotKeyInfo
                    {
                        Id = 0, // 局部快捷键不需要ID
                        Name = settingKey,
                        Key = hotKeySetting.Key,
                        Modifiers = hotKeySetting.Modifiers,
                        Description = description,
                        IsGlobal = false,
                        IsEnabled = true,
                        Callback = callback
                    };

                    Debug.WriteLine($"局部快捷键注册成功: {settingKey} - {hotKeySetting}");
                }
                else
                {
                    Debug.WriteLine($"局部快捷键注册失败: {settingKey} - {hotKeySetting}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"注册快捷键失败: {settingKey}, {ex.Message}");
        }
    }

    /// <summary>
    /// 检查快捷键是否已被其他设置项注册
    /// </summary>
    private bool IsHotKeyRegisteredByOtherSetting(string currentSettingKey, HotKeySetting hotKeySetting)
    {
        foreach (var kvp in _registeredHotKeys)
        {
            if (kvp.Key != currentSettingKey &&
                kvp.Value.Key == hotKeySetting.Key &&
                kvp.Value.Modifiers == hotKeySetting.Modifiers)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 更新快捷键注册
    /// </summary>
    private async Task UpdateHotKeyRegistration(string settingKey, Action callback, string description, bool isGlobal)
    {
        try
        {
            Debug.WriteLine($"更新快捷键注册: {settingKey}");

            // 先注销现有的快捷键
            if (isGlobal)
            {
                if (_globalHotKeyIds.TryGetValue(settingKey, out var oldId))
                {
                    var success = _hotKeyManager.UnregisterGlobalHotKey(oldId);
                    if (success)
                    {
                        _globalHotKeyIds.Remove(settingKey);
                        Debug.WriteLine($"全局快捷键注销成功: {settingKey} (ID: {oldId})");
                    }
                    else
                    {
                        Debug.WriteLine($"全局快捷键注销失败: {settingKey} (ID: {oldId})");
                    }
                }
            }
            else
            {
                var success = _hotKeyManager.UnregisterLocalHotKey(settingKey);
                if (success)
                {
                    Debug.WriteLine($"局部快捷键注销成功: {settingKey}");
                }
                else
                {
                    Debug.WriteLine($"局部快捷键注销失败: {settingKey}");
                }
            }

            // 从记录中移除
            _registeredHotKeys.Remove(settingKey);

            // 重新注册
            await RegisterHotKeyFromSetting(settingKey, callback, description, isGlobal);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新快捷键注册失败: {settingKey}, {ex.Message}");
        }
    }

    /// <summary>
    /// 注册全局快捷键
    /// </summary>
    public int RegisterGlobalHotKey(Key key, ModifierKeys modifiers, Action callback, string description = "") => _hotKeyManager.RegisterGlobalHotKey(key, modifiers, callback, description);

    /// <summary>
    /// 注册局部快捷键
    /// </summary>
    public bool RegisterLocalHotKey(string name, Key key, ModifierKeys modifiers, Action callback, string description = "") => _hotKeyManager.RegisterLocalHotKey(name, key, modifiers, callback, description);

    /// <summary>
    /// 注销全局快捷键
    /// </summary>
    public bool UnregisterGlobalHotKey(int hotKeyId) => _hotKeyManager.UnregisterGlobalHotKey(hotKeyId);

    /// <summary>
    /// 注销局部快捷键
    /// </summary>
    public bool UnregisterLocalHotKey(string name) => _hotKeyManager.UnregisterLocalHotKey(name);

    /// <summary>
    /// 启用/禁用局部快捷键
    /// </summary>
    public bool SetLocalHotKeyEnabled(string name, bool enabled) => _hotKeyManager.SetLocalHotKeyEnabled(name, enabled);

    /// <summary>
    /// 获取所有快捷键
    /// </summary>
    public IEnumerable<HotKeyInfo> GetAllHotKeys()
    {
        var allHotKeys = new List<HotKeyInfo>();

        // 添加全局快捷键
        var globalHotKeys = _hotKeyManager.GetGlobalHotKeys();
        if (globalHotKeys != null)
        {
            allHotKeys.AddRange(globalHotKeys.Values);
        }

        // 添加局部快捷键
        var localHotKeys = _hotKeyManager.GetLocalHotKeys();
        if (localHotKeys != null)
        {
            allHotKeys.AddRange(localHotKeys.Values);
        }

        return allHotKeys;
    }

    /// <summary>
    /// 检查快捷键是否已被注册
    /// </summary>
    public bool IsHotKeyRegistered(Key key, ModifierKeys modifiers) => _hotKeyManager.IsHotKeyRegistered(key, modifiers);

    /// <summary>
    /// 获取已注册的快捷键信息
    /// </summary>
    public IReadOnlyDictionary<string, HotKeyInfo> GetRegisteredHotKeys() => _registeredHotKeys.AsReadOnly();

    /// <summary>
    /// 重新加载所有快捷键
    /// </summary>
    public async Task ReloadAllHotKeysAsync()
    {
        try
        {
            Debug.WriteLine("重新加载所有快捷键...");

            // 清除所有现有注册
            await ClearAllRegistrationsAsync();

            // 重新注册所有快捷键
            _isInitialized = false;
            RegisterDefaultHotKeys();

            Debug.WriteLine("快捷键重新加载完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"重新加载快捷键失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清除所有注册
    /// </summary>
    private Task ClearAllRegistrationsAsync()
    {
        try
        {
            // 注销所有全局快捷键
            foreach (var kvp in from kvp in _globalHotKeyIds.ToList() let success = _hotKeyManager.UnregisterGlobalHotKey(kvp.Value) where success select kvp)
            {
                Debug.WriteLine($"清除全局快捷键: {kvp.Key}");
            }
            _globalHotKeyIds.Clear();

            // 注销所有局部快捷键
            foreach (var kvp in from kvp in _registeredHotKeys.ToList() where !kvp.Value.IsGlobal let success = _hotKeyManager.UnregisterLocalHotKey(kvp.Key) where success select kvp)
            {
                Debug.WriteLine($"清除局部快捷键: {kvp.Key}");
            }

            _registeredHotKeys.Clear();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清除快捷键注册失败: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    #endregion

    #region 设置变更处理

    /// <summary>
    /// 处理设置变更
    /// </summary>
    private async void OnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        // 检查是否是快捷键设置
        var hotKeyActions = GetHotKeyActionMappings();

        if (hotKeyActions.TryGetValue(e.Key, out var actionInfo))
        {
            Debug.WriteLine($"检测到快捷键设置变更: {e.Key}");
            await UpdateHotKeyRegistration(e.Key, actionInfo.callback, actionInfo.description, actionInfo.isGlobal);
        }
    }

    /// <summary>
    /// 获取快捷键动作映射
    /// </summary>
    private Dictionary<string, (Action callback, string description, bool isGlobal)> GetHotKeyActionMappings() => new()
    {
            { SettingKeys.HotKeyShowMainWindow, (OnShowMainWindow, "显示主窗口", true) },
            { SettingKeys.HotKeyQuickSearch, (OnQuickSearch, "快速搜索", true) },
            { SettingKeys.HotKeySearch, (OnSearch, "搜索", false) },
            { SettingKeys.HotKeyRefresh, (OnRefresh, "刷新", false) },
            { SettingKeys.HotKeySettings, (OnSettings, "设置", false) },
            { SettingKeys.HotKeyExit, (OnExit, "退出", false) }
        };

    #endregion

    #region 私有方法 - 快捷键回调

    /// <summary>
    /// 显示主窗口
    /// </summary>
    private void OnShowMainWindow()
    {
        Debug.WriteLine("快捷键触发: 显示主窗口");
        try
        {
            ShowMainWindowRequested?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"显示主窗口快捷键回调异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 快速搜索
    /// </summary>
    private void OnQuickSearch()
    {
        Debug.WriteLine("快捷键触发: 快速搜索");
        try
        {
            QuickSearchRequested?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"快速搜索快捷键回调异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 搜索
    /// </summary>
    private void OnSearch()
    {
        Debug.WriteLine("快捷键触发: 搜索");
        try
        {
            SearchRequested?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"搜索快捷键回调异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 刷新
    /// </summary>
    private void OnRefresh()
    {
        Debug.WriteLine("快捷键触发: 刷新");
        try
        {
            RefreshRequested?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"刷新快捷键回调异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 设置
    /// </summary>
    private void OnSettings()
    {
        Debug.WriteLine("快捷键触发: 设置");
        try
        {
            SettingsRequested?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"设置快捷键回调异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 退出
    /// </summary>
    private void OnExit()
    {
        Debug.WriteLine("快捷键触发: 退出");
        try
        {
            ExitRequested?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"退出快捷键回调异常: {ex.Message}");
        }
    }

    #endregion

    #region 事件

    /// <summary>
    /// 显示主窗口请求事件
    /// </summary>
    public event Action? ShowMainWindowRequested;

    /// <summary>
    /// 快速搜索请求事件
    /// </summary>
    public event Action? QuickSearchRequested;

    /// <summary>
    /// 搜索请求事件
    /// </summary>
    public event Action? SearchRequested;

    /// <summary>
    /// 刷新请求事件
    /// </summary>
    public event Action? RefreshRequested;

    /// <summary>
    /// 设置请求事件
    /// </summary>
    public event Action? SettingsRequested;

    /// <summary>
    /// 退出请求事件
    /// </summary>
    public event Action? ExitRequested;

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
            Debug.WriteLine("正在释放快捷键服务...");

            // 取消设置变更监听
            _settingsService.SettingChanged -= OnSettingChanged;

            // 清除所有快捷键注册
            _ = ClearAllRegistrationsAsync();

            // 清空事件订阅
            ShowMainWindowRequested = null;
            QuickSearchRequested = null;
            SearchRequested = null;
            RefreshRequested = null;
            SettingsRequested = null;
            ExitRequested = null;

            // 释放热键管理器
            _hotKeyManager?.Dispose();

            _disposed = true;
            Debug.WriteLine("快捷键服务已释放");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放快捷键服务异常: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 析构函数
    /// </summary>
    ~HotKeyService()
    {
        Dispose();
    }

    #endregion
}