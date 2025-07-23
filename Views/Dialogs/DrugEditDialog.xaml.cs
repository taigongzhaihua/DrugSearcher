using DrugSearcher.Configuration;
using DrugSearcher.Managers;
using DrugSearcher.Models;
using DrugSearcher.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Shell;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.Views;

/// <summary>
/// 药物编辑对话框
/// </summary>
public partial class DrugEditDialog
{
    private DrugEditDialogViewModel? _viewModel;
    private readonly ThemeManager _themeManager;

    public DrugEditDialog(LocalDrugInfo? drugInfo = null)
    {

        InitializeComponent();
        _themeManager = ContainerAccessor.Resolve<ThemeManager>();

        try
        {
            _viewModel = new DrugEditDialogViewModel(drugInfo);
            DataContext = _viewModel;
            ConfigureWindowChrome();

            // 窗口加载完成后注册到主题管理器
            Loaded += OnWindowLoaded;
            Closed += OnWindowClosed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"初始化DrugEditDialog失败: {ex.Message}");
            // 设置一个默认的ViewModel以防止崩溃
            _viewModel = new DrugEditDialogViewModel();
            DataContext = _viewModel;
        }
    }
    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // 注册窗口以启用边框颜色跟随主题
        _themeManager.RegisterWindow(this);
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // 注销窗口
        _themeManager.UnregisterWindow(this);
    }
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
    /// <summary>
    /// 获取编辑后的药物信息
    /// </summary>
    public LocalDrugInfo? DrugInfo => _viewModel?.ResultDrugInfo;

    /// <summary>
    /// 保存按钮点击事件
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_viewModel?.DialogResult != true) return;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"保存按钮点击处理失败: {ex.Message}");
            MessageBox.Show($"保存时发生错误：{ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 取消按钮点击事件
    /// </summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DialogResult = false;
            Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"取消按钮点击处理失败: {ex.Message}");
            // 强制关闭窗口
            Close();
        }
    }

    /// <summary>
    /// 窗口加载完成事件
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        try
        {
            base.OnSourceInitialized(e);

            // 确保ViewModel已正确初始化
            if (_viewModel != null) return;
            _viewModel = new DrugEditDialogViewModel();
            DataContext = _viewModel;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"窗口初始化失败: {ex.Message}");
        }
    }
}