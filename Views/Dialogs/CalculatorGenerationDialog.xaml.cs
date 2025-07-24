using DrugSearcher.Configuration;
using DrugSearcher.Managers;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shell;

namespace DrugSearcher.Views;

/// <summary>
/// 计算器生成对话框
/// </summary>
public partial class CalculatorGenerationDialog
{
    public string CalculatorType { get; private set; } = string.Empty;
    public string AdditionalRequirements { get; private set; } = string.Empty;
    private readonly ThemeManager _themeManager;

    public CalculatorGenerationDialog()
    {
        InitializeComponent();

        _themeManager = ContainerAccessor.Resolve<ThemeManager>();

        ConfigureWindowChrome();
        // 窗口加载完成后注册到主题管理器
        Loaded += OnWindowLoaded;
        Closed += OnWindowClosed;

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
            windowChrome.NonClientFrameEdges = NonClientFrameEdges.Bottom;
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

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        // 获取选择的计算器类型
        if (CalculatorTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            CalculatorType = selectedItem.Content.ToString() ?? "通用剂量计算器";
        }
        else
        {
            CalculatorType = "通用剂量计算器";
        }

        // 获取额外要求
        AdditionalRequirements = AdditionalRequirementsTextBox.Text.Trim();

        // 设置对话框结果为True并关闭
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        // 设置对话框结果为False并关闭
        DialogResult = false;
        Close();
    }
}