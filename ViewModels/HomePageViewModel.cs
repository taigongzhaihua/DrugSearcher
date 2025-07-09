using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Models;
using DrugSearcher.Services;
using System.Collections.ObjectModel;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.ViewModels
{
    public partial class HomePageViewModel : ObservableObject
    {
        private readonly DrugSearchService _drugSearchService;
        private CancellationTokenSource? _searchCancellationTokenSource;

        public HomePageViewModel(DrugSearchService drugSearchService)
        {
            _drugSearchService = drugSearchService;
            SearchResults = [];
            SearchSuggestions = [];

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
        private DrugInfo? _selectedDrug;

        [ObservableProperty]
        private bool _showSearchSuggestions;

        // 详细信息属性
        [ObservableProperty]
        private string _drugName = string.Empty;

        [ObservableProperty]
        private string _genericName = string.Empty;

        [ObservableProperty]
        private string _manufacturerInfo = string.Empty;

        [ObservableProperty]
        private string _approvalNumber = string.Empty;

        [ObservableProperty]
        private string _dataSourceInfo = string.Empty;

        [ObservableProperty]
        private string _drugDescription = "暂无信息";

        [ObservableProperty]
        private string _indications = "暂无信息";

        [ObservableProperty]
        private string _dosage = "暂无信息";

        [ObservableProperty]
        private string _sideEffects = "暂无信息";

        [ObservableProperty]
        private string _precautions = "暂无信息";

        public ObservableCollection<DrugInfo> SearchResults { get; }
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
        private async Task SelectDrug(DrugInfo drug)
        {
            if (drug != null)
            {
                SelectedDrug = drug;
                await DisplayDrugDetails(drug);
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
                    SearchOnline = IsOnlineEnabled
                };

                var results = await _drugSearchService.SearchDrugsAsync(searchCriteria);

                // 检查是否被取消
                if (_searchCancellationTokenSource.Token.IsCancellationRequested)
                    return;

                foreach (var drug in results)
                {
                    SearchResults.Add(drug);
                }

                UpdateResultCount(results.Count);
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

        private async Task DisplayDrugDetails(DrugInfo drug)
        {
            try
            {
                // 先显示基本信息
                ShowBasicInfo(drug);
                ShowDetailPanel();

                // 异步获取详细信息
                var detailInfo = await _drugSearchService.GetDrugDetailsAsync(drug.Id, drug.DataSource);

                if (detailInfo != null)
                {
                    // 更新详细信息
                    DrugDescription = detailInfo.Description ?? "暂无信息";
                    Indications = detailInfo.Indications ?? "暂无信息";
                    Dosage = detailInfo.Dosage ?? "暂无信息";
                    SideEffects = detailInfo.SideEffects ?? "暂无信息";
                    Precautions = detailInfo.Precautions ?? "暂无信息";

                    // 如果是缓存数据，异步更新在线数据
                    if (detailInfo.DataSource == DataSource.CachedDocuments)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _drugSearchService.UpdateCachedDataAsync(drug.Id);
                            }
                            catch (Exception ex)
                            {
                                // 静默处理更新失败，不影响用户体验
                                System.Diagnostics.Debug.WriteLine($"更新缓存数据失败: {ex.Message}");
                            }
                        });
                    }
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
        /// <param name="value">搜索词</param>
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
        /// <param name="keyword">关键词</param>
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

        private void ShowBasicInfo(DrugInfo drug)
        {
            DrugName = drug.DrugName ?? string.Empty;
            GenericName = drug.GenericName ?? string.Empty;
            ManufacturerInfo = drug.Manufacturer ?? string.Empty;
            ApprovalNumber = drug.ApprovalNumber ?? string.Empty;
            DataSourceInfo = GetDataSourceDisplay(drug.DataSource);
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
        }

        private string GetDataSourceDisplay(DataSource dataSource)
        {
            return dataSource switch
            {
                DataSource.LocalDatabase => "本地数据库",
                DataSource.OnlineSearch => "在线查询",
                DataSource.CachedDocuments => "本地缓存",
                _ => "未知"
            };
        }

        #endregion
    }
}