using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Configuration;
using DrugSearcher.Helpers;
using DrugSearcher.Models;
using DrugSearcher.Services;
using DrugSearcher.Views;
using DrugSearcher.Views.Dialogs;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
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


    public HomePageViewModel(DrugSearchService drugSearchService,
        JavaScriptDosageCalculatorService calculatorService,
        DosageCalculatorAiService aiService)
    {
        _drugSearchService = drugSearchService;
        _calculatorService = calculatorService;
        SearchResults = [];
        SearchSuggestions = [];
        MarkdownContents = DrugInfoMarkdownHelper.ConvertToMarkdownDictionary(new LocalDrugInfo());

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
    }

    #region 原有属性 (搜索相关)

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
    // Markdown 内容字典
    [ObservableProperty]
    private Dictionary<string, string> _markdownContents;

    public ObservableCollection<UnifiedDrugSearchResult?> SearchResults { get; }
    public ObservableCollection<string> SearchSuggestions { get; }

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
    private void HideSuggestions()
    {
        ShowSearchSuggestions = false;
    }

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
                paramDict[param.Name] = value;
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

        var result = await ShowCalculatorGenerationDialog();
        if (result == null) return;

        IsGeneratingCalculator = true;
        GenerationStatus = "正在生成计算器...";

        try
        {
            var generationResult = await _aiService.GenerateCalculatorAsync(
                SelectedDrugInfo,
                result.CalculatorType,
                result.AdditionalRequirements);
            Debug.WriteLine(generationResult.Calculator?.CalculationCode);
            if (generationResult is { Success: true, Calculator: not null })
            {

                // 验证并保存生成的计算器
                var savedCalculator = await _calculatorService.SaveCalculatorAsync(generationResult.Calculator);

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
            }
            else
            {
                GenerationStatus = "计算器生成失败";
                MessageBox.Show($"生成计算器失败: {generationResult.ErrorMessage}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            GenerationStatus = "计算器生成失败";
            MessageBox.Show($"生成计算器时发生错误: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsGeneratingCalculator = false;
        }
    }

    #endregion

    #region 私有方法 (搜索相关)

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
                    UpdateDetailInfo(detailInfo);
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
    private async Task<CalculatorGenerationRequest?> ShowCalculatorGenerationDialog()
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