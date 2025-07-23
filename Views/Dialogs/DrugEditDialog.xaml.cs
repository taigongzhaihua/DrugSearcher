using DrugSearcher.Configuration;
using DrugSearcher.Managers;
using DrugSearcher.Models;
using DrugSearcher.ViewModels;
using System.Windows;
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