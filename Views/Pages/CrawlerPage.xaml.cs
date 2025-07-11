using DrugSearcher.ViewModels;

namespace DrugSearcher.Views;

/// <summary>
/// CrawlerPage.xaml 的交互逻辑
/// </summary>
public partial class CrawlerPage
{
    public CrawlerPage(CrawlerPageViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}