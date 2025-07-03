using DrugSearcher.ViewModels;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace DrugSearcher.Views;

/// <summary>
/// 设置页面，提供应用程序的各种配置选项
/// 采用 Fluent Design 设计风格，提供现代化的用户体验
/// </summary>
public partial class SettingsPage
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

            var viewModel1 = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = viewModel1;

            Debug.WriteLine("设置页面初始化完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"设置页面初始化失败: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region 事件处理器

    /// <summary>
    /// 处理搜索框文本变化事件
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">文本变化事件参数</param>
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            var searchText = SearchBox.Text.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                ShowAllSettingsGroups();
                Debug.WriteLine("搜索已清空，显示所有设置组");
            }
            else
            {
                FilterSettingsBySearchText(searchText);
                Debug.WriteLine($"搜索设置: {searchText}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"搜索处理失败: {ex.Message}");
        }
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 显示所有设置组
    /// </summary>
    private void ShowAllSettingsGroups()
    {
        try
        {
            TraySettingsGroup.Visibility = Visibility.Visible;
            UiSettingsGroup.Visibility = Visibility.Visible;
            AppSettingsGroup.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"显示所有设置组失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 根据搜索文本过滤设置项
    /// </summary>
    /// <param name="searchText">搜索文本</param>
    private void FilterSettingsBySearchText(string searchText)
    {
        try
        {
            // 定义各个设置组的关键词
            var settingsCategories = new Dictionary<StackPanel, HashSet<string>>
            {
                {
                    TraySettingsGroup,
                    [
                        "托盘", "tray", "系统托盘", "最小化", "minimize", "通知", "notification",
                        "图标", "icon", "关闭", "close", "显示", "show"
                    ]
                },
                {
                    UiSettingsGroup,
                    [
                        "界面", "ui", "外观", "appearance", "主题", "theme", "颜色", "color",
                        "字体", "font", "语言", "language", "大小", "size", "浅色", "light",
                        "深色", "dark", "模式", "mode"
                    ]
                },
                {
                    AppSettingsGroup,
                    [
                        "应用", "app", "应用程序", "application", "启动", "startup", "开机", "boot",
                        "自启动", "auto", "程序", "program"
                    ]
                }
            };

            // 根据搜索文本过滤设置组
            foreach (var category in settingsCategories)
            {
                var settingsGroup = category.Key;
                var keywords = category.Value;

                var isVisible = keywords.Any(keyword =>
                    keyword.Contains(searchText) ||
                    searchText.Contains(keyword) ||
                    IsTextMatch(keyword, searchText));

                settingsGroup.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"过滤设置项失败: {ex.Message}");
            // 发生错误时显示所有设置组
            ShowAllSettingsGroups();
        }
    }

    /// <summary>
    /// 检查文本是否匹配（支持部分匹配和模糊匹配）
    /// </summary>
    /// <param name="keyword">关键词</param>
    /// <param name="searchText">搜索文本</param>
    /// <returns>是否匹配</returns>
    private static bool IsTextMatch(string keyword, string searchText)
    {
        try
        {
            // 精确匹配
            if (keyword.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                return true;

            // 包含匹配
            if (keyword.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                searchText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;

            // 首字母匹配（支持拼音首字母搜索）
            if (keyword.Length > 0 && searchText.Length > 0 &&
                keyword[0].ToString().Equals(searchText[0].ToString(), StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}