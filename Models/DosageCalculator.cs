using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrugSearcher.Models;

/// <summary>
/// 剂量计算器模型
/// </summary>
[Table("DosageCalculators")]
public class DosageCalculator
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 药物标识符 - 格式：{DataSource}_{Id}
    /// 例如：LocalDatabase_123, OnlineDatabase_456
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DrugIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// 数据源类型
    /// </summary>
    [Required]
    public DataSource DataSource { get; set; }

    /// <summary>
    /// 原始药物ID（在对应表中的实际ID）
    /// </summary>
    [Required]
    public int OriginalDrugId { get; set; }

    [Required]
    [MaxLength(200)]
    public string DrugName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string CalculatorName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "TEXT")]
    public string CalculationCode { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "TEXT")]
    public string ParameterDefinitions { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public bool IsActive { get; set; } = true;

    [MaxLength(50)]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// 生成药物标识符
    /// </summary>
    public static string GenerateDrugIdentifier(DataSource dataSource, int drugId) => $"{dataSource}_{drugId}";

    /// <summary>
    /// 解析药物标识符
    /// </summary>
    public static (DataSource DataSource, int DrugId) ParseDrugIdentifier(string identifier)
    {
        var parts = identifier.Split('_');
        if (parts.Length != 2)
            throw new ArgumentException("Invalid drug identifier format");

        var dataSource = Enum.Parse<DataSource>(parts[0]);
        var drugId = int.Parse(parts[1]);
        return (dataSource, drugId);
    }
}

public class DosageParameter
{
    public string? Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty; // "number", "text", "select", "boolean"
    public string Unit { get; set; } = string.Empty;
    public bool IsRequired { get; set; } = true;
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }

    /// <summary>
    /// 默认值 - 支持多种类型（数字、字符串、布尔值）
    /// </summary>
    [JsonConverter(typeof(FlexibleDefaultValueConverter))]
    public object? DefaultValue { get; set; }

    public List<string> Options { get; set; } = [];
    public string Description { get; set; } = string.Empty;

    [JsonIgnore]
    public object Value { get; set; } = null!; // UI绑定用
    /// <summary>
    /// 获取类型化的默认值
    /// </summary>
    public T? GetTypedDefaultValue<T>()
    {
        if (DefaultValue == null) return default;

        try
        {
            if (DefaultValue is T directValue)
                return directValue;

            return (T)Convert.ChangeType(DefaultValue, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 根据DataType获取适当的默认值
    /// </summary>
    public object GetDefaultValueByDataType()
    {
        if (DefaultValue == null)
        {
            return DataType switch
            {
                ParameterTypes.Number => 0.0,
                ParameterTypes.Boolean => false,
                _ => string.Empty
            };
        }

        return DataType switch
        {
            ParameterTypes.Number => (object)GetTypedDefaultValue<double>() ?? 0.0,
            ParameterTypes.Boolean => (object)GetTypedDefaultValue<bool>() ?? false,
            _ => GetTypedDefaultValue<string>() ?? string.Empty
        };
    }
}

/// <summary>
/// 灵活的默认值JSON转换器
/// </summary>
public class FlexibleDefaultValueConverter : JsonConverter<object?>
{
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();
            case JsonTokenType.Number:
                if (reader.TryGetDouble(out var doubleValue))
                    return doubleValue;
                if (reader.TryGetInt32(out var intValue))
                    return intValue;
                return reader.GetDecimal();
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Null:
                return null;
            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }

        switch (value)
        {
            case string stringValue:
                writer.WriteStringValue(stringValue);
                break;
            case double doubleValue:
                writer.WriteNumberValue(doubleValue);
                break;
            case float floatValue:
                writer.WriteNumberValue(floatValue);
                break;
            case int intValue:
                writer.WriteNumberValue(intValue);
                break;
            case long longValue:
                writer.WriteNumberValue(longValue);
                break;
            case bool boolValue:
                writer.WriteBooleanValue(boolValue);
                break;
            case decimal decimalValue:
                writer.WriteNumberValue(decimalValue);
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}

/// <summary>
/// 剂量计算结果
/// </summary>
public class DosageCalculationResult
{
    public string Description { get; set; } = string.Empty;
    public double Dose { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsWarning { get; set; }
    public string WarningMessage { get; set; } = string.Empty;
}

/// <summary>
/// 剂量计算请求
/// </summary>
public class DosageCalculationRequest
{
    public int CalculatorId { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = [];

    public string CalculationCode { get; set; } = string.Empty;
}

/// <summary>
/// 计算器统计信息
/// </summary>
public class DosageCalculatorStatistics
{
    public int TotalCalculators { get; set; }
    public int ActiveCalculators { get; set; }
    public int TodayCreated { get; set; }
    public int WeekCreated { get; set; }
    public int MonthCreated { get; set; }
    public Dictionary<string, int> CalculatorsByDrug { get; set; } = [];
}

/// <summary>
/// 参数类型常量
/// </summary>
public static class ParameterTypes
{
    public const string Number = "number";
    public const string Text = "text";
    public const string Select = "select";
    public const string Boolean = "boolean";

    public static readonly List<string> All = [Number, Text, Select, Boolean];
}