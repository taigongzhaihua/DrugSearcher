using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Models;
using DrugSearcher.Services;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.ViewModels;

public partial class CalculatorEditorViewModel : ObservableObject
{
    private readonly JavaScriptDosageCalculatorService _calculatorService;
    private readonly ILogger<CalculatorEditorViewModel> _logger;
    private readonly BaseDrugInfo _drugInfo;
    private readonly DosageCalculator? _editingCalculator;
    private string _originalCode = string.Empty;
    private string _originalCalculatorName = string.Empty;
    private string _originalDescription = string.Empty;
    private string _originalParametersJson = string.Empty;

    public CalculatorEditorViewModel(
        JavaScriptDosageCalculatorService calculatorService,
        ILogger<CalculatorEditorViewModel> logger,
        BaseDrugInfo drugInfo,
        DosageCalculator? editingCalculator = null)
    {
        _calculatorService = calculatorService;
        _logger = logger;
        _drugInfo = drugInfo;
        _editingCalculator = editingCalculator;
        IsEditing = editingCalculator != null;

        Parameters = [];
        CodeDocument = new TextDocument();

        InitializeCalculator();
    }

    #region Properties

    [ObservableProperty]
    public partial string CalculatorName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CalculationCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial TextDocument CodeDocument { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial bool IsTesting { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DosageParameterViewModel? SelectedParameter { get; set; }

    public ObservableCollection<DosageParameterViewModel> Parameters { get; }

    public string WindowTitle => IsEditing ? $"编辑计算器 - {_drugInfo.DrugName}" : $"创建计算器 - {_drugInfo.DrugName}";

    public string SaveButtonText => IsEditing ? "保存修改" : "创建计算器";

    public bool IsEditing { get; }

    /// <summary>
    /// 获取是否有未保存的更改
    /// </summary>
    public bool HasUnsavedChanges
    {
        get
        {
            // 检查基本信息是否更改
            if (CalculatorName != _originalCalculatorName ||
                Description != _originalDescription ||
                CodeDocument.Text != _originalCode)
            {
                return true;
            }

            // 检查参数是否更改
            var currentParametersJson = GetCurrentParametersJson();
            if (currentParametersJson != _originalParametersJson)
            {
                return true;
            }

            return false;
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void AddParameter()
    {
        var parameter = new DosageParameterViewModel
        {
            Name = GenerateParameterName(),
            DisplayName = "新参数",
            DataType = ParameterTypes.NUMBER,
            Unit = "",
            IsRequired = false,
            DefaultValue = 0,
            Description = "请输入参数描述"
        };

        Parameters.Add(parameter);
        SelectedParameter = parameter;
        StatusMessage = "已添加新参数";
    }

    [RelayCommand]
    private void RemoveParameter()
    {
        if (SelectedParameter != null)
        {
            Parameters.Remove(SelectedParameter);
            SelectedParameter = Parameters.FirstOrDefault();
            StatusMessage = "已删除参数";
        }
    }

    [RelayCommand]
    private void DuplicateParameter()
    {
        if (SelectedParameter != null)
        {
            var newParam = new DosageParameterViewModel
            {
                Name = GenerateParameterName(),
                DisplayName = SelectedParameter.DisplayName + " (副本)",
                DataType = SelectedParameter.DataType,
                Unit = SelectedParameter.Unit,
                IsRequired = SelectedParameter.IsRequired,
                DefaultValue = SelectedParameter.DefaultValue,
                MinValue = SelectedParameter.MinValue,
                MaxValue = SelectedParameter.MaxValue,
                Options = [.. SelectedParameter.Options],
                Description = SelectedParameter.Description
            };

            var index = Parameters.IndexOf(SelectedParameter);
            Parameters.Insert(index + 1, newParam);
            SelectedParameter = newParam;
            StatusMessage = "已复制参数";
        }
    }

    [RelayCommand]
    private void MoveParameterUp()
    {
        if (SelectedParameter != null)
        {
            var index = Parameters.IndexOf(SelectedParameter);
            if (index > 0)
            {
                Parameters.Move(index, index - 1);
                StatusMessage = "参数已上移";
            }
        }
    }

    [RelayCommand]
    private void MoveParameterDown()
    {
        if (SelectedParameter != null)
        {
            var index = Parameters.IndexOf(SelectedParameter);
            if (index < Parameters.Count - 1)
            {
                Parameters.Move(index, index + 1);
                StatusMessage = "参数已下移";
            }
        }
    }

    [RelayCommand]
    private async Task TestCalculator()
    {
        if (!ValidateCalculator())
        {
            return;
        }

        IsTesting = true;
        StatusMessage = "正在测试计算器...";

        try
        {
            // 从代码编辑器获取最新代码
            CalculationCode = CodeDocument.Text;

            // 创建测试参数
            var testParams = new Dictionary<string, object>();
            foreach (var param in Parameters)
            {
                var testValue = GetTestValue(param);
                if (param.Name != null) testParams[param.Name] = testValue;
            }

            // 执行测试
            var request = new DosageCalculationRequest
            {
                CalculationCode = CalculationCode,
                Parameters = testParams
            };

            var results = await _calculatorService.TestCalculationAsync(request);

            if (results.Count != 0)
            {
                var resultLines = results.Select(r =>
                {
                    var line = $"• {r.Description}: {r.Dose} {r.Unit}";
                    if (!string.IsNullOrEmpty(r.Frequency))
                        line += $", {r.Frequency}";
                    if (!string.IsNullOrEmpty(r.Duration))
                        line += $", {r.Duration}";
                    if (!string.IsNullOrEmpty(r.Notes))
                        line += $" ({r.Notes})";
                    if (r.IsWarning)
                        line += $" [警告: {r.WarningMessage}]";
                    return line;
                });

                StatusMessage = $"✓ 测试成功！结果：\n{string.Join("\n", resultLines)}";
            }
            else
            {
                StatusMessage = "⚠ 测试完成，但无结果输出。请检查代码是否调用了addResult相关函数。";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ 测试失败: {ex.Message}";
            _logger.LogError(ex, "计算器测试失败");
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task SaveCalculator()
    {
        if (!ValidateCalculator())
        {
            return;
        }

        IsSaving = true;
        StatusMessage = "正在保存计算器...";

        try
        {
            // 从代码编辑器获取最新代码
            CalculationCode = CodeDocument.Text;

            var calculator = CreateCalculatorFromViewModel();

            if (IsEditing)
            {
                calculator.Id = _editingCalculator!.Id;
                calculator.CreatedAt = _editingCalculator.CreatedAt;
                calculator.CreatedBy = _editingCalculator.CreatedBy;
                calculator.UpdatedAt = DateTime.Now;

                await _calculatorService.UpdateCalculatorAsync(calculator);
                StatusMessage = "✓ 计算器已成功更新！";
            }
            else
            {
                await _calculatorService.SaveCalculatorAsync(calculator);
                StatusMessage = "✓ 计算器已成功创建！";
            }

            // 更新原始值
            _originalCalculatorName = CalculatorName;
            _originalDescription = Description;
            _originalCode = CodeDocument.Text;
            _originalParametersJson = GetCurrentParametersJson();

            // 触发属性变化通知
            OnPropertyChanged(nameof(HasUnsavedChanges));

            // 通知保存成功
            OnCalculatorSaved?.Invoke(calculator);
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ 保存失败: {ex.Message}";
            _logger.LogError(ex, "保存计算器失败");
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private void InsertCodeTemplate()
    {
        var template = GetCodeTemplate();
        var insertPosition = CodeDocument.TextLength;

        if (CodeDocument.TextLength > 0)
        {
            CodeDocument.Insert(insertPosition, "\n\n" + template);
        }
        else
        {
            CodeDocument.Insert(insertPosition, template);
        }

        StatusMessage = "代码模板已插入";
    }

    [RelayCommand]
    private void InsertParameterCode()
    {
        if (SelectedParameter != null)
        {
            var paramCode = GenerateParameterCode(SelectedParameter);
            var insertPosition = CodeDocument.TextLength;

            if (CodeDocument.TextLength > 0)
            {
                CodeDocument.Insert(insertPosition, "\n" + paramCode);
            }
            else
            {
                CodeDocument.Insert(insertPosition, paramCode);
            }

            StatusMessage = $"已插入参数 {SelectedParameter.DisplayName} 的代码";
        }
    }

    [RelayCommand]
    private void ValidateCode()
    {
        try
        {
            var code = CodeDocument.Text;
            var isValid = _calculatorService.ValidateJavaScript(code);
            StatusMessage = isValid ? "✓ 代码语法验证通过！" : "✗ 代码语法验证失败，请检查语法";
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ 代码验证失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearCode()
    {
        if (MessageBox.Show("确定要清空所有代码吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            CodeDocument.Text = "";
            StatusMessage = "代码已清空";
        }
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        if (MessageBox.Show("确定要重置为默认代码吗？这将覆盖现有代码。", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            CodeDocument.Text = GetDefaultCalculationCode();
            StatusMessage = "代码已重置为默认模板";
        }
    }

    #endregion

    #region Events

    public event Action<DosageCalculator>? OnCalculatorSaved;

    #endregion

    #region Private Methods


    private void InitializeCalculator()
    {
        if (IsEditing && _editingCalculator != null)
        {
            // 编辑模式：加载现有数据
            CalculatorName = _editingCalculator.CalculatorName;
            Description = _editingCalculator.Description ?? string.Empty;
            CalculationCode = _editingCalculator.CalculationCode;
            CodeDocument.Text = _editingCalculator.CalculationCode;

            // 保存原始值用于比较
            _originalCalculatorName = CalculatorName;
            _originalDescription = Description;
            _originalCode = CalculationCode;

            // 加载参数
            if (!string.IsNullOrEmpty(_editingCalculator.ParameterDefinitions))
            {
                try
                {
                    var parameters = JsonSerializer.Deserialize<List<DosageParameter>>(_editingCalculator.ParameterDefinitions);
                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            Parameters.Add(new DosageParameterViewModel(param));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "加载参数定义失败");
                }
            }

            _originalParametersJson = GetCurrentParametersJson();
            StatusMessage = "已加载现有计算器数据";
        }
        else
        {
            // 创建模式：设置默认值
            CalculatorName = $"{_drugInfo.DrugName}剂量计算器";
            Description = $"用于计算{_drugInfo.DrugName}的推荐剂量";
            CalculationCode = GetDefaultCalculationCode();
            CodeDocument.Text = CalculationCode;

            // 保存原始值
            _originalCalculatorName = CalculatorName;
            _originalDescription = Description;
            _originalCode = CalculationCode;

            // 添加默认参数
            AddDefaultParameters();
            _originalParametersJson = GetCurrentParametersJson();

            StatusMessage = "已初始化新计算器";
        }

        // 监听属性变化以更新 HasUnsavedChanges
        PropertyChanged += OnPropertyChangedForUnsavedChanges;
        CodeDocument.TextChanged += OnCodeDocumentTextChanged;
        Parameters.CollectionChanged += OnParametersCollectionChanged;
    }

    private void OnPropertyChangedForUnsavedChanges(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalculatorName) or nameof(Description))
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }
    }

    private void OnCodeDocumentTextChanged(object? sender, EventArgs e) => OnPropertyChanged(nameof(HasUnsavedChanges));

    private void OnParametersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));

        // 订阅新参数的属性变化
        if (e.NewItems != null)
        {
            foreach (DosageParameterViewModel param in e.NewItems)
            {
                param.PropertyChanged += OnParameterPropertyChanged;
            }
        }

        // 取消订阅移除的参数
        if (e.OldItems != null)
        {
            foreach (DosageParameterViewModel param in e.OldItems)
            {
                param.PropertyChanged -= OnParameterPropertyChanged;
            }
        }
    }

    private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(HasUnsavedChanges));

    private string GetCurrentParametersJson()
    {
        var parameters = Parameters.Select(p => new DosageParameter
        {
            Name = p.Name,
            DisplayName = p.DisplayName,
            DataType = p.DataType,
            Unit = p.Unit,
            IsRequired = p.IsRequired,
            DefaultValue = p.DefaultValue,
            MinValue = p.MinValue,
            MaxValue = p.MaxValue,
            Options = [.. p.Options],
            Description = p.Description
        }).ToList();

        return JsonSerializer.Serialize(parameters);
    }

    private void AddDefaultParameters()
    {
        Parameters.Add(new DosageParameterViewModel
        {
            Name = "weight",
            DisplayName = "体重",
            DataType = ParameterTypes.NUMBER,
            Unit = "kg",
            IsRequired = true,
            DefaultValue = 70,
            MinValue = 1,
            MaxValue = 200,
            Description = "患者体重（千克）"
        });

        Parameters.Add(new DosageParameterViewModel
        {
            Name = "age",
            DisplayName = "年龄",
            DataType = ParameterTypes.NUMBER,
            Unit = "岁",
            IsRequired = true,
            DefaultValue = 35,
            MinValue = 0,
            MaxValue = 120,
            Description = "患者年龄（岁）"
        });

        Parameters.Add(new DosageParameterViewModel
        {
            Name = "severity",
            DisplayName = "病情严重程度",
            DataType = ParameterTypes.SELECT,
            IsRequired = true,
            DefaultValue = "中度",
            OptionsText = "轻度, 中度, 重度",
            Description = "根据病情严重程度选择"
        });
    }

    private string GenerateParameterName()
    {
        var baseName = "param";
        var counter = 1;

        while (Parameters.Any(p => p.Name == $"{baseName}{counter}"))
        {
            counter++;
        }

        return $"{baseName}{counter}";
    }

    private static string GenerateParameterCode(DosageParameterViewModel parameter) => parameter.DataType switch
    {
        ParameterTypes.NUMBER => $"// 获取{parameter.DisplayName}\n{parameter.Name} = parseFloat({parameter.Name}) || {parameter.DefaultValue ?? 0};\n",
        ParameterTypes.BOOLEAN => $"// 获取{parameter.DisplayName}\n{parameter.Name} = Boolean({parameter.Name});\n",
        ParameterTypes.SELECT => $"// 获取{parameter.DisplayName}\n{parameter.Name} = {parameter.Name} || '{parameter.DefaultValue ?? ""}';\n",
        _ => $"// 获取{parameter.DisplayName}\n{parameter.Name} = {parameter.Name} || '{parameter.DefaultValue ?? ""}';\n"
    };

    private static string GetDefaultCalculationCode() => """
                                                         // 获取并验证参数
                                                         weight = parseFloat(weight) || 0;
                                                         age = parseFloat(age) || 0;
                                                         severity = severity || '中度';

                                                         // 输入验证
                                                         if (weight <= 0 || weight > 200) {
                                                             addWarning('体重范围', 0, 'mg', '', '体重应在1-200kg之间');
                                                             return;
                                                         }

                                                         if (age < 0 || age > 120) {
                                                             addWarning('年龄范围', 0, 'mg', '', '年龄应在0-120岁之间');
                                                             return;
                                                         }

                                                         // 基础剂量计算
                                                         var baseDose = weight * 10; // 示例：10mg/kg

                                                         // 严重程度调整
                                                         if (severity === '重度') {
                                                             baseDose *= 1.5;
                                                         } else if (severity === '轻度') {
                                                             baseDose *= 0.8;
                                                         }

                                                         // 年龄调整
                                                         if (age > 65) {
                                                             baseDose *= 0.9;
                                                         } else if (age < 18) {
                                                             baseDose *= 1.1;
                                                         }

                                                         // 计算单次剂量
                                                         var singleDose = round(baseDose / 3, 1);

                                                         // 输出结果
                                                         addNormalResult('推荐剂量', singleDose, 'mg', '每日3次', '7-14天', '餐后服用');
                                                         """;

    private static string GetCodeTemplate() => """
                                               // 代码模板
                                               // 1. 参数获取和验证
                                               weight = parseFloat(weight) || 0;
                                               age = parseFloat(age) || 0;

                                               // 2. 输入验证
                                               if (weight <= 0) {
                                                   addWarning('参数错误', 0, 'mg', '', '请输入有效的体重');
                                                   return;
                                               }

                                               // 3. 剂量计算
                                               var dose = weight * 10; // 根据实际药物调整

                                               // 4. 特殊情况调整
                                               if (age > 65) {
                                                   dose *= 0.8; // 老年人减量
                                               }

                                               // 5. 输出结果
                                               addNormalResult('推荐剂量', round(dose, 1), 'mg', '每日3次', '7-14天', '餐后服用');
                                               """;

    private bool ValidateCalculator()
    {
        if (string.IsNullOrWhiteSpace(CalculatorName))
        {
            StatusMessage = "✗ 请输入计算器名称";
            return false;
        }

        var currentCode = CodeDocument.Text;
        if (string.IsNullOrWhiteSpace(currentCode))
        {
            StatusMessage = "✗ 请输入计算代码";
            return false;
        }

        if (!Parameters.Any())
        {
            StatusMessage = "✗ 请至少添加一个参数";
            return false;
        }

        // 验证参数
        foreach (var param in Parameters)
        {
            if (string.IsNullOrWhiteSpace(param.Name))
            {
                StatusMessage = $"✗ 参数名称不能为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(param.DisplayName))
            {
                StatusMessage = $"✗ 参数 '{param.Name}' 的显示名称不能为空";
                return false;
            }

            // 检查参数名称是否重复
            if (Parameters.Count(p => p.Name == param.Name) > 1)
            {
                StatusMessage = $"✗ 参数名称 '{param.Name}' 重复";
                return false;
            }
        }

        // 验证代码
        try
        {
            var isValid = _calculatorService.ValidateJavaScript(currentCode);
            if (!isValid)
            {
                StatusMessage = "✗ 代码语法验证失败，请检查语法";
                return false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ 代码验证失败: {ex.Message}";
            return false;
        }

        return true;
    }

    private DosageCalculator CreateCalculatorFromViewModel()
    {
        var parameters = Parameters.Select(p => new DosageParameter
        {
            Name = p.Name,
            DisplayName = p.DisplayName,
            DataType = p.DataType,
            Unit = p.Unit,
            IsRequired = p.IsRequired,
            DefaultValue = p.DefaultValue,
            MinValue = p.MinValue,
            MaxValue = p.MaxValue,
            Options = [.. p.Options],
            Description = p.Description
        }).ToList();

        return new DosageCalculator
        {
            DrugIdentifier = DosageCalculator.GenerateDrugIdentifier(_drugInfo.DataSource, _drugInfo.Id),
            DataSource = _drugInfo.DataSource,
            OriginalDrugId = _drugInfo.Id,
            DrugName = _drugInfo.DrugName,
            CalculatorName = CalculatorName,
            Description = Description,
            CalculationCode = CodeDocument.Text,
            ParameterDefinitions = JsonSerializer.Serialize(parameters),
            CreatedBy = "taigongzhaihua",
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            IsActive = true
        };
    }

    private static object GetTestValue(DosageParameterViewModel param) => param.DataType switch
    {
        ParameterTypes.NUMBER => param.DefaultValue ?? 0,
        ParameterTypes.BOOLEAN => param.DefaultValue ?? false,
        ParameterTypes.SELECT => param.DefaultValue ?? param.Options?.FirstOrDefault() ?? "",
        _ => param.DefaultValue ?? ""
    };

    #endregion

    #region Cleanup

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Cleanup()
    {
        // 取消事件订阅
        PropertyChanged -= OnPropertyChangedForUnsavedChanges;
        CodeDocument.TextChanged -= OnCodeDocumentTextChanged;
        Parameters.CollectionChanged -= OnParametersCollectionChanged;

        foreach (var param in Parameters)
        {
            param.PropertyChanged -= OnParameterPropertyChanged;
        }
    }

    #endregion
}