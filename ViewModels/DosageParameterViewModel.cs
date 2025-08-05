using CommunityToolkit.Mvvm.ComponentModel;
using DrugSearcher.Models;

namespace DrugSearcher.ViewModels;

public partial class DosageParameterViewModel : ObservableObject
{
    public DosageParameterViewModel()
    {
        Options = [];
        OptionsText = string.Empty;
        DataType = ParameterTypes.NUMBER;
    }
    /// <summary>
    /// 转换为模型对象
    /// </summary>
    public DosageParameter ToModel() => new()
    {
        Name = Name,
        DisplayName = DisplayName,
        Description = Description,
        DataType = DataType,
        Unit = Unit,
        IsRequired = IsRequired,
        DefaultValue = DefaultValue,
        MinValue = MinValue,
        MaxValue = MaxValue,
        Options = [.. Options]
    };

    public DosageParameterViewModel(DosageParameter parameter)
    {
        Name = parameter.Name;
        DisplayName = parameter.DisplayName;
        DataType = parameter.DataType;
        Unit = parameter.Unit;
        IsRequired = parameter.IsRequired;
        DefaultValue = parameter.DefaultValue;
        MinValue = parameter.MinValue;
        MaxValue = parameter.MaxValue;
        Options = [.. parameter.Options];
        Description = parameter.Description ?? string.Empty;

        // 初始化选项文本
        UpdateOptionsText();
    }

    [ObservableProperty]
    public partial string? Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DataType { get; set; } = ParameterTypes.NUMBER;

    [ObservableProperty]
    public partial string Unit { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsRequired { get; set; }

    [ObservableProperty]
    public partial object? DefaultValue { get; set; }

    [ObservableProperty]
    public partial double? MinValue { get; set; }

    [ObservableProperty]
    public partial double? MaxValue { get; set; }

    [ObservableProperty]
    public partial string[] Options { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OptionsText { get; set; } = string.Empty;

    // 用于DataGrid显示的属性
    public string DataTypeName
    {
        get => DataType;
        set => DataType = value;
    }

    public string DefaultValueString
    {
        get => DefaultValue?.ToString() ?? string.Empty;
        set
        {
            switch (DataType)
            {
                case ParameterTypes.NUMBER:
                    {
                        if (double.TryParse(value, out var numValue))
                        {
                            DefaultValue = numValue;
                        }
                        else
                        {
                            DefaultValue = 0;
                        }

                        break;
                    }
                case ParameterTypes.BOOLEAN:
                    {
                        if (bool.TryParse(value, out var boolValue))
                        {
                            DefaultValue = boolValue;
                        }
                        else
                        {
                            DefaultValue = false;
                        }

                        break;
                    }
                default:
                    DefaultValue = value;
                    break;
            }
        }
    }

    public string MinValueString
    {
        get => MinValue?.ToString() ?? string.Empty;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                MinValue = null;
            }
            else if (double.TryParse(value, out var numValue))
            {
                MinValue = numValue;
            }
        }
    }

    public string MaxValueString
    {
        get => MaxValue?.ToString() ?? string.Empty;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                MaxValue = null;
            }
            else if (double.TryParse(value, out var numValue))
            {
                MaxValue = numValue;
            }
        }
    }

    partial void OnOptionsTextChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            Options = value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => s.Trim())
                          .ToArray();
        }
        else
        {
            Options = [];
        }
    }

    partial void OnOptionsChanged(string[] value) => UpdateOptionsText();

    partial void OnDataTypeChanged(string value)
    {
        OnPropertyChanged(nameof(DataTypeName));
        OnPropertyChanged(nameof(IsNumberType));
        OnPropertyChanged(nameof(IsSelectType));
        OnPropertyChanged(nameof(IsBooleanType));
        OnPropertyChanged(nameof(IsTextType));

        // 根据数据类型设置默认值
        switch (value)
        {
            case ParameterTypes.NUMBER:
                if (DefaultValue == null || !IsNumeric(DefaultValue))
                    DefaultValue = 0;
                break;
            case ParameterTypes.BOOLEAN:
                if (DefaultValue == null || DefaultValue is not bool)
                    DefaultValue = false;
                break;
            case ParameterTypes.SELECT:
                if (DefaultValue == null || DefaultValue is not string)
                    DefaultValue = "";
                break;
            case ParameterTypes.TEXT:
                if (DefaultValue == null || DefaultValue is not string)
                    DefaultValue = "";
                break;
        }

        OnPropertyChanged(nameof(DefaultValueString));
    }

    private void UpdateOptionsText() => OptionsText = string.Join(", ", Options);

    private static bool IsNumeric(object value) => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    public bool IsNumberType => DataType == ParameterTypes.NUMBER;
    public bool IsSelectType => DataType == ParameterTypes.SELECT;
    public bool IsBooleanType => DataType == ParameterTypes.BOOLEAN;
    public bool IsTextType => DataType == ParameterTypes.TEXT;

    // 为DataGrid提供数据类型选项
    public static List<string> DataTypeOptions => ParameterTypes.All;
}