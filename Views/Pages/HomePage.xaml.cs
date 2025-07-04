using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.Views
{
    /// <summary>
    /// HomePage.xaml 的交互逻辑
    /// </summary>
    public partial class HomePage : Page
    {
        private ObservableCollection<DrugInfo> searchResults;
        private DrugSearchService drugSearchService;

        public HomePage()
        {
            InitializeComponent();
            InitializeData();
        }

        private void InitializeData()
        {
            searchResults = [];
            SearchResultsListBox.ItemsSource = searchResults;
            drugSearchService = new DrugSearchService();

            // 设置默认选中项
            DrugTypeComboBox.SelectedIndex = 0;
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearch();
        }

        private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await PerformSearch();
            }
        }

        private async Task PerformSearch()
        {
            var searchTerm = SearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(searchTerm))
            {
                MessageBox.Show("请输入搜索关键词", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 显示加载状态
            SetLoadingState(true);
            searchResults.Clear();
            HideDetailPanel();

            try
            {
                var searchCriteria = new DrugSearchCriteria
                {
                    SearchTerm = searchTerm,
                    DrugType = DrugTypeComboBox.SelectedItem?.ToString(),
                    Manufacturer = ManufacturerTextBox.Text?.Trim(),
                    Indication = IndicationTextBox.Text?.Trim(),
                    SearchLocalDb = LocalDbCheckBox.IsChecked ?? false,
                    SearchOnline = OnlineSearchCheckBox.IsChecked ?? false,
                    SearchCachedDocs = CachedDocsCheckBox.IsChecked ?? false
                };

                var results = await drugSearchService.SearchDrugsAsync(searchCriteria);

                foreach (var drug in results)
                {
                    searchResults.Add(drug);
                }

                UpdateResultCount(results.Count);
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

        private void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem is DrugInfo selectedDrug)
            {
                DisplayDrugDetails(selectedDrug);
            }
        }

        private async void DisplayDrugDetails(DrugInfo drug)
        {
            try
            {
                // 如果需要获取详细信息，可以异步加载
                var detailInfo = await drugSearchService.GetDrugDetailsAsync(drug.Id, drug.DataSource);

                // 更新基本信息
                DrugNameTextBlock.Text = detailInfo.DrugName;
                GenericNameTextBlock.Text = detailInfo.GenericName;
                ManufacturerInfoTextBlock.Text = detailInfo.Manufacturer;
                ApprovalNumberTextBlock.Text = detailInfo.ApprovalNumber;
                DataSourceInfoTextBlock.Text = GetDataSourceDisplay(detailInfo.DataSource);

                // 更新详细信息
                DrugDescriptionTextBlock.Text = detailInfo.Description ?? "暂无信息";
                IndicationsTextBlock.Text = detailInfo.Indications ?? "暂无信息";
                DosageTextBlock.Text = detailInfo.Dosage ?? "暂无信息";
                SideEffectsTextBlock.Text = detailInfo.SideEffects ?? "暂无信息";
                PrecautionsTextBlock.Text = detailInfo.Precautions ?? "暂无信息";

                ShowDetailPanel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取药物详情时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetLoadingState(bool isLoading)
        {
            SearchProgressBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            SearchButton.IsEnabled = !isLoading;
            SearchStatusTextBlock.Text = isLoading ? "搜索中..." : "";
        }

        private void UpdateResultCount(int count)
        {
            ResultCountTextBlock.Text = $"搜索结果: {count} 条";
        }

        private void ShowDetailPanel()
        {
            DefaultHintTextBlock.Visibility = Visibility.Collapsed;
            DrugInfoPanel.Visibility = Visibility.Visible;
        }

        private void HideDetailPanel()
        {
            DefaultHintTextBlock.Visibility = Visibility.Visible;
            DrugInfoPanel.Visibility = Visibility.Collapsed;
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
    }

    // 数据模型类
    public class DrugInfo
    {
        public string Id { get; set; }
        public string DrugName { get; set; }
        public string GenericName { get; set; }
        public string Manufacturer { get; set; }
        public string ApprovalNumber { get; set; }
        public DataSource DataSource { get; set; }
        public string DataSourceColor => GetDataSourceColor();
        public string Description { get; set; }
        public string Indications { get; set; }
        public string Dosage { get; set; }
        public string SideEffects { get; set; }
        public string Precautions { get; set; }

        private string GetDataSourceColor()
        {
            return DataSource switch
            {
                DataSource.LocalDatabase => "#2196F3",
                DataSource.OnlineSearch => "#4CAF50",
                DataSource.CachedDocuments => "#FF9800",
                _ => "#757575"
            };
        }
    }

    public enum DataSource
    {
        LocalDatabase,
        OnlineSearch,
        CachedDocuments
    }

    public class DrugSearchCriteria
    {
        public string? SearchTerm { get; set; }
        public string DrugType { get; set; }
        public string Manufacturer { get; set; }
        public string Indication { get; set; }
        public bool SearchLocalDb { get; set; }
        public bool SearchOnline { get; set; }
        public bool SearchCachedDocs { get; set; }
    }

    // 搜索服务类（需要实现具体的搜索逻辑）
    public class DrugSearchService
    {
        public async Task<List<DrugInfo>> SearchDrugsAsync(DrugSearchCriteria criteria)
        {
            var results = new List<DrugInfo>();

            // 模拟搜索过程
            await Task.Delay(1000); // 模拟网络延迟

            // 这里需要实现具体的搜索逻辑：
            // 1. 搜索本地SQLite数据库
            if (criteria.SearchLocalDb)
            {
                var localResults = await SearchLocalDatabaseAsync(criteria);
                results.AddRange(localResults);
            }

            // 2. 在线爬虫搜索
            if (criteria.SearchOnline)
            {
                var onlineResults = await SearchOnlineAsync(criteria);
                results.AddRange(onlineResults);
            }

            // 3. 搜索本地缓存的文档
            if (criteria.SearchCachedDocs)
            {
                var cachedResults = await SearchCachedDocumentsAsync(criteria);
                results.AddRange(cachedResults);
            }

            return results.Distinct().ToList(); // 去重
        }

        public async Task<DrugInfo> GetDrugDetailsAsync(string drugId, DataSource dataSource)
        {
            // 根据数据源获取详细信息
            return dataSource switch
            {
                DataSource.LocalDatabase => await GetLocalDrugDetailsAsync(drugId),
                DataSource.OnlineSearch => await GetOnlineDrugDetailsAsync(drugId),
                DataSource.CachedDocuments => await GetCachedDrugDetailsAsync(drugId),
                _ => throw new ArgumentException("不支持的数据源")
            };
        }

        private async Task<List<DrugInfo>> SearchLocalDatabaseAsync(DrugSearchCriteria criteria)
        {
            // TODO: 实现SQLite数据库搜索
            await Task.Delay(100);
            return
            [
                new DrugInfo()
                {
                    Id = "local_1",
                    DrugName = "阿司匹林肠溶片",
                    GenericName = "阿司匹林",
                    Manufacturer = "拜耳医药",
                    ApprovalNumber = "H12345678",
                    DataSource = DataSource.LocalDatabase
                }
            ];
        }

        private async Task<List<DrugInfo>> SearchOnlineAsync(DrugSearchCriteria criteria)
        {
            // TODO: 实现在线爬虫搜索
            await Task.Delay(2000);
            return
            [
                new DrugInfo()
                {
                    Id = "online_1",
                    DrugName = "布洛芬缓释胶囊",
                    GenericName = "布洛芬",
                    Manufacturer = "强生制药",
                    ApprovalNumber = "H98765432",
                    DataSource = DataSource.OnlineSearch
                }
            ];
        }

        private async Task<List<DrugInfo>> SearchCachedDocumentsAsync(DrugSearchCriteria criteria)
        {
            // TODO: 实现本地文档数据库搜索
            await Task.Delay(50);
            return
            [
                new DrugInfo()
                {
                    Id = "cached_1",
                    DrugName = "对乙酰氨基酚片",
                    GenericName = "对乙酰氨基酚",
                    Manufacturer = "华润双鹤",
                    ApprovalNumber = "H11223344",
                    DataSource = DataSource.CachedDocuments
                }
            ];
        }

        private async Task<DrugInfo> GetLocalDrugDetailsAsync(string drugId)
        {
            // TODO: 从SQLite获取详细信息
            await Task.Delay(100);
            return new DrugInfo
            {
                Id = drugId,
                DrugName = "阿司匹林肠溶片",
                GenericName = "阿司匹林",
                Manufacturer = "拜耳医药",
                ApprovalNumber = "H12345678",
                DataSource = DataSource.LocalDatabase,
                Description = "本品为白色肠溶片，除去肠溶衣后显白色。",
                Indications = "用于发热、头痛、神经痛、牙痛、月经痛、肌肉痛、关节痛等。",
                Dosage = "口服。成人每次300-600mg，每日3次。",
                SideEffects = "可见胃肠道不适、恶心、呕吐等。",
                Precautions = "有消化道溃疡史者慎用。"
            };
        }

        private async Task<DrugInfo> GetOnlineDrugDetailsAsync(string drugId)
        {
            // TODO: 在线获取详细信息并缓存
            await Task.Delay(2000);
            return new DrugInfo
            {
                Id = drugId,
                DrugName = "布洛芬缓释胶囊",
                GenericName = "布洛芬",
                Manufacturer = "强生制药",
                ApprovalNumber = "H98765432",
                DataSource = DataSource.OnlineSearch,
                Description = "本品为硬胶囊剂，内容物为白色或类白色颗粒。",
                Indications = "用于缓解轻至中度疼痛，如头痛、关节痛、偏头痛、牙痛、肌肉痛、神经痛、痛经。",
                Dosage = "口服。成人每次200mg，每日2-3次。",
                SideEffects = "可见消化不良、恶心、腹痛等胃肠道反应。",
                Precautions = "对本品过敏者禁用。"
            };
        }

        private async Task<DrugInfo> GetCachedDrugDetailsAsync(string drugId)
        {
            // TODO: 从本地文档数据库获取详细信息
            await Task.Delay(50);
            return new DrugInfo
            {
                Id = drugId,
                DrugName = "对乙酰氨基酚片",
                GenericName = "对乙酰氨基酚",
                Manufacturer = "华润双鹤",
                ApprovalNumber = "H11223344",
                DataSource = DataSource.CachedDocuments,
                Description = "本品为白色片剂。",
                Indications = "用于普通感冒或流行性感冒引起的发热，也用于缓解轻至中度疼痛。",
                Dosage = "口服。成人每次500mg，每日3-4次。",
                SideEffects = "偶见皮疹、荨麻疹等过敏反应。",
                Precautions = "严重肝肾功能不全者禁用。"
            };
        }
    }
}