using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Models;
using DrugSearcher.Services;
using System.Collections.ObjectModel;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.ViewModels;

/// <summary>
/// 剂量计算器ViewModel
/// 分离关注点：专门处理计算器的UI逻辑和状态管理
/// </summary>
public partial class DosageCalculatorViewModel(JavaScriptDosageCalculatorService calculatorService) : ObservableObject
{
    #region 属性

    [ObservableProperty]
    public partial BaseDrugInfo? CurrentDrugInfo { get; set; }

    [ObservableProperty]
    public partial DosageCalculator? SelectedCalculator { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsCalculating { get; set; }

    [ObservableProperty]
    public partial bool HasResults { get; set; }

    [ObservableProperty]
    public partial bool HasCalculators { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "请选择药物以查看可用的计算器";

    public ObservableCollection<DosageCalculator> AvailableCalculators { get; } = [];

    public ObservableCollection<DosageParameter> Parameters { get; } = [];

    public ObservableCollection<DosageCalculationResult> CalculationResults { get; } = [];

    #endregion

    #region 命令

    [RelayCommand]
    private async Task LoadCalculatorsForDrug(BaseDrugInfo drugInfo)
    {
        try
        {
            IsLoading = true;
            CurrentDrugInfo = drugInfo;
            StatusMessage = "正在加载计算器...";

            var calculators = await calculatorService.GetCalculatorsForDrugAsync(drugInfo);

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
                StatusMessage = $"找到 {AvailableCalculators.Count} 个计算器";
            }
            else
            {
                StatusMessage = "该药物暂无可用的计算器";
                ClearCalculatorData();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载计算器失败: {ex.Message}";
            HasCalculators = false;
            ClearCalculatorData();
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadCalculatorParameters()
    {
        if (SelectedCalculator == null) return;

        try
        {
            var parameters = await calculatorService.GetCalculatorParametersAsync(SelectedCalculator.Id);

            Parameters.Clear();
            foreach (var param in parameters)
            {
                // 设置默认值
                if (param.DefaultValue is not null)
                {
                    param.Value = param.DefaultValue;
                }
                else
                {
                    param.Value = param.DataType switch
                    {
                        ParameterTypes.BOOLEAN => false,
                        ParameterTypes.NUMBER => 0.0,
                        _ => string.Empty
                    };
                }
                Parameters.Add(param);
            }

            // 清空之前的计算结果
            CalculationResults.Clear();
            HasResults = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载参数失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Calculate()
    {
        if (SelectedCalculator == null) return;

        // 验证必填参数
        var missingParams = Parameters.Where(p => p.IsRequired &&
            (p.Value == null || string.IsNullOrWhiteSpace(p.Value.ToString()))).ToList();

        if (missingParams.Count != 0)
        {
            var missingNames = string.Join(", ", missingParams.Select(p => p.DisplayName));
            MessageBox.Show($"请填写必填参数: {missingNames}", "参数验证",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsCalculating = true;
        CalculationResults.Clear();
        HasResults = false;
        StatusMessage = "正在计算...";

        try
        {
            // 构建参数字典
            var paramDict = new Dictionary<string, object>();
            foreach (var param in Parameters)
            {
                if (param.Value != null)
                {
                    // 根据参数类型转换值
                    object value = param.DataType switch
                    {
                        ParameterTypes.NUMBER => Convert.ToDouble(param.Value),
                        ParameterTypes.BOOLEAN => Convert.ToBoolean(param.Value),
                        _ => param.Value.ToString() ?? string.Empty
                    };
                    if (param.Name != null) paramDict[param.Name] = value;
                }
            }

            var request = new DosageCalculationRequest
            {
                CalculatorId = SelectedCalculator.Id,
                Parameters = paramDict
            };

            var results = await calculatorService.CalculateDosageAsync(request);

            foreach (var result in results)
            {
                CalculationResults.Add(result);
            }

            HasResults = CalculationResults.Count > 0;
            StatusMessage = HasResults ? $"计算完成，共 {CalculationResults.Count} 个结果" : "计算完成，无结果";
        }
        catch (Exception ex)
        {
            CalculationResults.Add(new DosageCalculationResult
            {
                Description = "计算错误",
                IsWarning = true,
                WarningMessage = ex.Message
            });
            HasResults = true;
            StatusMessage = "计算失败";
        }
        finally
        {
            IsCalculating = false;
        }
    }

    [RelayCommand]
    private async Task CreateCalculator()
    {
        if (CurrentDrugInfo == null) return;

        // 这里可以打开计算器创建/编辑窗口
        // 暂时显示消息
        MessageBox.Show("计算器创建功能正在开发中...", "功能提示",
            MessageBoxButton.OK, MessageBoxImage.Information);

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task EditCalculator()
    {
        if (SelectedCalculator == null) return;

        // 这里可以打开计算器编辑窗口
        // 暂时显示消息
        MessageBox.Show("计算器编辑功能正在开发中...", "功能提示",
            MessageBoxButton.OK, MessageBoxImage.Information);

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ClearResults()
    {
        CalculationResults.Clear();
        HasResults = false;
        StatusMessage = "结果已清空";
    }

    #endregion

    #region 私有方法

    private void ClearCalculatorData()
    {
        SelectedCalculator = null;
        Parameters.Clear();
        CalculationResults.Clear();
        HasResults = false;
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 外部调用的加载方法
    /// </summary>
    public async Task LoadCalculatorsForDrugAsync(BaseDrugInfo drugInfo) => await LoadCalculatorsForDrug(drugInfo);

    /// <summary>
    /// 重置ViewModel状态
    /// </summary>
    public void Reset()
    {
        CurrentDrugInfo = null;
        AvailableCalculators.Clear();
        HasCalculators = false;
        ClearCalculatorData();
        StatusMessage = "请选择药物以查看可用的计算器";
    }

    #endregion
}