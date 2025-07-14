using DrugSearcher.ViewModels;
using System.Diagnostics;
using System.Windows.Controls;

namespace DrugSearcher.Views;

/// <summary>
/// 动态设置页面
/// </summary>
public partial class SettingsPage : Page
{
    #region 构造函数

    /// <summary>
    /// 初始化设置页面
    /// </summary>
    /// <param name="viewModel">设置页面视图模型</param>
    public SettingsPage(SettingsPageViewModel viewModel)
    {
        try
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            Debug.WriteLine("动态设置页面初始化完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"动态设置页面初始化失败: {ex.Message}");
            throw;
        }
    }

    #endregion
}