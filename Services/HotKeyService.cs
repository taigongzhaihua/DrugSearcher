using DrugSearcher.Managers;
using DrugSearcher.Models;
using DrugSearcher.Services.Interfaces;
using System.Diagnostics;
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
    private bool _disposed;

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

        Debug.WriteLine("快捷键服务初始化完成");
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 注册默认快捷键
    /// </summary>
    public void RegisterDefaultHotKeys()
    {
        try
        {
            // 注册全局快捷键
            RegisterGlobalHotKey(Key.F1, ModifierKeys.Alt,
                OnShowMainWindow, "显示主窗口");

            RegisterGlobalHotKey(Key.F2, ModifierKeys.Control | ModifierKeys.Alt,
                OnQuickSearch, "快速搜索");

            // 注册局部快捷键
            RegisterLocalHotKey("Search", Key.F, ModifierKeys.Control,
                OnSearch, "搜索");

            RegisterLocalHotKey("Refresh", Key.F5, ModifierKeys.None,
                OnRefresh, "刷新");

            RegisterLocalHotKey("Settings", Key.S, ModifierKeys.Control,
                OnSettings, "设置");

            RegisterLocalHotKey("Exit", Key.Q, ModifierKeys.Control,
                OnExit, "退出");

            Debug.WriteLine("默认快捷键注册完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"注册默认快捷键失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 注册全局快捷键
    /// </summary>
    public int RegisterGlobalHotKey(Key key, ModifierKeys modifiers, Action callback, string description = "")
    {
        return _hotKeyManager.RegisterGlobalHotKey(key, modifiers, callback, description);
    }

    /// <summary>
    /// 注册局部快捷键
    /// </summary>
    public bool RegisterLocalHotKey(string name, Key key, ModifierKeys modifiers, Action callback, string description = "")
    {
        return _hotKeyManager.RegisterLocalHotKey(name, key, modifiers, callback, description);
    }

    /// <summary>
    /// 注销全局快捷键
    /// </summary>
    public bool UnregisterGlobalHotKey(int hotKeyId)
    {
        return _hotKeyManager.UnregisterGlobalHotKey(hotKeyId);
    }

    /// <summary>
    /// 注销局部快捷键
    /// </summary>
    public bool UnregisterLocalHotKey(string name)
    {
        return _hotKeyManager.UnregisterLocalHotKey(name);
    }

    /// <summary>
    /// 启用/禁用局部快捷键
    /// </summary>
    public bool SetLocalHotKeyEnabled(string name, bool enabled)
    {
        return _hotKeyManager.SetLocalHotKeyEnabled(name, enabled);
    }

    /// <summary>
    /// 获取所有快捷键
    /// </summary>
    public IEnumerable<HotKeyInfo> GetAllHotKeys()
    {
        var allHotKeys = new List<HotKeyInfo>();

        // 添加全局快捷键
        allHotKeys.AddRange(_hotKeyManager.GetGlobalHotKeys().Values);

        // 添加局部快捷键
        allHotKeys.AddRange(_hotKeyManager.GetLocalHotKeys().Values);

        return allHotKeys;
    }

    /// <summary>
    /// 检查快捷键是否已被注册
    /// </summary>
    public bool IsHotKeyRegistered(Key key, ModifierKeys modifiers)
    {
        return _hotKeyManager.IsHotKeyRegistered(key, modifiers);
    }

    #endregion

    #region 私有方法 - 快捷键回调

    /// <summary>
    /// 显示主窗口
    /// </summary>
    private void OnShowMainWindow()
    {
        Debug.WriteLine("快捷键触发: 显示主窗口");
        ShowMainWindowRequested?.Invoke();
    }

    /// <summary>
    /// 快速搜索
    /// </summary>
    private void OnQuickSearch()
    {
        Debug.WriteLine("快捷键触发: 快速搜索");
        QuickSearchRequested?.Invoke();
    }

    /// <summary>
    /// 搜索
    /// </summary>
    private void OnSearch()
    {
        Debug.WriteLine("快捷键触发: 搜索");
        SearchRequested?.Invoke();
    }

    /// <summary>
    /// 刷新
    /// </summary>
    private void OnRefresh()
    {
        Debug.WriteLine("快捷键触发: 刷新");
        RefreshRequested?.Invoke();
    }

    /// <summary>
    /// 设置
    /// </summary>
    private void OnSettings()
    {
        Debug.WriteLine("快捷键触发: 设置");
        SettingsRequested?.Invoke();
    }

    /// <summary>
    /// 退出
    /// </summary>
    private void OnExit()
    {
        Debug.WriteLine("快捷键触发: 退出");
        ExitRequested?.Invoke();
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
            // 清空事件订阅
            ShowMainWindowRequested = null;
            QuickSearchRequested = null;
            SearchRequested = null;
            RefreshRequested = null;
            SettingsRequested = null;
            ExitRequested = null;

            _hotKeyManager.Dispose();
            _disposed = true;
            Debug.WriteLine("快捷键服务已释放");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放快捷键服务异常: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}