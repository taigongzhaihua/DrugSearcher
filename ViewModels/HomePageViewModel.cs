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
    private readonly DosageCalculatorAiService _aiService;
    private CancellationTokenSource? _searchCancellationTokenSource;
    private Dictionary<string, string> _cachedMarkdownContents = [];
    private List<(string Key, string Header, bool IsSpecial)>? _tabDefinitions;

    // 分页相关
    private const int PAGE_SIZE = 30;
    private string _lastSearchTerm = string.Empty;

    public HomePageViewModel(
        DrugSearchService drugSearchService,
        JavaScriptDosageCalculatorService calculatorService,
        DosageCalculatorAiService aiService)
    {
        _drugSearchService = drugSearchService;
        _calculatorService = calculatorService;
        _aiService = aiService;

        InitializeDefaultValues();
        InitializeTabItems();
    }

    #region 初始化方法

    private void InitializeDefaultValues()
    {
        IsLocalDbEnabled = true;
        IsOnlineEnabled = true;
        ResultCount = "搜索结果: 0 条";
        CalculatorStatusMessage = "请选择药物以查看可用的计算器";
    }

    private void InitializeTabItems()
    {
        _tabDefinitions =
        [
            ("FullDetails", "全部详情", false),
            ("MainIngredients", "主要成分", false),
            ("Appearance", "性状", false),
            ("DrugDescription", "药物说明", false),
            ("Indications", "适应症", false),
            ("Dosage", "用法用量", true),
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

    #region 属性

    // 搜索相关
    [ObservableProperty]
    public partial string SearchTerm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLocalDbEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsOnlineEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ResultCount { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsDetailPanelVisible { get; set; }

    [ObservableProperty]
    public partial UnifiedDrugSearchResult? SelectedDrug { get; set; }

    [ObservableProperty]
    public partial bool ShowSearchSuggestions { get; set; }

    // 基本信息
    [ObservableProperty]
    public partial string DrugName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GenericName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TradeName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ManufacturerInfo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApprovalNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Specification { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DataSourceInfo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MatchInfo { get; set; } = string.Empty;

    // 中医信息
    [ObservableProperty]
    public partial string TcmDisease { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TcmSyndrome { get; set; } = string.Empty;

    // 计算器相关
    [ObservableProperty]
    public partial DosageCalculator? SelectedCalculator { get; set; }

    [ObservableProperty]
    public partial bool IsCalculatorLoading { get; set; }

    [ObservableProperty]
    public partial bool IsCalculating { get; set; }

    [ObservableProperty]
    public partial bool HasCalculationResults { get; set; }

    [ObservableProperty]
    public partial bool HasCalculators { get; set; }

    [ObservableProperty]
    public partial string CalculatorStatusMessage { get; set; } = "请选择药物以查看可用的计算器";

    // AI生成相关
    [ObservableProperty]
    public partial bool IsGeneratingCalculator { get; set; }

    [ObservableProperty]
    public partial string GenerationStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int GenerationProgress { get; set; }

    [ObservableProperty]
    public partial string GenerationStreamContent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GenerationReasoningContent { get; set; } = string.Empty;

    // Tab相关
    [ObservableProperty]
    public partial TabItemViewModel? SelectedTab { get; set; }

    public ObservableCollection<UnifiedDrugSearchResult?> SearchResults { get; } = [];

    public ObservableCollection<string> SearchSuggestions { get; } = [];

    public ObservableCollection<TabItemViewModel> TabItems { get; } = [];

    public ObservableCollection<DosageCalculator> AvailableCalculators { get; } = [];

    public ObservableCollection<DosageParameter> CalculatorParameters { get; } = [];

    public ObservableCollection<DosageCalculationResult> CalculationResults { get; } = [];

    public BaseDrugInfo? SelectedDrugInfo => SelectedDrug?.DrugInfo;

    #endregion

    #region 分页相关属性

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int TotalPages { get; set; } = 1;

    [ObservableProperty]
    public partial bool HasPreviousPage { get; set; }

    [ObservableProperty]
    public partial bool HasNextPage { get; set; }

    [ObservableProperty]
    public partial bool IsLoadingMore { get; set; }

    [ObservableProperty]
    public partial string PageInfo { get; set; } = string.Empty;

    #endregion

    #region 命令

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm))
        {
            MessageBox.Show("请输入搜索关键词", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 新搜索时重置页码
        CurrentPage = 1;
        _lastSearchTerm = SearchTerm.Trim();
        await PerformSearch(true);
    }

    [RelayCommand]
    private async Task LoadNextPage()
    {
        if (!HasNextPage || IsLoadingMore) return;
        CurrentPage++;
        await PerformSearch(false);
    }

    [RelayCommand]
    private async Task LoadPreviousPage()
    {
        if (!HasPreviousPage || IsLoadingMore) return;
        CurrentPage--;
        await PerformSearch(false);
    }

    [RelayCommand]
    private async Task GoToPage(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > TotalPages || IsLoadingMore) return;
        CurrentPage = pageNumber;
        await PerformSearch(false);
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
        ResetCalculatorState();
    }

    [RelayCommand]
    private void HideSuggestions() => ShowSearchSuggestions = false;

    [RelayCommand]
    private async Task LoadCalculatorsForDrug(BaseDrugInfo drugInfo)
    {
        await ExecuteWithLoadingState(
            async () =>
            {
                var calculators = await _calculatorService.GetCalculatorsForDrugAsync(drugInfo);

                AvailableCalculators.Clear();
                foreach (var calculator in calculators)
                {
                    AvailableCalculators.Add(calculator);
                }

                HasCalculators = AvailableCalculators.Count > 0;

                if (HasCalculators)
                {
                    SelectedCalculator = AvailableCalculators[0];
                    await LoadCalculatorParameters();
                    CalculatorStatusMessage = $"找到 {AvailableCalculators.Count} 个计算器";
                }
                else
                {
                    CalculatorStatusMessage = "该药物暂无可用的计算器";
                    ClearCalculatorData();
                }
            },
            loading => IsCalculatorLoading = loading,
            "正在加载计算器...",
            "加载计算器失败"
        );
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
                CalculatorParameters.Add(param);
            }

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

        var missingParams = ValidateRequiredParameters();
        if (missingParams.Count != 0)
        {
            ShowValidationError(missingParams);
            return;
        }

        await ExecuteCalculation();
    }

    [RelayCommand]
    private async Task CreateCalculator()
    {
        if (SelectedDrugInfo == null) return;
        await ShowCalculatorEditor();
    }

    [RelayCommand]
    private async Task EditCalculator()
    {
        if (SelectedCalculator == null || SelectedDrugInfo == null) return;
        await ShowCalculatorEditor(SelectedCalculator);
    }

    [RelayCommand]
    private void ClearCalculationResults()
    {
        CalculationResults.Clear();
        HasCalculationResults = false;
        CalculatorStatusMessage = "结果已清空";
    }

    [RelayCommand]
    private async Task GenerateCalculatorWithAi()
    {
        if (SelectedDrugInfo == null)
        {
            MessageBox.Show("请先选择药物", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var request = ShowCalculatorGenerationDialog();
        if (request == null) return;

        await GenerateCalculatorWithAiStream(request);
    }

    #endregion

    #region 私有方法

    [SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
    private async Task PerformSearch()
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();

        await ExecuteWithLoadingState(
            async () =>
            {
                SearchResults.Clear();
                HideDetailPanel();
                ShowSearchSuggestions = false;

                var searchCriteria = new DrugSearchCriteria
                {
                    SearchTerm = SearchTerm.Trim(),
                    SearchLocalDb = IsLocalDbEnabled,
                    SearchOnline = IsOnlineEnabled,
                    MaxResults = 100,
                    MinMatchScore = 0.1
                };

                var results = await _drugSearchService.SearchDrugsAsync(searchCriteria);

                if (_searchCancellationTokenSource.Token.IsCancellationRequested)
                    return;

                foreach (var drugResult in results)
                {
                    SearchResults.Add(drugResult);
                }

                UpdateResultCount(results.Count);

                if (results.Count > 0)
                {
                    await SelectDrug(results[0]);
                }
            },
            loading => IsLoading = loading,
            "搜索中...",
            "搜索时发生错误"
        );
    }

    private async Task PerformSearch(bool isNewSearch)
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _searchCancellationTokenSource.Token;

        await ExecuteWithLoadingState(
            async () =>
            {
                if (isNewSearch)
                {
                    SearchResults.Clear();
                    HideDetailPanel();
                    ShowSearchSuggestions = false;
                }

                IsLoadingMore = !isNewSearch;

                // 创建搜索条件
                var searchCriteria = new PaginatedDrugSearchCriteria
                {
                    SearchTerm = _lastSearchTerm,
                    SearchLocalDb = IsLocalDbEnabled,
                    SearchOnline = IsOnlineEnabled,
                    PageIndex = CurrentPage - 1, // 转换为0基索引
                    PageSize = PAGE_SIZE
                };

                var results = await _drugSearchService.SearchDrugsWithPaginationAsync(searchCriteria);

                if (cancellationToken.IsCancellationRequested)
                    return;

                SearchResults.Clear();

                foreach (var drugResult in results.Items)
                {
                    SearchResults.Add(drugResult);
                }

                // 更新分页信息
                UpdatePaginationInfo(results);

                // 如果是新搜索且有结果，选中第一个
                if (isNewSearch && results.Items.Count > 0)
                {
                    await SelectDrug(results.Items[0]);
                }
            },
            loading =>
            {
                if (isNewSearch)
                {
                    IsLoading = loading;
                }
                else
                {
                    IsLoadingMore = loading;
                }
            },
            isNewSearch ? "搜索中..." : "加载中...",
            "搜索时发生错误"
        );
    }

    private void UpdatePaginationInfo(PaginatedSearchResult results)
    {
        TotalPages = results.TotalPages;
        HasPreviousPage = results.HasPreviousPage;
        HasNextPage = results.HasNextPage;

        // 更新结果计数
        var startItem = results.TotalCount > 0 ? (CurrentPage - 1) * PAGE_SIZE + 1 : 0;
        var endItem = Math.Min(CurrentPage * PAGE_SIZE, results.TotalCount);

        ResultCount = $"搜索结果: {results.TotalCount} 条";
        PageInfo = results.TotalCount > 0
            ? $"显示 {startItem}-{endItem} 条，共 {results.TotalCount} 条"
            : string.Empty;
    }

    private async Task DisplayDrugDetails(UnifiedDrugSearchResult? drugResult)
    {
        try
        {
            ShowBasicInfo(drugResult);
            ShowDetailPanel();

            if (drugResult != null)
            {
                var detailInfo = await _drugSearchService.GetDrugDetailsAsync(
                    drugResult.DrugInfo.Id,
                    drugResult.DrugInfo.DataSource);

                if (detailInfo != null)
                {
                    await UpdateDetailInfo(detailInfo);
                }
            }

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

    private async Task GenerateCalculatorWithAiStream(CalculatorGenerationRequest request)
    {
        IsGeneratingCalculator = true;
        GenerationStatus = "正在准备生成计算器...";
        GenerationProgress = 0;
        GenerationStreamContent = string.Empty;
        GenerationReasoningContent = string.Empty;

        try
        {
            DosageCalculatorGenerationResult? finalResult = null;

            await Task.Run(async () =>
            {
                if (SelectedDrugInfo != null)
                    await foreach (var progress in _aiService.GenerateCalculatorStreamAsync(
                                       SelectedDrugInfo,
_aiService.Get_logger(), request.CalculatorType,
                                       request.AdditionalRequirements))
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            GenerationStatus = progress.Message;
                            GenerationProgress = progress.Progress;

                            if (!string.IsNullOrEmpty(progress.StreamChunk))
                            {
                                GenerationStreamContent += progress.StreamChunk;
                            }

                            // 收集思维链内容
                            if (!string.IsNullOrEmpty(progress.ReasoningContent))
                            {
                                GenerationReasoningContent = progress.ReasoningContent;
                            }

                            if (progress.Result != null)
                            {
                                finalResult = progress.Result;
                            }
                        }, System.Windows.Threading.DispatcherPriority.Background);
                    }
            }).ConfigureAwait(false);

            if (finalResult is { Success: true, Calculator: not null })
            {
                await SaveGeneratedCalculator(finalResult.Calculator, GenerationReasoningContent);
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
                GenerationReasoningContent = string.Empty;
            });
        }
    }

    private async Task SaveGeneratedCalculator(DosageCalculator calculator, string reasoning)
    {
        GenerationStatus = "正在保存计算器...";

        // 将思维链内容作为注释添加到代码开头
        if (!string.IsNullOrWhiteSpace(reasoning))
        {
            // 在代码前方添加AI生成声明
            var info = $"// 计算器名称: {calculator.CalculatorName}\n" +
                       $"// 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                       "// 注意：以下所有代码均由Deepseek R1 AI模型生成，不保证准确性。代码后方附有思考链内容。\n";
            calculator.CalculationCode = info + calculator.CalculationCode;
            var reasoningComment = $"/*\n * AI生成思维链:\n * {reasoning.Replace("\n", "\n * ")}\n */\n\n";
            calculator.CalculationCode += reasoningComment;
        }

        var savedCalculator = await _calculatorService.SaveCalculatorAsync(calculator).ConfigureAwait(false);

        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (SelectedDrugInfo != null) await LoadCalculatorsForDrug(SelectedDrugInfo);

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

    private async Task ExecuteWithLoadingState(
        Func<Task> action,
        Action<bool> setLoadingState,
        string loadingMessage,
        string errorMessage)
    {
        setLoadingState(true);
        CalculatorStatusMessage = loadingMessage;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            CalculatorStatusMessage = $"{errorMessage}: {ex.Message}";
            if (errorMessage == "加载计算器失败")
            {
                HasCalculators = false;
                ClearCalculatorData();
            }
        }
        finally
        {
            setLoadingState(false);
        }
    }

    private List<DosageParameter> ValidateRequiredParameters()
    {
        return [.. CalculatorParameters.Where(p => p.IsRequired && string.IsNullOrWhiteSpace(p.Value.ToString()))];
    }

    private static void ShowValidationError(List<DosageParameter> missingParams)
    {
        var missingNames = string.Join(", ", missingParams.Select(p => p.DisplayName));
        MessageBox.Show($"请填写必填参数: {missingNames}", "参数验证",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private async Task ExecuteCalculation()
    {
        IsCalculating = true;
        CalculationResults.Clear();
        HasCalculationResults = false;
        CalculatorStatusMessage = "正在计算...";

        try
        {
            var paramDict = BuildParameterDictionary();
            if (SelectedCalculator != null)
            {
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
            }

            HasCalculationResults = CalculationResults.Count > 0;
            CalculatorStatusMessage = HasCalculationResults
                ? $"计算完成，共 {CalculationResults.Count} 个结果"
                : "计算完成，无结果";
        }
        catch (Exception ex)
        {
            HandleCalculationError(ex);
        }
        finally
        {
            IsCalculating = false;
        }
    }

    private Dictionary<string, object> BuildParameterDictionary()
    {
        var paramDict = new Dictionary<string, object>();
        foreach (var param in CalculatorParameters)
        {
            object value = param.DataType switch
            {
                ParameterTypes.NUMBER => Convert.ToDouble(param.Value),
                ParameterTypes.BOOLEAN => Convert.ToBoolean(param.Value),
                _ => param.Value.ToString() ?? string.Empty
            };
            if (param.Name != null) paramDict[param.Name] = value;
        }

        return paramDict;
    }

    private void HandleCalculationError(Exception ex)
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

    private async Task ShowCalculatorEditor(DosageCalculator? calculator = null)
    {
        try
        {
            var calculatorService = ContainerAccessor.Resolve<JavaScriptDosageCalculatorService>();
            var logger = ContainerAccessor.Resolve<ILogger<CalculatorEditorViewModel>>();

            if (SelectedDrugInfo != null)
            {
                var viewModel = new CalculatorEditorViewModel(
                    calculatorService,
                    logger,
                    SelectedDrugInfo,
                    calculator);

                var window = new CalculatorEditorWindow();
                window.SetViewModel(viewModel);
                window.Owner = Application.Current.MainWindow;

                if (window.ShowDialog() == true)
                {
                    await LoadCalculatorsForDrug(SelectedDrugInfo);
                }
            }
        }
        catch (Exception ex)
        {
            var action = calculator == null ? "创建" : "编辑";
            MessageBox.Show($"{action}计算器时发生错误: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ShowBasicInfo(UnifiedDrugSearchResult? drugResult)
    {
        var drugInfo = drugResult?.DrugInfo;
        if (drugInfo == null) return;

        UpdateBasicInfo(drugInfo);
        if (drugResult != null) UpdateMatchInfo(drugResult);
        UpdateSpecificInfo(drugInfo);
    }

    private void UpdateBasicInfo(BaseDrugInfo drugInfo)
    {
        DrugName = drugInfo.DrugName;
        ManufacturerInfo = drugInfo.Manufacturer ?? string.Empty;
        ApprovalNumber = drugInfo.ApprovalNumber ?? string.Empty;
        Specification = drugInfo.Specification ?? string.Empty;
        DataSourceInfo = drugInfo.GetDataSourceText();
    }

    private void UpdateMatchInfo(UnifiedDrugSearchResult drugResult)
    {
        var matchScore = (drugResult.MatchScore * 100).ToString("F1");
        var matchedFields = string.Join(", ", drugResult.MatchedFields);
        MatchInfo = $"匹配度: {matchScore}% | 匹配字段: {matchedFields}";
    }

    private void UpdateSpecificInfo(BaseDrugInfo drugInfo)
    {
        switch (drugInfo)
        {
            case LocalDrugInfo localDrug:
                GenericName = localDrug.GenericName ?? string.Empty;
                TradeName = string.Empty;
                TcmDisease = localDrug.TcmDisease ?? string.Empty;
                TcmSyndrome = localDrug.TcmSyndrome ?? string.Empty;
                break;
            case OnlineDrugInfo onlineDrug:
                GenericName = string.Empty;
                TradeName = onlineDrug.TradeName ?? string.Empty;
                TcmDisease = string.Empty;
                TcmSyndrome = string.Empty;
                break;
            default:
                ClearSpecificInfo();
                break;
        }
    }

    private void ClearSpecificInfo()
    {
        GenericName = string.Empty;
        TradeName = string.Empty;
        TcmDisease = string.Empty;
        TcmSyndrome = string.Empty;
    }

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

    private async Task UpdateDetailInfo(BaseDrugInfo drugInfo)
    {
        _cachedMarkdownContents = DrugInfoMarkdownHelper.ConvertToMarkdownDictionary(drugInfo);

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            TabItems.Clear();
            foreach (var content in _cachedMarkdownContents.Where(content => !string.IsNullOrWhiteSpace(content.Value)))
            {
                var (key, header, isSpecial) = (_tabDefinitions ?? []).FirstOrDefault(t => t.Key == content.Key);

                var tabItem = new TabItemViewModel(header, key, isSpecial)
                {
                    Content = content.Value,
                    IsVisible = true,
                    IsLoaded = true
                };
                TabItems.Add(tabItem);
            }

            var firstVisibleTab = TabItems.FirstOrDefault(t => t.IsVisible);
            if (firstVisibleTab != null)
            {
                SelectedTab = firstVisibleTab;
            }
        });
    }

    partial void OnSearchTermChanged(string value) =>
        Task.Delay(300).ContinueWith(async _ =>
        {
            if (SearchTerm == value && !string.IsNullOrWhiteSpace(value) && value.Length >= 2)
            {
                await GetSearchSuggestions(value);
            }
        });

    private async Task GetSearchSuggestions(string keyword)
    {
        try
        {
            var suggestions = await _drugSearchService.GetSearchSuggestionsAsync(keyword);

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

    private void UpdateResultCount(int count) => ResultCount = $"搜索结果: {count} 条";
    private void ShowDetailPanel() => IsDetailPanelVisible = true;

    private void HideDetailPanel()
    {
        IsDetailPanelVisible = false;
        _cachedMarkdownContents.Clear();

        foreach (var tabItem in TabItems)
        {
            tabItem.IsVisible = false;
            tabItem.IsLoaded = false;
            tabItem.Content = string.Empty;
        }

        ResetCalculatorState();
    }

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

    public class CalculatorGenerationRequest
    {
        public string CalculatorType { get; set; } = "通用剂量计算器";

        public string AdditionalRequirements { get; set; } = string.Empty;
    }
}