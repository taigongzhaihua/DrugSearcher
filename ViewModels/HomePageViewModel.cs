using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Configuration;
using DrugSearcher.Helpers;
using DrugSearcher.Models;
using DrugSearcher.Services;
using DrugSearcher.Views;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.ViewModels;

public partial class HomePageViewModel : ObservableObject
{
    private readonly DrugSearchService _drugSearchService;
    private readonly JavaScriptDosageCalculatorService _calculatorService;
    private CancellationTokenSource? _searchCancellationTokenSource;
    private readonly DosageCalculatorAiService _aiService;
    private Dictionary<string, string> _cachedMarkdownContents = [];
    private List<(string Key, string Header, bool IsSpecial)> _tabDefinitions;
    public HomePageViewModel(DrugSearchService drugSearchService,
        JavaScriptDosageCalculatorService calculatorService,
        DosageCalculatorAiService aiService)
    {
        _drugSearchService = drugSearchService;
        _calculatorService = calculatorService;
        SearchResults = [];
        SearchSuggestions = [];
        TabItems = [];

        // 初始化计算器相关集合
        AvailableCalculators = [];
        CalculatorParameters = [];
        CalculationResults = [];

        // 设置默认值
        IsLocalDbEnabled = true;
        IsOnlineEnabled = true;
        ResultCount = "搜索结果: 0 条";
        CalculatorStatusMessage = "请选择药物以查看可用的计算器";
        _aiService = aiService;

        // 初始化标签页
        InitializeTabItems();
    }


    #region 初始化方法

    private void InitializeTabItems()
    {
        // 定义所有可能的标签页
        _tabDefinitions =
        [
            ("FullDetails", "全部详情", false),
            ("MainIngredients", "主要成分", false),
            ("Appearance", "性状", false),
            ("DrugDescription", "药物说明", false),
            ("Indications", "适应症", false),
            ("Dosage", "用法用量", true), // 特殊标签页
            ("SideEffects", "不良反应", false),
            ("Precautions", "注意事项", false),
            ("Contraindications", "禁忌", false),
            ("PregnancyAndLactation", "孕妇及哺乳期妇女用药", false),
            ("PediatricUse", "儿童用药", false),
            ("GeriatricUse", "老人用药", false),
            ("DrugInteractions", "药物相互作用", false),
            ("Pharmacology", "药理毒理", false),
            ("Pharmacokinetics", "药代动力学", false),
            ("Storage", "储存信息", false),
            ("TcmRemarks", "备注", false)
        ];
    }

    #endregion
    #region 原有属性 (搜索相关) - 保持不变

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

    // 添加AI生成相关属性
    [ObservableProperty]
    private bool _isGeneratingCalculator;

    [ObservableProperty]
    private string _generationStatus = string.Empty;

    // 新增：Tab相关属性
    [ObservableProperty]
    private TabItemViewModel? _selectedTab;

    public ObservableCollection<UnifiedDrugSearchResult?> SearchResults { get; }
    public ObservableCollection<string> SearchSuggestions { get; }
    public ObservableCollection<TabItemViewModel> TabItems { get; }

    // 计算器相关属性
    public BaseDrugInfo? SelectedDrugInfo => SelectedDrug?.DrugInfo;

    #endregion

    #region 新增属性 (计算器相关)

    [ObservableProperty]
    private DosageCalculator? _selectedCalculator;

    [ObservableProperty]
    private bool _isCalculatorLoading;

    [ObservableProperty]
    private bool _isCalculating;

    [ObservableProperty]
    private bool _hasCalculationResults;

    [ObservableProperty]
    private bool _hasCalculators;

    [ObservableProperty]
    private string _calculatorStatusMessage = "请选择药物以查看可用的计算器";

    [ObservableProperty]
    private int _generationProgress;

    [ObservableProperty]
    private string _generationStreamContent = string.Empty;
    public ObservableCollection<DosageCalculator> AvailableCalculators { get; }
    public ObservableCollection<DosageParameter> CalculatorParameters { get; }
    public ObservableCollection<DosageCalculationResult> CalculationResults { get; }

    #endregion

    #region 原有命令 (搜索相关)

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrEmpty(SearchTerm.Trim()))
        {
            MessageBox.Show("请输入搜索关键词", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await PerformSearch();
    }

    [RelayCommand]
    private async Task SelectDrug(UnifiedDrugSearchResult? drugResult)
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

        // 清空计算器状态
        ResetCalculatorState();
    }

    [RelayCommand]
    private void HideSuggestions() => ShowSearchSuggestions = false;

    #endregion

    #region 新增命令 (计算器相关)

    [RelayCommand]
    private async Task LoadCalculatorsForDrug(BaseDrugInfo drugInfo)
    {
        try
        {
            IsCalculatorLoading = true;
            CalculatorStatusMessage = "正在加载计算器...";

            var calculators = await _calculatorService.GetCalculatorsForDrugAsync(drugInfo);

            AvailableCalculators.Clear();
            foreach (var calculator in calculators)
            {
                AvailableCalculators.Add(calculator);
            }

            HasCalculators = AvailableCalculators.Count > 0;

            if (HasCalculators)
            {
                // 自动选择第一个计算器
                SelectedCalculator = AvailableCalculators[0];
                await LoadCalculatorParameters();
                CalculatorStatusMessage = $"找到 {AvailableCalculators.Count} 个计算器";
            }
            else
            {
                CalculatorStatusMessage = "该药物暂无可用的计算器";
                ClearCalculatorData();
            }
        }
        catch (Exception ex)
        {
            CalculatorStatusMessage = $"加载计算器失败: {ex.Message}";
            HasCalculators = false;
            ClearCalculatorData();
        }
        finally
        {
            IsCalculatorLoading = false;
        }
    }
    [RelayCommand]
    private async Task LoadCalculatorParameters()
    {
        if (SelectedCalculator == null) return;

        try
        {
            var parameters = await _calculatorService.GetCalculatorParametersAsync(SelectedCalculator.Id);

            CalculatorParameters.Clear();
            foreach (var param in parameters)
            {
                // 参数的Value已经在服务层设置好了
                CalculatorParameters.Add(param);
            }

            // 清空之前的计算结果
            CalculationResults.Clear();
            HasCalculationResults = false;

            CalculatorStatusMessage = parameters.Count > 0
                ? $"已加载 {parameters.Count} 个参数"
                : "该计算器没有参数定义";
        }
        catch (Exception ex)
        {
            CalculatorStatusMessage = $"加载参数失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Calculate()
    {
        if (SelectedCalculator == null) return;

        // 验证必填参数
        var missingParams = CalculatorParameters.Where(p => p.IsRequired &&
            (string.IsNullOrWhiteSpace(p.Value.ToString()))).ToList();

        if (missingParams.Count != 0)
        {
            var missingNames = string.Join(", ", missingParams.Select(p => p.DisplayName));
            MessageBox.Show($"请填写必填参数: {missingNames}", "参数验证",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsCalculating = true;
        CalculationResults.Clear();
        HasCalculationResults = false;
        CalculatorStatusMessage = "正在计算...";

        try
        {
            // 构建参数字典
            var paramDict = new Dictionary<string, object>();
            foreach (var param in CalculatorParameters)
            {
                // 根据参数类型转换值
                object value = param.DataType switch
                {
                    ParameterTypes.Number => Convert.ToDouble(param.Value),
                    ParameterTypes.Boolean => Convert.ToBoolean(param.Value),
                    _ => param.Value.ToString() ?? string.Empty
                };
                if (param.Name != null) paramDict[param.Name] = value;
            }

            var request = new DosageCalculationRequest
            {
                CalculatorId = SelectedCalculator.Id,
                Parameters = paramDict
            };

            var results = await _calculatorService.CalculateDosageAsync(request);

            foreach (var result in results)
            {
                CalculationResults.Add(result);
            }

            HasCalculationResults = CalculationResults.Count > 0;
            CalculatorStatusMessage = HasCalculationResults ?
                $"计算完成，共 {CalculationResults.Count} 个结果" : "计算完成，无结果";
        }
        catch (Exception ex)
        {
            CalculationResults.Add(new DosageCalculationResult
            {
                Description = "计算错误",
                IsWarning = true,
                WarningMessage = ex.Message
            });
            HasCalculationResults = true;
            CalculatorStatusMessage = "计算失败";
        }
        finally
        {
            IsCalculating = false;
        }
    }

    [RelayCommand]
    private async Task CreateCalculator()
    {
        if (SelectedDrugInfo == null) return;

        try
        {
            var calculatorService = ContainerAccessor.Resolve<JavaScriptDosageCalculatorService>();
            var logger = ContainerAccessor.Resolve<ILogger<CalculatorEditorViewModel>>();

            var viewModel = new CalculatorEditorViewModel(calculatorService, logger, SelectedDrugInfo);
            var window = new CalculatorEditorWindow();
            window.SetViewModel(viewModel);
            window.Owner = Application.Current.MainWindow;

            if (window.ShowDialog() == true)
            {
                // 重新加载计算器列表
                await LoadCalculatorsForDrug(SelectedDrugInfo);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"创建计算器时发生错误: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // 更新编辑计算器命令
    [RelayCommand]
    private async Task EditCalculator()
    {
        if (SelectedCalculator == null || SelectedDrugInfo == null) return;

        try
        {
            var calculatorService = ContainerAccessor.Resolve<JavaScriptDosageCalculatorService>();
            var logger = ContainerAccessor.Resolve<ILogger<CalculatorEditorViewModel>>();

            var viewModel = new CalculatorEditorViewModel(calculatorService, logger, SelectedDrugInfo, SelectedCalculator);
            var window = new CalculatorEditorWindow();
            window.SetViewModel(viewModel);
            window.Owner = Application.Current.MainWindow;

            if (window.ShowDialog() == true)
            {
                // 重新加载计算器列表
                await LoadCalculatorsForDrug(SelectedDrugInfo);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"编辑计算器时发生错误: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ClearCalculationResults()
    {
        CalculationResults.Clear();
        HasCalculationResults = false;
        CalculatorStatusMessage = "结果已清空";
    }

    // 添加AI生成命令
    [RelayCommand]
    private async Task GenerateCalculatorWithAi()
    {
        if (SelectedDrugInfo == null)
        {
            MessageBox.Show("请先选择药物", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = ShowCalculatorGenerationDialog();
        if (result == null) return;

        IsGeneratingCalculator = true;
        GenerationStatus = "正在准备生成计算器...";
        GenerationProgress = 0;
        GenerationStreamContent = string.Empty;

        try
        {
            DosageCalculatorGenerationResult? finalResult = null;

            // 在后台线程运行流式生成
            await Task.Run(async () =>
            {
                await foreach (var progress in _aiService.GenerateCalculatorStreamAsync(
                    SelectedDrugInfo,
                    result.CalculatorType,
                    result.AdditionalRequirements))
                {
                    // 在UI线程更新进度
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        GenerationStatus = progress.Message;
                        GenerationProgress = progress.Progress;

                        // 显示部分内容（可选）
                        if (!string.IsNullOrEmpty(progress.StreamChunk))
                        {
                            GenerationStreamContent += progress.StreamChunk;
                        }

                        // 保存最终结果
                        if (progress.Result != null)
                        {
                            finalResult = progress.Result;
                        }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }).ConfigureAwait(false);

            if (finalResult is { Success: true, Calculator: not null })
            {
                GenerationStatus = "正在保存计算器...";

                // 在后台线程保存计算器
                var savedCalculator = await _calculatorService.SaveCalculatorAsync(finalResult.Calculator).ConfigureAwait(false);

                // 在UI线程更新界面
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    // 重新加载计算器列表
                    await LoadCalculatorsForDrug(SelectedDrugInfo);

                    // 选择新生成的计算器
                    var newCalculator = AvailableCalculators.FirstOrDefault(c => c.Id == savedCalculator.Id);
                    if (newCalculator != null)
                    {
                        SelectedCalculator = newCalculator;
                        await LoadCalculatorParameters();
                    }

                    GenerationStatus = "计算器生成成功！";
                    MessageBox.Show($"计算器 '{savedCalculator.CalculatorName}' 已生成并保存", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    GenerationStatus = "计算器生成失败";
                    MessageBox.Show($"生成计算器失败: {finalResult?.ErrorMessage ?? "未知错误"}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                GenerationStatus = "计算器生成失败";
                MessageBox.Show($"生成计算器时发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
        finally
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsGeneratingCalculator = false;
                GenerationProgress = 0;
                GenerationStreamContent = string.Empty;
            });
        }
    }

    #endregion

    #region 私有方法 (搜索相关)

    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
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
                SearchTerm = SearchTerm.Trim(),
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
    private async Task DisplayDrugDetails(UnifiedDrugSearchResult? drugResult)
    {
        try
        {
            // 显示基本信息
            ShowBasicInfo(drugResult);
            ShowDetailPanel();

            // 异步获取详细信息（如果需要）
            if (drugResult != null)
            {
                var detailInfo = await _drugSearchService.GetDrugDetailsAsync(
                    drugResult.DrugInfo.Id,
                    drugResult.DrugInfo.DataSource);

                if (detailInfo != null)
                {
                    // 更新详细信息
                    await UpdateDetailInfo(detailInfo);
                }
            }

            // 加载计算器
            if (SelectedDrugInfo != null)
            {
                await LoadCalculatorsForDrug(SelectedDrugInfo);
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
    partial void OnSearchTermChanged(string value) =>
        // 延迟获取建议，避免频繁请求
        Task.Delay(300).ContinueWith(async _ =>
        {
            if (SearchTerm == value && !string.IsNullOrWhiteSpace(value) && value.Length >= 2)
            {
                await GetSearchSuggestions(value);
            }
        });

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
            Debug.WriteLine($"获取搜索建议失败: {ex.Message}");
        }
    }

    private void ShowBasicInfo(UnifiedDrugSearchResult? drugResult)
    {
        var drugInfo = drugResult?.DrugInfo;

        if (drugInfo == null) return;
        DrugName = drugInfo.DrugName;
        ManufacturerInfo = drugInfo.Manufacturer ?? string.Empty;
        ApprovalNumber = drugInfo.ApprovalNumber ?? string.Empty;
        Specification = drugInfo.Specification ?? string.Empty;
        DataSourceInfo = drugInfo.GetDataSourceText();

        // 显示匹配信息
        if (drugResult != null)
        {
            var matchScore = (drugResult.MatchScore * 100).ToString("F1");
            var matchedFields = string.Join(", ", drugResult.MatchedFields);
            MatchInfo = $"匹配度: {matchScore}% | 匹配字段: {matchedFields}";
        }

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

    /// <summary>
    /// 显示计算器生成对话框
    /// </summary>
    private static CalculatorGenerationRequest? ShowCalculatorGenerationDialog()
    {
        var dialog = new CalculatorGenerationDialog();
        if (dialog.ShowDialog() == true)
        {
            return new CalculatorGenerationRequest
            {
                CalculatorType = dialog.CalculatorType,
                AdditionalRequirements = dialog.AdditionalRequirements
            };
        }
        return null;
    }

    /// <summary>
    /// 计算器生成请求
    /// </summary>
    public class CalculatorGenerationRequest
    {
        public string CalculatorType { get; set; } = "通用剂量计算器";
        public string AdditionalRequirements { get; set; } = string.Empty;
    }

    private async Task UpdateDetailInfo(BaseDrugInfo drugInfo)
    {
        // 使用 Helper 类生成 Markdown 内容
        _cachedMarkdownContents = DrugInfoMarkdownHelper.ConvertToMarkdownDictionary(drugInfo);

        // 更新标签页可见性和内容
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            TabItems.Clear();
            foreach (var content in _cachedMarkdownContents.Where(content => !string.IsNullOrWhiteSpace(content.Value)))
            {
                var (key, header, isSpecial) = _tabDefinitions.FirstOrDefault(t => t.Key == content.Key);

                var tabItem = new TabItemViewModel(header, key, isSpecial)
                {
                    Content = content.Value,
                    IsVisible = true,
                    IsLoaded = true
                };
                TabItems.Add(tabItem);
            }


            // 选择第一个可见的标签页
            var firstVisibleTab = TabItems.FirstOrDefault(t => t.IsVisible);
            if (firstVisibleTab != null)
            {
                SelectedTab = firstVisibleTab;
            }
        });
    }

    private void SetLoadingState(bool isLoading)
    {
        IsLoading = isLoading;
        SearchStatus = isLoading ? "搜索中..." : string.Empty;
    }

    private void UpdateResultCount(int count) => ResultCount = $"搜索结果: {count} 条";

    private void ShowDetailPanel() => IsDetailPanelVisible = true;

    private void HideDetailPanel()
    {
        IsDetailPanelVisible = false;
        _cachedMarkdownContents.Clear();

        // 重置所有标签页
        foreach (var tabItem in TabItems)
        {
            tabItem.IsVisible = false;
            tabItem.IsLoaded = false;
            tabItem.Content = string.Empty;
        }

        // 隐藏详情面板时也重置计算器状态
        ResetCalculatorState();
    }

    #endregion

    #region 私有方法 (计算器相关)

    private void ClearCalculatorData()
    {
        SelectedCalculator = null;
        CalculatorParameters.Clear();
        CalculationResults.Clear();
        HasCalculationResults = false;
    }

    private void ResetCalculatorState()
    {
        AvailableCalculators.Clear();
        HasCalculators = false;
        ClearCalculatorData();
        CalculatorStatusMessage = "请选择药物以查看可用的计算器";
    }

    #endregion
}