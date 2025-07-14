using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Helpers;
using DrugSearcher.Models;
using DrugSearcher.Services;
using System.Collections.ObjectModel;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.ViewModels;

public partial class HomePageViewModel : ObservableObject
{
    private readonly DrugSearchService _drugSearchService;
    private CancellationTokenSource? _searchCancellationTokenSource;

    public HomePageViewModel(DrugSearchService drugSearchService)
    {
        _drugSearchService = drugSearchService;
        SearchResults = [];
        SearchSuggestions = [];
        MarkdownContents = DrugInfoMarkdownHelper.ConvertToMarkdownDictionary(new LocalDrugInfo());

        // 设置默认值
        IsLocalDbEnabled = true;
        IsOnlineEnabled = true;
        ResultCount = "搜索结果: 0 条";
    }

    #region 属性

    [ObservableProperty]
    private string _searchTerm = string.Empty;

    [ObservableProperty]
    private bool _isLocalDbEnabled;

    [ObservableProperty]
    private bool _isOnlineEnabled;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _resultCount = string.Empty;

    [ObservableProperty]
    private string _searchStatus = string.Empty;

    [ObservableProperty]
    private bool _isDetailPanelVisible;

    [ObservableProperty]
    private UnifiedDrugSearchResult? _selectedDrug;

    [ObservableProperty]
    private bool _showSearchSuggestions;

    // 基本信息属性
    [ObservableProperty]
    private string _drugName = string.Empty;

    [ObservableProperty]
    private string _genericName = string.Empty;

    [ObservableProperty]
    private string _tradeName = string.Empty;

    [ObservableProperty]
    private string _manufacturerInfo = string.Empty;

    [ObservableProperty]
    private string _approvalNumber = string.Empty;

    [ObservableProperty]
    private string _specification = string.Empty;

    [ObservableProperty]
    private string _dataSourceInfo = string.Empty;

    [ObservableProperty]
    private string _matchInfo = string.Empty;

    // 中医信息属性
    [ObservableProperty]
    private string _tcmDisease = string.Empty;

    [ObservableProperty]
    private string _tcmSyndrome = string.Empty;

    // Markdown 内容字典
    [ObservableProperty]
    private Dictionary<string, string> _markdownContents;

    public ObservableCollection<UnifiedDrugSearchResult> SearchResults { get; }
    public ObservableCollection<string> SearchSuggestions { get; }

    #endregion

    #region 命令

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrEmpty(SearchTerm?.Trim()))
        {
            MessageBox.Show("请输入搜索关键词", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await PerformSearch();
    }

    [RelayCommand]
    private async Task SelectDrug(UnifiedDrugSearchResult drugResult)
    {
        if (drugResult != null)
        {
            SelectedDrug = drugResult;
            await DisplayDrugDetails(drugResult);
        }
    }

    [RelayCommand]
    private async Task SelectSuggestion(string suggestion)
    {
        if (!string.IsNullOrEmpty(suggestion))
        {
            SearchTerm = suggestion;
            ShowSearchSuggestions = false;
            await PerformSearch();
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchTerm = string.Empty;
        SearchResults.Clear();
        SearchSuggestions.Clear();
        ShowSearchSuggestions = false;
        HideDetailPanel();
        ResultCount = "搜索结果: 0 条";
    }

    [RelayCommand]
    private void HideSuggestions()
    {
        ShowSearchSuggestions = false;
    }

    #endregion

    #region 私有方法

    private async Task PerformSearch()
    {
        // 取消之前的搜索
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();

        SetLoadingState(true);
        SearchResults.Clear();
        HideDetailPanel();
        ShowSearchSuggestions = false;

        try
        {
            var searchCriteria = new DrugSearchCriteria
            {
                SearchTerm = SearchTerm?.Trim(),
                SearchLocalDb = IsLocalDbEnabled,
                SearchOnline = IsOnlineEnabled,
                MaxResults = 100,
                MinMatchScore = 0.1
            };

            var results = await _drugSearchService.SearchDrugsAsync(searchCriteria);

            // 检查是否被取消
            if (_searchCancellationTokenSource.Token.IsCancellationRequested)
                return;

            foreach (var drugResult in results)
            {
                SearchResults.Add(drugResult);
            }

            UpdateResultCount(results.Count);

            // 如果有结果，自动选择第一个
            if (results.Count > 0)
            {
                await SelectDrug(results[0]);
            }
        }
        catch (OperationCanceledException)
        {
            // 搜索被取消，不需要处理
        }
        catch (Exception ex)
        {
            MessageBox.Show($"搜索时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private async Task DisplayDrugDetails(UnifiedDrugSearchResult drugResult)
    {
        try
        {
            // 显示基本信息
            ShowBasicInfo(drugResult);
            ShowDetailPanel();

            // 异步获取详细信息（如果需要）
            var detailInfo = await _drugSearchService.GetDrugDetailsAsync(
                drugResult.DrugInfo.Id,
                drugResult.DrugInfo.DataSource);

            if (detailInfo != null)
            {
                // 更新详细信息
                UpdateDetailInfo(detailInfo);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"获取药物详情时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 搜索词变化时获取建议
    /// </summary>
    partial void OnSearchTermChanged(string value)
    {
        // 延迟获取建议，避免频繁请求
        Task.Delay(300).ContinueWith(async _ =>
        {
            if (SearchTerm == value && !string.IsNullOrWhiteSpace(value) && value.Length >= 2)
            {
                await GetSearchSuggestions(value);
            }
        });
    }

    /// <summary>
    /// 获取搜索建议
    /// </summary>
    private async Task GetSearchSuggestions(string keyword)
    {
        try
        {
            var suggestions = await _drugSearchService.GetSearchSuggestionsAsync(keyword);

            // 在UI线程中更新建议
            Application.Current.Dispatcher.Invoke(() =>
            {
                SearchSuggestions.Clear();
                foreach (var suggestion in suggestions)
                {
                    SearchSuggestions.Add(suggestion);
                }
                ShowSearchSuggestions = suggestions.Count > 0;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"获取搜索建议失败: {ex.Message}");
        }
    }

    private void ShowBasicInfo(UnifiedDrugSearchResult drugResult)
    {
        var drugInfo = drugResult.DrugInfo;

        DrugName = drugInfo.DrugName ?? string.Empty;
        ManufacturerInfo = drugInfo.Manufacturer ?? string.Empty;
        ApprovalNumber = drugInfo.ApprovalNumber ?? string.Empty;
        Specification = drugInfo.Specification ?? string.Empty;
        DataSourceInfo = drugInfo.GetDataSourceText();

        // 显示匹配信息
        var matchScore = (drugResult.MatchScore * 100).ToString("F1");
        var matchedFields = string.Join(", ", drugResult.MatchedFields);
        MatchInfo = $"匹配度: {matchScore}% | 匹配字段: {matchedFields}";

        switch (drugInfo)
        {
            // 根据不同的数据源显示不同的信息
            case LocalDrugInfo localDrug:
                GenericName = localDrug.GenericName ?? string.Empty;
                TradeName = string.Empty;

                // 设置中医信息
                TcmDisease = localDrug.TcmDisease ?? string.Empty;
                TcmSyndrome = localDrug.TcmSyndrome ?? string.Empty;
                break;
            case OnlineDrugInfo onlineDrug:
                GenericName = string.Empty;
                TradeName = onlineDrug.TradeName ?? string.Empty;

                // 清空中医信息
                TcmDisease = string.Empty;
                TcmSyndrome = string.Empty;
                break;
            default:
                GenericName = string.Empty;
                TradeName = string.Empty;
                TcmDisease = string.Empty;
                TcmSyndrome = string.Empty;
                break;
        }
    }

    private void UpdateDetailInfo(BaseDrugInfo drugInfo)
    {
        // 使用 Helper 类生成 Markdown 内容
        var newMarkdownContents = DrugInfoMarkdownHelper.ConvertToMarkdownDictionary(drugInfo);

        // 更新字典（这会触发 UI 更新）
        MarkdownContents = newMarkdownContents;
    }

    private void SetLoadingState(bool isLoading)
    {
        IsLoading = isLoading;
        SearchStatus = isLoading ? "搜索中..." : string.Empty;
    }

    private void UpdateResultCount(int count)
    {
        ResultCount = $"搜索结果: {count} 条";
    }

    private void ShowDetailPanel()
    {
        IsDetailPanelVisible = true;
    }

    private void HideDetailPanel()
    {
        IsDetailPanelVisible = false;
        MarkdownContents.Clear();
    }

    #endregion
}