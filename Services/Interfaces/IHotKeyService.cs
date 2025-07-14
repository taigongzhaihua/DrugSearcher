using System.Windows.Input;
using DrugSearcher.Models;

namespace DrugSearcher.Services;

/// <summary>
/// 快捷键服务接口
/// </summary>
public interface IHotKeyService : IDisposable
{
    #region 方法

    /// <summary>
    /// 注册默认快捷键
    /// </summary>
    void RegisterDefaultHotKeys();

    /// <summary>
    /// 注册全局快捷键
    /// </summary>
    int RegisterGlobalHotKey(Key key, ModifierKeys modifiers, Action callback, string description = "");

    /// <summary>
    /// 注册局部快捷键
    /// </summary>
    bool RegisterLocalHotKey(string name, Key key, ModifierKeys modifiers, Action callback, string description = "");

    /// <summary>
    /// 注销全局快捷键
    /// </summary>
    bool UnregisterGlobalHotKey(int hotKeyId);

    /// <summary>
    /// 注销局部快捷键
    /// </summary>
    bool UnregisterLocalHotKey(string name);

    /// <summary>
    /// 启用/禁用局部快捷键
    /// </summary>
    bool SetLocalHotKeyEnabled(string name, bool enabled);

    /// <summary>
    /// 获取所有快捷键
    /// </summary>
    IEnumerable<HotKeyInfo> GetAllHotKeys();

    /// <summary>
    /// 检查快捷键是否已被注册
    /// </summary>
    bool IsHotKeyRegistered(Key key, ModifierKeys modifiers);

    #endregion

    #region 事件

    /// <summary>
    /// 显示主窗口请求事件
    /// </summary>
    event Action? ShowMainWindowRequested;

    /// <summary>
    /// 快速搜索请求事件
    /// </summary>
    event Action? QuickSearchRequested;

    /// <summary>
    /// 搜索请求事件
    /// </summary>
    event Action? SearchRequested;

    /// <summary>
    /// 刷新请求事件
    /// </summary>
    event Action? RefreshRequested;

    /// <summary>
    /// 设置请求事件
    /// </summary>
    event Action? SettingsRequested;

    /// <summary>
    /// 退出请求事件
    /// </summary>
    event Action? ExitRequested;

    #endregion
}