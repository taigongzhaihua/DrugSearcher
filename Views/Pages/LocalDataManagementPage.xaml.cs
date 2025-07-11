using DrugSearcher.Models;
using DrugSearcher.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace DrugSearcher.Views;

/// <summary>
/// 本地数据管理页面
/// </summary>
public partial class LocalDataManagementPage : Page
{
    public LocalDataManagementPage(LocalDataManagementViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>
    /// 全选复选框选中事件
    /// </summary>
    private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (DataContext is LocalDataManagementViewModel viewModel)
        {
            foreach (var item in viewModel.DrugItems)
            {
                if (!viewModel.SelectedDrugs.Contains(item))
                {
                    viewModel.SelectedDrugs.Add(item);
                }
            }
            DrugDataGrid.SelectAll();
        }
    }

    /// <summary>
    /// 全选复选框取消选中事件
    /// </summary>
    private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is LocalDataManagementViewModel viewModel)
        {
            viewModel.SelectedDrugs.Clear();
            DrugDataGrid.UnselectAll();
        }
    }

    /// <summary>
    /// 数据网格选择变化事件
    /// </summary>
    private void DrugDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is LocalDataManagementViewModel viewModel)
        {
            // 添加新选中的项
            foreach (DrugInfo item in e.AddedItems)
            {
                if (!viewModel.SelectedDrugs.Contains(item))
                {
                    viewModel.SelectedDrugs.Add(item);
                }
            }

            // 移除取消选中的项
            foreach (DrugInfo item in e.RemovedItems)
            {
                viewModel.SelectedDrugs.Remove(item);
            }
        }
    }
}