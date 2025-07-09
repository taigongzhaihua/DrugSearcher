using DrugSearcher.ViewModels;
using System.Windows.Controls;

namespace DrugSearcher.Views;

/// <summary>
/// HomePage.xaml 的交互逻辑
/// </summary>
public partial class HomePage : Page
{
    public HomePage(HomePageViewModel viewModel)
    {
        InitializeComponent();
        // DataContext 已在 XAML 中设置
        DataContext = viewModel;
    }
}