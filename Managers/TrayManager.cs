using DrugSearcher.Constants;
using DrugSearcher.Models;
using DrugSearcher.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Windows;
using Application = System.Windows.Application;
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using ToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace DrugSearcher.Managers;

/// <summary>
/// 系统托盘管理器，负责托盘图标的显示、隐藏以及相关的用户交互
/// 提供窗口显示/隐藏切换、托盘通知、右键菜单等功能
/// </summary>
[SuppressMessage("ReSharper", "AsyncVoidMethod")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public class TrayManager : IDisposable
{
    #region 私有字段

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private readonly Window _mainWindow;
    private readonly IUserSettingsService _settingsService;
    private bool _disposed;
    private bool _isExiting;

    // 菜单项引用，用于动态更新文本和状态
    private ToolStripMenuItem? _showHideMenuItem;
    private ToolStripMenuItem? _trayIconToggleMenuItem;
    private ToolStripMenuItem? _notificationsToggleMenuItem;
    private ToolStripMenuItem? _minimizeOnCloseMenuItem;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化托盘管理器
    /// </summary>
    /// <param name="mainWindow">主窗口引用</param>
    /// <param name="settingsService">用户设置服务</param>
    public TrayManager(Window mainWindow, IUserSettingsService settingsService)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        InitializeTrayIcon();
        SetupWindowEventHandlers();
        SubscribeToSettingsChanges();

        // 根据设置初始化托盘图标状态
        _ = UpdateTrayIconVisibilityAsync();
    }

    #endregion

    #region 初始化方法

    /// <summary>
    /// 初始化托盘图标和相关组件
    /// </summary>
    private void InitializeTrayIcon()
    {
        CreateContextMenu();
        CreateNotifyIcon();
    }

    /// <summary>
    /// 创建右键上下文菜单
    /// </summary>
    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenuStrip();

        // 主窗口显示/隐藏
        _showHideMenuItem = new ToolStripMenuItem("显示主窗口", null, OnShowHideClick);
        _contextMenu.Items.Add(_showHideMenuItem);

        _contextMenu.Items.Add(new ToolStripSeparator());

        // 托盘设置子菜单
        CreateTraySettingsSubMenu();

        // 应用功能菜单
        CreateApplicationMenuItems();

        // 在菜单打开时更新菜单项状态
        _contextMenu.Opening += OnContextMenuOpening;
    }

    /// <summary>
    /// 创建托盘设置子菜单
    /// </summary>
    private void CreateTraySettingsSubMenu()
    {
        var traySettingsMenu = new ToolStripMenuItem("托盘设置");

        _trayIconToggleMenuItem = new ToolStripMenuItem("显示托盘图标", null, OnTrayIconToggleClick);
        _notificationsToggleMenuItem = new ToolStripMenuItem("显示托盘通知", null, OnNotificationsToggleClick);
        _minimizeOnCloseMenuItem = new ToolStripMenuItem("关闭时最小化到托盘", null, OnMinimizeOnCloseToggleClick);

        traySettingsMenu.DropDownItems.AddRange(_trayIconToggleMenuItem, _notificationsToggleMenuItem, _minimizeOnCloseMenuItem);

        _contextMenu?.Items.Add(traySettingsMenu);
    }

    /// <summary>
    /// 创建应用程序功能菜单项
    /// </summary>
    private void CreateApplicationMenuItems()
    {
        _contextMenu?.Items.Add(new ToolStripSeparator());

        var settingsMenuItem = new ToolStripMenuItem("设置", null, OnSettingsClick);
        var aboutMenuItem = new ToolStripMenuItem("关于", null, OnAboutClick);

        _contextMenu?.Items.AddRange(settingsMenuItem, aboutMenuItem);

        _contextMenu?.Items.Add(new ToolStripSeparator());

        var exitMenuItem = new ToolStripMenuItem("退出", null, OnExitClick);
        _contextMenu?.Items.Add(exitMenuItem);
    }

    /// <summary>
    /// 创建系统托盘图标
    /// </summary>
    private void CreateNotifyIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = GetApplicationIcon(),
            ContextMenuStrip = _contextMenu,
            Text = "DrugSearcher - 双击显示/隐藏窗口",
            Visible = false
        };

        // 双击托盘图标切换窗口显示状态
        _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;

        // 单击也可以显示窗口（可选行为）
        _notifyIcon.Click += OnNotifyIconClick;
    }

    /// <summary>
    /// 设置主窗口事件处理器
    /// </summary>
    private void SetupWindowEventHandlers()
    {
        _mainWindow.StateChanged += OnWindowStateChanged;
        _mainWindow.Closing += OnWindowClosing;
    }

    /// <summary>
    /// 订阅设置变更事件
    /// </summary>
    private void SubscribeToSettingsChanges() => _settingsService.SettingChanged += OnSettingChanged;

    /// <summary>
    /// 获取应用程序图标
    /// </summary>
    /// <returns>应用程序图标</returns>
    private static Icon GetApplicationIcon()
    {
        try
        {
            // 尝试从应用程序资源获取图标
            var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/app.ico"));
            if (iconStream != null)
            {
                return new Icon(iconStream.Stream);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"无法加载自定义图标: {ex.Message}");
        }

        // 使用默认的应用程序图标
        try
        {
            return Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location)
                   ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    #endregion

    #region 事件处理器

    /// <summary>
    /// 处理窗口状态变更事件
    /// </summary>
    private async void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            var showTrayIcon = await _settingsService.GetSettingAsync(SettingKeys.SHOW_TRAY_ICON, true);
            if (showTrayIcon)
            {
                await HideToTrayAsync();
            }
        }
    }

    /// <summary>
    /// 处理窗口关闭事件
    /// </summary>
    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isExiting)
            return;

        var minimizeOnClose = await _settingsService.GetSettingAsync(SettingKeys.MINIMIZE_TO_TRAY_ON_CLOSE, true);
        var showTrayIcon = await _settingsService.GetSettingAsync(SettingKeys.SHOW_TRAY_ICON, true);

        if (minimizeOnClose && showTrayIcon)
        {
            e.Cancel = true;
            await HideToTrayAsync();
        }
    }

    /// <summary>
    /// 处理设置变更事件
    /// </summary>
    private async void OnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        switch (e.Key)
        {
            case SettingKeys.SHOW_TRAY_ICON:
                await UpdateTrayIconVisibilityAsync();
                break;
            case SettingKeys.SHOW_TRAY_NOTIFICATIONS:
                // 通知设置变更，无需特殊处理
                break;
            case SettingKeys.MINIMIZE_TO_TRAY_ON_CLOSE:
                // 关闭行为设置变更，无需特殊处理
                break;
        }
    }

    /// <summary>
    /// 处理右键菜单打开事件
    /// </summary>
    private async void OnContextMenuOpening(object? sender, CancelEventArgs e) => await UpdateMenuItemStatesAsync();

    /// <summary>
    /// 处理托盘图标双击事件
    /// </summary>
    private async void OnNotifyIconDoubleClick(object? sender, EventArgs e) => await ToggleWindowVisibilityAsync();

    /// <summary>
    /// 处理托盘图标单击事件
    /// </summary>
    private async void OnNotifyIconClick(object? sender, EventArgs e)
    {
        // 可选：单击也显示窗口
        if (e is MouseEventArgs { Button: MouseButtons.Left })
        {
            await ShowFromTrayAsync();
        }
    }

    /// <summary>
    /// 处理显示/隐藏菜单项点击事件
    /// </summary>
    private async void OnShowHideClick(object? sender, EventArgs e) => await ToggleWindowVisibilityAsync();

    /// <summary>
    /// 处理托盘图标开关菜单项点击事件
    /// </summary>
    private async void OnTrayIconToggleClick(object? sender, EventArgs e)
    {
        try
        {
            var currentValue = await _settingsService.GetSettingAsync(SettingKeys.SHOW_TRAY_ICON, true);
            await _settingsService.SetSettingAsync(SettingKeys.SHOW_TRAY_ICON, !currentValue);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"切换托盘图标设置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理托盘通知开关菜单项点击事件
    /// </summary>
    private async void OnNotificationsToggleClick(object? sender, EventArgs e)
    {
        try
        {
            var currentValue = await _settingsService.GetSettingAsync(SettingKeys.SHOW_TRAY_NOTIFICATIONS, true);
            await _settingsService.SetSettingAsync(SettingKeys.SHOW_TRAY_NOTIFICATIONS, !currentValue);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"切换托盘通知设置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理关闭时最小化开关菜单项点击事件
    /// </summary>
    private async void OnMinimizeOnCloseToggleClick(object? sender, EventArgs e)
    {
        try
        {
            var currentValue = await _settingsService.GetSettingAsync(SettingKeys.MINIMIZE_TO_TRAY_ON_CLOSE, true);
            await _settingsService.SetSettingAsync(SettingKeys.MINIMIZE_TO_TRAY_ON_CLOSE, !currentValue);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"切换关闭时最小化设置失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理设置菜单项点击事件
    /// </summary>
    private async void OnSettingsClick(object? sender, EventArgs e)
    {
        await ShowFromTrayAsync();
        SettingsRequested?.Invoke();
    }

    /// <summary>
    /// 处理关于菜单项点击事件
    /// </summary>
    private async void OnAboutClick(object? sender, EventArgs e)
    {
        await ShowFromTrayAsync();
        AboutRequested?.Invoke();
    }

    /// <summary>
    /// 处理退出菜单项点击事件
    /// </summary>
    private void OnExitClick(object? sender, EventArgs e) => ExitApplication();

    #endregion

    #region 公共方法

    /// <summary>
    /// 切换窗口显示/隐藏状态
    /// </summary>
    public async Task ToggleWindowVisibilityAsync()
    {
        try
        {
            if (IsWindowVisible())
            {
                await HideToTrayAsync();
            }
            else
            {
                await ShowFromTrayAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"切换窗口可见性失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 隐藏窗口到托盘
    /// </summary>
    public async Task HideToTrayAsync()
    {
        try
        {
            _mainWindow.Hide();
            _mainWindow.ShowInTaskbar = false;

            await UpdateTrayIconVisibilityAsync();

            // 显示托盘通知（如果启用）
            await ShowHideNotificationAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"隐藏到托盘失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从托盘显示窗口
    /// </summary>
    public async Task ShowFromTrayAsync()
    {
        try
        {
            _mainWindow.Show();
            _mainWindow.ShowInTaskbar = true;
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();

            // 根据设置决定托盘图标可见性
            await UpdateTrayIconVisibilityAsync();

            // 使用单实例管理器的强制激活功能
            SingleInstanceManager.ForceActivateWindow(_mainWindow);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"从托盘显示窗口失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 强制显示托盘图标（用于设置页面等）
    /// </summary>
    public void ForceShowTrayIcon()
    {
        if (_notifyIcon != null) _notifyIcon.Visible = true;
    }

    /// <summary>
    /// 强制隐藏托盘图标
    /// </summary>
    public void ForceHideTrayIcon()
    {
        if (_notifyIcon != null) _notifyIcon.Visible = false;
    }

    /// <summary>
    /// 更新托盘图标的可见性
    /// </summary>
    public async Task UpdateTrayIconVisibilityAsync()
    {
        if (_notifyIcon == null) return;

        try
        {
            var showTrayIcon = await _settingsService.GetSettingAsync(SettingKeys.SHOW_TRAY_ICON, true);

            // 如果设置为显示托盘图标，则显示
            // 如果窗口隐藏且设置为不显示托盘图标，则强制显示（避免程序无法访问）
            var windowVisible = IsWindowVisible();
            _notifyIcon.Visible = showTrayIcon || !windowVisible;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新托盘图标可见性失败: {ex.Message}");
            // 发生错误时默认显示托盘图标
            if (_notifyIcon != null) _notifyIcon.Visible = true;
        }
    }

    /// <summary>
    /// 显示自定义托盘通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知消息</param>
    /// <param name="icon">通知图标</param>
    /// <param name="timeout">显示超时时间（毫秒）</param>
    public async Task ShowTrayNotificationAsync(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
    {
        try
        {
            var showNotifications = await _settingsService.GetSettingAsync(SettingKeys.SHOW_TRAY_NOTIFICATIONS, true);
            if (_notifyIcon is { Visible: true } && showNotifications)
            {
                _notifyIcon.ShowBalloonTip(timeout, title, message, icon);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"显示托盘通知失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 退出应用程序
    /// </summary>
    public void ExitApplication()
    {
        try
        {
            _isExiting = true;

            // 移除窗口事件处理器
            RemoveWindowEventHandlers();

            // 隐藏托盘图标
            if (_notifyIcon != null) _notifyIcon.Visible = false;

            // 退出应用程序
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"退出应用程序失败: {ex.Message}");
        }
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 检查窗口是否可见
    /// </summary>
    /// <returns>如果窗口可见则返回true</returns>
    private bool IsWindowVisible() => _mainWindow.IsVisible &&
               _mainWindow.WindowState != WindowState.Minimized &&
               _mainWindow.ShowInTaskbar;

    /// <summary>
    /// 异步更新菜单项状态
    /// </summary>
    private async Task UpdateMenuItemStatesAsync()
    {
        try
        {
            // 更新显示/隐藏菜单项文本
            if (_showHideMenuItem != null) _showHideMenuItem.Text = IsWindowVisible() ? "隐藏到托盘" : "显示主窗口";

            // 更新设置菜单项的选中状态
            await UpdateToggleMenuItemStatesAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"更新菜单项状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 更新切换类型菜单项的选中状态
    /// </summary>
    private async Task UpdateToggleMenuItemStatesAsync()
    {
        if (_trayIconToggleMenuItem != null)
        {
            var showTrayIcon = await _settingsService.GetSettingAsync(SettingKeys.SHOW_TRAY_ICON, true);
            _trayIconToggleMenuItem.Checked = showTrayIcon;
        }

        if (_notificationsToggleMenuItem != null)
        {
            var showNotifications = await _settingsService.GetSettingAsync(SettingKeys.SHOW_TRAY_NOTIFICATIONS, true);
            _notificationsToggleMenuItem.Checked = showNotifications;
        }

        if (_minimizeOnCloseMenuItem != null)
        {
            var minimizeOnClose = await _settingsService.GetSettingAsync(SettingKeys.MINIMIZE_TO_TRAY_ON_CLOSE, true);
            _minimizeOnCloseMenuItem.Checked = minimizeOnClose;
        }
    }

    /// <summary>
    /// 显示隐藏到托盘的通知
    /// </summary>
    private async Task ShowHideNotificationAsync()
    {
        var showNotifications = await _settingsService.GetSettingAsync(SettingKeys.SHOW_TRAY_NOTIFICATIONS, true);
        if (_notifyIcon is { Visible: true } && showNotifications)
        {
            _notifyIcon.ShowBalloonTip(2000,
                "DrugSearcher",
                "应用程序已隐藏到托盘，双击图标可重新显示",
                ToolTipIcon.Info);
        }
    }

    /// <summary>
    /// 移除窗口事件处理器
    /// </summary>
    private void RemoveWindowEventHandlers()
    {
        _mainWindow.StateChanged -= OnWindowStateChanged;
        _mainWindow.Closing -= OnWindowClosing;
    }

    /// <summary>
    /// 取消订阅设置变更事件
    /// </summary>
    private void UnsubscribeFromSettingsChanges() => _settingsService.SettingChanged -= OnSettingChanged;

    #endregion

    #region 兼容性方法（保持向后兼容）

    /// <summary>
    /// 切换窗口显示/隐藏状态（同步版本，用于向后兼容）
    /// </summary>
    public void ToggleWindowVisibility() => _ = ToggleWindowVisibilityAsync();

    /// <summary>
    /// 隐藏到托盘（同步版本，用于向后兼容）
    /// </summary>
    public void HideToTray() => _ = HideToTrayAsync();

    /// <summary>
    /// 从托盘显示（同步版本，用于向后兼容）
    /// </summary>
    public void ShowFromTray() => _ = ShowFromTrayAsync();

    /// <summary>
    /// 更新托盘图标可见性（同步版本，用于向后兼容）
    /// </summary>
    public void UpdateTrayIconVisibility() => _ = UpdateTrayIconVisibilityAsync();

    /// <summary>
    /// 显示托盘通知（同步版本，用于向后兼容）
    /// </summary>
    public void ShowTrayNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000) => _ = ShowTrayNotificationAsync(title, message, icon, timeout);

    #endregion

    #region 事件

    /// <summary>
    /// 设置请求事件
    /// </summary>
    public event Action? SettingsRequested;

    /// <summary>
    /// 关于请求事件
    /// </summary>
    public event Action? AboutRequested;
    #endregion

    #region 资源释放

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            // 取消订阅设置变更事件
            UnsubscribeFromSettingsChanges();

            // 移除窗口事件处理器
            RemoveWindowEventHandlers();

            // 释放托盘图标
            DisposeTrayIcon();

            // 释放上下文菜单
            DisposeContextMenu();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放TrayManager资源失败: {ex.Message}");
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 释放托盘图标资源
    /// </summary>
    private void DisposeTrayIcon()
    {
        if (_notifyIcon == null) return;
        _notifyIcon.Visible = false;
        _notifyIcon.DoubleClick -= OnNotifyIconDoubleClick;
        _notifyIcon.Click -= OnNotifyIconClick;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }

    /// <summary>
    /// 释放上下文菜单资源
    /// </summary>
    private void DisposeContextMenu()
    {
        if (_contextMenu == null) return;
        _contextMenu.Opening -= OnContextMenuOpening;
        _contextMenu.Dispose();
        _contextMenu = null;
    }

    #endregion
}