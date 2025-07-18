using DrugSearcher.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DrugSearcher.Managers;

/// <summary>
/// 快捷键管理器，支持全局和局部快捷键注册
/// </summary>
public partial class HotKeyManager : IDisposable
{
    #region Win32 API 声明

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int vKey);

    #endregion

    #region 常量定义

    private const int WmHotkey = 0x0312;
    private const int MaxHotkeyId = 0xBFFF;

    #endregion

    #region 私有字段

    private readonly Dictionary<int, HotKeyInfo> _globalHotKeys = [];
    private readonly Dictionary<string, LocalHotKeyInfo> _localHotKeys = [];
    private readonly Window _ownerWindow;
    private readonly WindowInteropHelper _interopHelper;
    private readonly HwndSource? _hwndSource;
    private int _nextHotKeyId = 1;
    private bool _disposed;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化快捷键管理器
    /// </summary>
    /// <param name="ownerWindow">拥有者窗口</param>
    public HotKeyManager(Window ownerWindow)
    {
        _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
        _interopHelper = new WindowInteropHelper(_ownerWindow);

        try
        {
            _hwndSource = HwndSource.FromHwnd(_interopHelper.Handle);
            _hwndSource?.AddHook(WndProc);

            // 注册窗口键盘事件
            _ownerWindow.KeyDown += OnWindowKeyDown;
            _ownerWindow.KeyUp += OnWindowKeyUp;

            Debug.WriteLine("快捷键管理器初始化完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"快捷键管理器初始化失败: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region 公共方法 - 全局快捷键

    /// <summary>
    /// 注册全局快捷键
    /// </summary>
    /// <param name="key">键值</param>
    /// <param name="modifiers">修饰键</param>
    /// <param name="callback">回调函数</param>
    /// <param name="description">描述</param>
    /// <returns>快捷键ID，失败返回-1</returns>
    public int RegisterGlobalHotKey(Key key, ModifierKeys modifiers, Action callback, string description = "")
    {
        ArgumentNullException.ThrowIfNull(callback);

        try
        {
            var hotKeyId = GetNextHotKeyId();
            var vkCode = KeyInterop.VirtualKeyFromKey(key);
            var modifierFlags = ConvertToWin32Modifiers(modifiers);

            var success = RegisterHotKey(_interopHelper.Handle, hotKeyId, modifierFlags, (uint)vkCode);

            if (success)
            {
                var hotKeyInfo = new HotKeyInfo
                {
                    Id = hotKeyId,
                    Key = key,
                    Modifiers = modifiers,
                    Callback = callback,
                    Description = description,
                    IsGlobal = true
                };

                _globalHotKeys[hotKeyId] = hotKeyInfo;

                Debug.WriteLine($"全局快捷键注册成功: {GetHotKeyDisplayName(key, modifiers)} (ID: {hotKeyId})");
                return hotKeyId;
            }
            else
            {
                Debug.WriteLine($"全局快捷键注册失败: {GetHotKeyDisplayName(key, modifiers)}");
                return -1;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"注册全局快捷键异常: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// 注销全局快捷键
    /// </summary>
    /// <param name="hotKeyId">快捷键ID</param>
    /// <returns>是否成功注销</returns>
    public bool UnregisterGlobalHotKey(int hotKeyId)
    {
        try
        {
            if (!_globalHotKeys.TryGetValue(hotKeyId, out var hotKeyInfo))
            {
                Debug.WriteLine($"未找到快捷键ID: {hotKeyId}");
                return false;
            }

            var success = UnregisterHotKey(_interopHelper.Handle, hotKeyId);

            if (success)
            {
                _globalHotKeys.Remove(hotKeyId);
                Debug.WriteLine($"全局快捷键注销成功: {GetHotKeyDisplayName(hotKeyInfo.Key, hotKeyInfo.Modifiers)}");
            }
            else
            {
                Debug.WriteLine($"全局快捷键注销失败: {GetHotKeyDisplayName(hotKeyInfo.Key, hotKeyInfo.Modifiers)}");
            }

            return success;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"注销全局快捷键异常: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region 公共方法 - 局部快捷键

    /// <summary>
    /// 注册局部快捷键
    /// </summary>
    /// <param name="name">快捷键名称</param>
    /// <param name="key">键值</param>
    /// <param name="modifiers">修饰键</param>
    /// <param name="callback">回调函数</param>
    /// <param name="description">描述</param>
    /// <returns>是否注册成功</returns>
    public bool RegisterLocalHotKey(string name, Key key, ModifierKeys modifiers, Action callback, string description = "")
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        ArgumentNullException.ThrowIfNull(callback);

        try
        {
            if (_localHotKeys.ContainsKey(name))
            {
                Debug.WriteLine($"局部快捷键名称已存在: {name}");
                return false;
            }

            var hotKeyInfo = new LocalHotKeyInfo
            {
                Name = name,
                Key = key,
                Modifiers = modifiers,
                Callback = callback,
                Description = description,
                IsEnabled = true
            };

            _localHotKeys[name] = hotKeyInfo;

            Debug.WriteLine($"局部快捷键注册成功: {name} - {GetHotKeyDisplayName(key, modifiers)}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"注册局部快捷键异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 注销局部快捷键
    /// </summary>
    /// <param name="name">快捷键名称</param>
    /// <returns>是否成功注销</returns>
    public bool UnregisterLocalHotKey(string name)
    {
        try
        {
            if (_localHotKeys.Remove(name))
            {
                Debug.WriteLine($"局部快捷键注销成功: {name}");
                return true;
            }
            else
            {
                Debug.WriteLine($"未找到局部快捷键: {name}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"注销局部快捷键异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 启用/禁用局部快捷键
    /// </summary>
    /// <param name="name">快捷键名称</param>
    /// <param name="enabled">是否启用</param>
    /// <returns>是否操作成功</returns>
    public bool SetLocalHotKeyEnabled(string name, bool enabled)
    {
        try
        {
            if (_localHotKeys.TryGetValue(name, out var hotKeyInfo))
            {
                hotKeyInfo.IsEnabled = enabled;
                Debug.WriteLine($"局部快捷键 {name} 已{(enabled ? "启用" : "禁用")}");
                return true;
            }
            else
            {
                Debug.WriteLine($"未找到局部快捷键: {name}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"设置局部快捷键状态异常: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region 公共方法 - 查询

    /// <summary>
    /// 获取所有已注册的全局快捷键
    /// </summary>
    /// <returns>全局快捷键列表</returns>
    public IReadOnlyDictionary<int, HotKeyInfo> GetGlobalHotKeys() => _globalHotKeys;

    /// <summary>
    /// 获取所有已注册的局部快捷键
    /// </summary>
    /// <returns>局部快捷键列表</returns>
    public IReadOnlyDictionary<string, LocalHotKeyInfo> GetLocalHotKeys() => _localHotKeys;

    /// <summary>
    /// 检查快捷键是否已被注册
    /// </summary>
    /// <param name="key">键值</param>
    /// <param name="modifiers">修饰键</param>
    /// <returns>是否已被注册</returns>
    public bool IsHotKeyRegistered(Key key, ModifierKeys modifiers)
    {
        // 检查全局快捷键
        foreach (var hotKey in _globalHotKeys.Values)
        {
            if (hotKey.Key == key && hotKey.Modifiers == modifiers)
                return true;
        }

        // 检查局部快捷键
        foreach (var hotKey in _localHotKeys.Values)
        {
            if (hotKey.Key == key && hotKey.Modifiers == modifiers)
                return true;
        }

        return false;
    }

    #endregion

    #region 私有方法 - 消息处理

    /// <summary>
    /// 窗口过程消息处理
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            var hotKeyId = wParam.ToInt32();

            if (_globalHotKeys.TryGetValue(hotKeyId, out var hotKeyInfo))
            {
                try
                {
                    Debug.WriteLine($"触发全局快捷键: {GetHotKeyDisplayName(hotKeyInfo.Key, hotKeyInfo.Modifiers)}");
                    hotKeyInfo.Callback?.Invoke();
                    handled = true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"执行全局快捷键回调异常: {ex.Message}");
                }
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// 窗口键盘按下事件处理
    /// </summary>
    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var modifiers = Keyboard.Modifiers;

            // 检查局部快捷键
            foreach (var hotKey in _localHotKeys.Values)
            {
                if (hotKey.IsEnabled && hotKey.Key == key && hotKey.Modifiers == modifiers)
                {
                    Debug.WriteLine($"触发局部快捷键: {hotKey.Name} - {GetHotKeyDisplayName(key, modifiers)}");
                    hotKey.Callback?.Invoke();
                    e.Handled = true;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理窗口键盘事件异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 窗口键盘释放事件处理
    /// </summary>
    private void OnWindowKeyUp(object sender, KeyEventArgs e)
    {
        // 可以在这里处理键盘释放相关逻辑
    }

    #endregion

    #region 私有方法 - 辅助功能

    /// <summary>
    /// 获取下一个快捷键ID
    /// </summary>
    /// <returns>快捷键ID</returns>
    private int GetNextHotKeyId()
    {
        while (_globalHotKeys.ContainsKey(_nextHotKeyId) && _nextHotKeyId <= MaxHotkeyId)
        {
            _nextHotKeyId++;
        }

        if (_nextHotKeyId > MaxHotkeyId)
        {
            throw new InvalidOperationException("已达到最大快捷键注册数量");
        }

        return _nextHotKeyId++;
    }

    /// <summary>
    /// 将 WPF 修饰键转换为 Win32 修饰键
    /// </summary>
    /// <param name="modifiers">WPF 修饰键</param>
    /// <returns>Win32 修饰键</returns>
    private static uint ConvertToWin32Modifiers(ModifierKeys modifiers)
    {
        uint win32Modifiers = 0;

        if ((modifiers & ModifierKeys.Alt) != 0)
            win32Modifiers |= 0x0001; // MOD_ALT

        if ((modifiers & ModifierKeys.Control) != 0)
            win32Modifiers |= 0x0002; // MOD_CONTROL

        if ((modifiers & ModifierKeys.Shift) != 0)
            win32Modifiers |= 0x0004; // MOD_SHIFT

        if ((modifiers & ModifierKeys.Windows) != 0)
            win32Modifiers |= 0x0008; // MOD_WIN

        return win32Modifiers;
    }

    /// <summary>
    /// 获取快捷键的显示名称
    /// </summary>
    /// <param name="key">键值</param>
    /// <param name="modifiers">修饰键</param>
    /// <returns>显示名称</returns>
    private static string GetHotKeyDisplayName(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();

        if ((modifiers & ModifierKeys.Control) != 0)
            parts.Add("Ctrl");

        if ((modifiers & ModifierKeys.Alt) != 0)
            parts.Add("Alt");

        if ((modifiers & ModifierKeys.Shift) != 0)
            parts.Add("Shift");

        if ((modifiers & ModifierKeys.Windows) != 0)
            parts.Add("Win");

        parts.Add(key.ToString());

        return string.Join(" + ", parts);
    }

    /// <summary>
    /// 注销所有全局快捷键
    /// </summary>
    private void UnregisterAllGlobalHotKeys()
    {
        try
        {
            foreach (var hotKeyId in _globalHotKeys.Keys.ToList())
            {
                UnregisterGlobalHotKey(hotKeyId);
            }

            Debug.WriteLine("所有全局快捷键已注销");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"注销所有全局快捷键异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 清空所有局部快捷键
    /// </summary>
    private void ClearAllLocalHotKeys()
    {
        try
        {
            _localHotKeys.Clear();
            Debug.WriteLine("所有局部快捷键已清空");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清空所有局部快捷键异常: {ex.Message}");
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
            // 注销所有快捷键
            UnregisterAllGlobalHotKeys();
            ClearAllLocalHotKeys();

            // 移除事件处理
            _hwndSource?.RemoveHook(WndProc);

            _ownerWindow.KeyDown -= OnWindowKeyDown;
            _ownerWindow.KeyUp -= OnWindowKeyUp;

            _disposed = true;
            Debug.WriteLine("快捷键管理器已释放");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放快捷键管理器异常: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}