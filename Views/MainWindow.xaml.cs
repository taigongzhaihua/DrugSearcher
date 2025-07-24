using DrugSearcher.Configuration;
using DrugSearcher.Constants;
using DrugSearcher.Helpers;
using DrugSearcher.Managers;
using DrugSearcher.Services;
using DrugSearcher.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;
using Button = System.Windows.Controls.Button;

namespace DrugSearcher.Views;

/// <summary>
/// 主窗口，应用程序的主要用户界面
/// 包括窗口管理、导航控制、托盘集成等功能
/// </summary>
public partial class MainWindow
{
    #region 私有字段

    private TrayManager? _trayManager;
    private readonly IUserSettingsService? _settingsService;
    private readonly MainWindowViewModel _viewModel;
    private IHotKeyService? _hotKeyService;
    private readonly ThemeManager _themeManager;

    #endregion

    #region 构造函数

    /// <summary>
    /// 初始化主窗口
    /// </summary>
    /// <param name="viewModel">主窗口视图模型</param>
    /// <param name="settingsService">用户设置服务</param>
    public MainWindow(
        MainWindowViewModel viewModel,
        IUserSettingsService settingsService,
        ThemeManager themeManager)
    {

        try
        {
            // 初始化组件
            InitializeComponent();

            // 保存依赖引用
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _themeManager = themeManager;

            // 设置数据上下文
            DataContext = _viewModel;

            // 初始化窗口设置
            InitializeWindow();
            // 延迟初始化快捷键服务
            Loaded += OnWindowLoaded;
            // 注册窗口关闭事件
            Closed += OnWindowClosed;
            // 监听激活状态变化
            Activated += OnWindowActivated;
            Deactivated += OnWindowDeactivated;
            Debug.WriteLine("主窗口初始化完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"主窗口初始化失败: {ex.Message}");
            throw;
        }
    }


    #endregion

    #region 初始化方法

    // 窗口加载完成后初始化快捷键
    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 从容器解析快捷键服务
            if (!ContainerAccessor.IsInitialized) return;
            var hotKeyService = ContainerAccessor.Resolve<IHotKeyService>();
            InitializeHotKeys(hotKeyService);
            _themeManager.RegisterWindow(this);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"初始化快捷键服务失败: {ex.Message}");
        }
    }
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // 注销窗口
        _themeManager.UnregisterWindow(this);
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        // 窗口获得焦点时刷新
        WindowColorManager.RefreshWindow(this);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // 窗口失去焦点时刷新
        WindowColorManager.RefreshWindow(this);
    }

    /// <summary>
    /// 初始化窗口设置
    /// </summary>
    private void InitializeWindow()
    {
        try
        {
            // 配置窗口Chrome
            ConfigureWindowChrome();

            // 初始化服务
            InitializeServices();

            // 设置导航
            SetupNavigation();

            Debug.WriteLine("窗口设置初始化完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"窗口设置初始化失败: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 配置窗口Chrome样式
    /// </summary>
    private void ConfigureWindowChrome()
    {
        var windowChrome = new WindowChrome
        {
            ResizeBorderThickness = new Thickness(5),
            CaptionHeight = 60,
            CornerRadius = new CornerRadius(8),
            GlassFrameThickness = new Thickness(-1),
            UseAeroCaptionButtons = false
        };

        // 检查是否为 Windows 11
        if (IsWindows11())
        {
            // Windows 11：隐藏顶部边框
            windowChrome.NonClientFrameEdges = NonClientFrameEdges.Bottom |
                                               NonClientFrameEdges.Left |
                                               NonClientFrameEdges.Right;
        }
        else
        {
            windowChrome.NonClientFrameEdges = NonClientFrameEdges.Bottom |
                                               NonClientFrameEdges.Left |
                                               NonClientFrameEdges.Right;
            // Windows 10 及更早版本：保留四条边框
            WindowStyle = WindowStyle.ThreeDBorderWindow;
        }

        WindowChrome.SetWindowChrome(this, windowChrome);
        Debug.WriteLine($"窗口Chrome配置完成 - 系统版本: {(IsWindows11() ? "Windows 11" : "Windows 10 或更早")}");
    }

    /// <summary>
    /// 初始化相关服务
    /// </summary>
    private void InitializeServices()
    {
        if (!ContainerAccessor.IsInitialized)
        {
            Debug.WriteLine("容器未初始化，跳过服务初始化");
            return;
        }

        try
        {
            // 启动单实例管道监听
            SingleInstanceManager.StartListening(this);
            Debug.WriteLine("单实例管理器已启动");

            // 初始化托盘管理器
            InitializeTrayManager();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"服务初始化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 初始化托盘管理器
    /// </summary>
    private void InitializeTrayManager()
    {
        if (_settingsService == null)
        {
            Debug.WriteLine("设置服务未提供，跳过托盘管理器初始化");
            return;
        }

        try
        {
            _trayManager = new TrayManager(this, _settingsService);

            // 订阅托盘管理器事件
            _trayManager.SettingsRequested += OnTraySettingsRequested;
            _trayManager.AboutRequested += OnTrayAboutRequested;

            Debug.WriteLine("托盘管理器初始化完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"托盘管理器初始化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 初始化快捷键
    /// </summary>
    private void InitializeHotKeys(IHotKeyService? hotKeyService)
    {
        if (hotKeyService == null) return;

        try
        {
            // 注册默认快捷键
            hotKeyService.RegisterDefaultHotKeys();

            // 订阅快捷键事件
            hotKeyService.ShowMainWindowRequested += OnShowMainWindowRequested;
            hotKeyService.QuickSearchRequested += OnQuickSearchRequested;
            hotKeyService.SearchRequested += OnSearchRequested;
            hotKeyService.RefreshRequested += OnRefreshRequested;
            hotKeyService.SettingsRequested += OnSettingsRequested;
            hotKeyService.ExitRequested += OnExitRequested;

            _hotKeyService = hotKeyService;

            Debug.WriteLine("快捷键初始化完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"快捷键初始化失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 设置页面导航
    /// </summary>
    private void SetupNavigation()
    {
        try
        {
            // 导航到主页
            NavigateToHomePage();

            // 设置导航事件处理
            MainFrame.Navigated += OnFrameNavigated;

            Debug.WriteLine("页面导航设置完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"页面导航设置失败: {ex.Message}");
        }
    }

    #endregion

    #region 导航管理

    /// <summary>
    /// 导航到主页
    /// </summary>
    private void NavigateToHomePage()
    {
        try
        {
            var homePage = ContainerAccessor.Resolve<HomePage>();
            MainFrame.Navigate(homePage);
            Debug.WriteLine("已导航到主页");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"导航到主页失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 导航到设置页
    /// </summary>
    private void NavigateToSettingsPage()
    {
        try
        {
            var settingsPage = ContainerAccessor.Resolve<SettingsPage>();
            MainFrame.Navigate(settingsPage);
            Debug.WriteLine("已导航到设置页");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"导航到设置页失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 导航到数据管理页
    /// </summary>
    private void NavigateToDataManagementPage()
    {
        try
        {
            var dataManagementPage = ContainerAccessor.Resolve<LocalDataManagementPage>();
            MainFrame.Navigate(dataManagementPage);
            Debug.WriteLine("已导航到数据管理页");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"导航到数据管理页失败: {ex.Message}");
        }
    }

    private void NavigateToCrawlerPage()
    {
        try
        {
            var crawlerPage = ContainerAccessor.Resolve<CrawlerPage>();
            MainFrame.Navigate(crawlerPage);
            Debug.WriteLine("已导航到爬虫页");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"导航到爬虫页失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 导航到关于页
    /// </summary>
    private void NavigateToAboutPage()
    {
        try
        {
            var aboutPage = ContainerAccessor.Resolve<AboutPage>();
            MainFrame.Navigate(aboutPage);
            Debug.WriteLine("已导航到关于页");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"显示关于对话框失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理框架导航事件
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">导航事件参数</param>
    private void OnFrameNavigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
    {
        try
        {
            UpdateMenuItemVisibility(e.Content);
            Debug.WriteLine($"页面导航完成: {e.Content?.GetType().Name}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理页面导航事件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据当前页面更新菜单项可见性
    /// </summary>
    /// <param name="currentPage">当前页面</param>
    private void UpdateMenuItemVisibility(object? currentPage)
    {
        List<FrameworkElement> pages =
        [
            HomeButton, SettingsMenuItem, CrawlMenuItem, LoacalDataMenuItem, AboutMenuItem
        ];
        foreach (var element in pages)
        {
            element.Visibility = Visibility.Visible;
        }

        switch (currentPage)
        {
            case HomePage:
                // 主页：隐藏主页按钮
                HomeButton.Visibility = Visibility.Collapsed;
                break;

            case SettingsPage:
                // 设置页：显示主页按钮，隐藏设置菜单项
                SettingsMenuItem.Visibility = Visibility.Collapsed;
                break;

            case LocalDataManagementPage:
                // 数据管理页：显示主页按钮
                LoacalDataMenuItem.Visibility = Visibility.Collapsed;
                break;
            case CrawlerPage:
                CrawlMenuItem.Visibility = Visibility.Collapsed;
                break;
            case AboutPage:
                AboutMenuItem.Visibility = Visibility.Collapsed;
                break;
            default:
                // 其他页面：显示所有按钮
                HomeButton.Visibility = Visibility.Visible;
                SettingsMenuItem.Visibility = Visibility.Visible;
                break;
        }
    }

    #endregion

    #region 托盘管理

    /// <summary>
    /// 手动隐藏到托盘
    /// </summary>
    public async Task HideToTrayAsync()
    {
        try
        {
            if (_trayManager != null)
            {
                await _trayManager.HideToTrayAsync();
                Debug.WriteLine("窗口已隐藏到托盘");
            }
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
            if (_trayManager != null)
            {
                await _trayManager.ShowFromTrayAsync();
                Debug.WriteLine("窗口已从托盘显示");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"从托盘显示失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 切换窗口显示状态
    /// </summary>
    public async Task ToggleVisibilityAsync()
    {
        try
        {
            if (_trayManager != null)
            {
                await _trayManager.ToggleWindowVisibilityAsync();
                Debug.WriteLine("窗口显示状态已切换");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"切换窗口显示状态失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 显示托盘通知
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知消息</param>
    public async Task ShowNotificationAsync(string title, string message)
    {
        try
        {
            if (_trayManager != null)
            {
                await _trayManager.ShowTrayNotificationAsync(title, message);
                Debug.WriteLine($"托盘通知已显示: {title}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"显示托盘通知失败: {ex.Message}");
        }
    }

    #endregion

    #region 事件处理器 - 托盘

    /// <summary>
    /// 处理托盘设置请求
    /// </summary>
    private async void OnTraySettingsRequested()
    {
        try
        {
            await ShowFromTrayAsync();
            NavigateToSettingsPage();
            Debug.WriteLine("已响应托盘设置请求");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理托盘设置请求失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理托盘关于请求
    /// </summary>
    private async void OnTrayAboutRequested()
    {
        try
        {
            await ShowFromTrayAsync();
            NavigateToAboutPage();
            Debug.WriteLine("已响应托盘关于请求");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理托盘关于请求失败: {ex.Message}");
        }
    }

    #endregion

    #region 事件处理器 - 导航按钮

    /// <summary>
    /// 处理主页按钮点击
    /// </summary>
    private void HomeButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            NavigateToHomePage();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"主页按钮点击处理失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理后退按钮点击
    /// </summary>
    private void BackButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (MainFrame.CanGoBack)
            {
                MainFrame.GoBack();
                Debug.WriteLine("执行后退导航");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"后退按钮点击处理失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理前进按钮点击
    /// </summary>
    private void ForwardButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (MainFrame.CanGoForward)
            {
                MainFrame.GoForward();
                Debug.WriteLine("执行前进导航");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"前进按钮点击处理失败: {ex.Message}");
        }
    }

    #endregion

    #region 事件处理器 - 菜单

    /// <summary>
    /// 处理菜单按钮点击 - 显示上下文菜单
    /// </summary>
    private void MenuButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { ContextMenu: not null } button)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
                Debug.WriteLine("功能菜单已打开");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"菜单按钮点击处理失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理数据管理菜单项点击
    /// </summary>
    private void DataManagementMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            NavigateToDataManagementPage();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"数据管理菜单项点击处理失败: {ex.Message}");
        }
    }

    private void MenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            NavigateToCrawlerPage();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"数据管理菜单项点击处理失败: {ex.Message}");
        }
    }


    /// <summary>
    /// 处理设置菜单项点击
    /// </summary>
    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            NavigateToSettingsPage();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"设置菜单项点击处理失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理关于菜单项点击
    /// </summary>
    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            NavigateToAboutPage();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"关于菜单项点击处理失败: {ex.Message}");
        }
    }

    #endregion

    #region 事件处理器 - 窗口控制

    /// <summary>
    /// 处理最小化按钮点击
    /// </summary>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            WindowState = WindowState.Minimized;
            Debug.WriteLine("窗口已最小化");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"最小化操作失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理最大化按钮点击
    /// </summary>
    private void MaximizeButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

            Debug.WriteLine($"窗口状态已切换到: {WindowState}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"最大化操作失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理关闭按钮点击
    /// </summary>
    private async void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var shouldMinimizeToTray = await ShouldMinimizeToTrayOnClose();

            if (shouldMinimizeToTray)
            {
                await HideToTrayAsync();
                Debug.WriteLine("窗口已最小化到托盘");
            }
            else
            {
                Close();
                Debug.WriteLine("窗口已关闭");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"关闭操作失败: {ex.Message}");
            // 确保窗口能够关闭
            Close();
        }
    }

    #endregion

    #region 事件处理器 - 快捷键

    // 快捷键事件处理方法
    private void OnShowMainWindowRequested() => _ = ShowFromTrayAsync();

    private void OnQuickSearchRequested()
    {
        _ = ShowFromTrayAsync();
        NavigateToHomePage();
    }

    private void OnSearchRequested()
    {
        // 触发搜索功能
    }

    private void OnRefreshRequested()
    {
        // 刷新当前页面
        if (MainFrame.Content is Page)
        {
            MainFrame.Refresh();
        }
    }

    private void OnSettingsRequested() => NavigateToSettingsPage();

    private void OnExitRequested() => Close();

    #endregion

    #region 辅助方法

    /// <summary>
    /// 检查是否应该在关闭时最小化到托盘
    /// </summary>
    /// <returns>是否应该最小化到托盘</returns>
    private async Task<bool> ShouldMinimizeToTrayOnClose()
    {
        try
        {
            if (_settingsService == null)
                return false;

            return await _settingsService.GetSettingAsync(SettingKeys.MinimizeToTrayOnClose, false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检查托盘设置失败: {ex.Message}");
            return false;
        }
    }


    private void CleanupHotKeys()
    {
        if (_hotKeyService == null) return;

        try
        {
            _hotKeyService.ShowMainWindowRequested -= OnShowMainWindowRequested;
            _hotKeyService.QuickSearchRequested -= OnQuickSearchRequested;
            _hotKeyService.SearchRequested -= OnSearchRequested;
            _hotKeyService.RefreshRequested -= OnRefreshRequested;
            _hotKeyService.SettingsRequested -= OnSettingsRequested;
            _hotKeyService.ExitRequested -= OnExitRequested;

            _hotKeyService.Dispose();
            Debug.WriteLine("快捷键已清理");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清理快捷键失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 判断是否为 Windows 11
    /// </summary>
    private static bool IsWindows11()
    {
        try
        {
            var osVersion = Environment.OSVersion.Version;

            // Windows 11 的版本号为 10.0.22000 或更高
            // Build 22000 是 Windows 11 的第一个正式版本
            return osVersion is { Major: 10, Build: >= 22000 };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检测系统版本失败: {ex.Message}");
            return false; // 默认按 Windows 10 处理
        }
    }

    #endregion

    #region 窗口生命周期

    /// <summary>
    /// 窗口关闭时的清理工作
    /// </summary>
    /// <param name="e">关闭事件参数</param>
    protected override void OnClosed(EventArgs e)
    {
        try
        {
            // 清理托盘管理器
            CleanupTrayManager();

            CleanupHotKeys(); // 添加这一行
            // 清理单实例管理器
            SingleInstanceManager.Cleanup();

            // 清理视图模型
            _viewModel.Dispose();

            Debug.WriteLine("主窗口清理完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"主窗口清理失败: {ex.Message}");
        }
        finally
        {
            base.OnClosed(e);
        }
    }

    /// <summary>
    /// 清理托盘管理器
    /// </summary>
    private void CleanupTrayManager()
    {
        if (_trayManager == null)
            return;

        try
        {
            // 取消事件订阅
            _trayManager.SettingsRequested -= OnTraySettingsRequested;
            _trayManager.AboutRequested -= OnTrayAboutRequested;

            // 释放资源
            _trayManager.Dispose();
            _trayManager = null;

            Debug.WriteLine("托盘管理器已清理");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清理托盘管理器失败: {ex.Message}");
        }
    }

    #endregion

    #region 向后兼容方法

    /// <summary>
    /// 隐藏到托盘（向后兼容）
    /// </summary>
    public void HideToTray() => _ = HideToTrayAsync();

    /// <summary>
    /// 从托盘显示（向后兼容）
    /// </summary>
    public void ShowFromTray() => _ = ShowFromTrayAsync();

    /// <summary>
    /// 切换窗口显示状态（向后兼容）
    /// </summary>
    public void ToggleVisibility() => _ = ToggleVisibilityAsync();

    /// <summary>
    /// 显示托盘通知（向后兼容）
    /// </summary>
    /// <param name="title">通知标题</param>
    /// <param name="message">通知消息</param>
    public void ShowNotification(string title, string message) => _ = ShowNotificationAsync(title, message);

    #endregion
}