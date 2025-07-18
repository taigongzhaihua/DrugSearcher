using CommunityToolkit.Mvvm.ComponentModel;
using DrugSearcher.Models;

namespace DrugSearcher.ViewModels;

public partial class DosageParameterViewModel : ObservableObject
{
    public DosageParameterViewModel()
    {
        Options = [];
        OptionsText = string.Empty;
        DataType = ParameterTypes.Number;
    }
    /// <summary>
    /// 转换为模型对象
    /// </summary>
    public DosageParameter ToModel()
    {
        return new DosageParameter
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
            Options = Options.ToList()
        };
    }

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
        Options = parameter.Options.ToArray();
        Description = parameter.Description ?? string.Empty;

        // 初始化选项文本
        UpdateOptionsText();
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _dataType = ParameterTypes.Number;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private bool _isRequired;

    [ObservableProperty]
    private object? _defaultValue;

    [ObservableProperty]
    private double? _minValue;

    [ObservableProperty]
    private double? _maxValue;

    [ObservableProperty]
    private string[] _options;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _optionsText = string.Empty;

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
            if (DataType == ParameterTypes.Number)
            {
                if (double.TryParse(value, out var numValue))
                {
                    DefaultValue = numValue;
                }
                else
                {
                    DefaultValue = 0;
                }
            }
            else if (DataType == ParameterTypes.Boolean)
            {
                if (bool.TryParse(value, out var boolValue))
                {
                    DefaultValue = boolValue;
                }
                else
                {
                    DefaultValue = false;
                }
            }
            else
            {
                DefaultValue = value;
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

    partial void OnOptionsChanged(string[] value)
    {
        UpdateOptionsText();
    }

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
            case ParameterTypes.Number:
                if (DefaultValue == null || !IsNumeric(DefaultValue))
                    DefaultValue = 0;
                break;
            case ParameterTypes.Boolean:
                if (DefaultValue == null || DefaultValue is not bool)
                    DefaultValue = false;
                break;
            case ParameterTypes.Select:
                if (DefaultValue == null || DefaultValue is not string)
                    DefaultValue = "";
                break;
            case ParameterTypes.Text:
                if (DefaultValue == null || DefaultValue is not string)
                    DefaultValue = "";
                break;
        }

        OnPropertyChanged(nameof(DefaultValueString));
    }

    private void UpdateOptionsText()
    {
        OptionsText = string.Join(", ", Options);
    }

    private static bool IsNumeric(object value)
    {
        return value is byte || value is sbyte || value is short || value is ushort ||
               value is int || value is uint || value is long || value is ulong ||
               value is float || value is double || value is decimal;
    }

    public bool IsNumberType => DataType == ParameterTypes.Number;
    public bool IsSelectType => DataType == ParameterTypes.Select;
    public bool IsBooleanType => DataType == ParameterTypes.Boolean;
    public bool IsTextType => DataType == ParameterTypes.Text;

    // 为DataGrid提供数据类型选项
    public static List<string> DataTypeOptions => ParameterTypes.All;
}