using DrugSearcher.ViewModels;
using System.Diagnostics;
using System.Windows.Controls;

namespace DrugSearcher.Views
{
    /// <summary>
    /// 关于页面
    /// </summary>
    public partial class AboutPage : Page
    {
        /// <summary>
        /// 初始化关于页面
        /// </summary>
        /// <param name="viewModel">关于页面视图模型</param>
        public AboutPage(AboutPageViewModel viewModel)
        {
            try
            {
                InitializeComponent();
                DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
                Debug.WriteLine("关于页面初始化完成");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"关于页面初始化失败: {ex.Message}");
                throw;
            }
        }
    }
}