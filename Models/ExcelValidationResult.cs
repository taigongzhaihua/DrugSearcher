namespace DrugSearcher.Models;

/// <summary>
/// Excel验证结果
/// </summary>
public class ExcelValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 错误消息
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 警告消息
    /// </summary>
    public string? WarningMessage { get; set; }

    /// <summary>
    /// 检测到的列
    /// </summary>
    public List<string> DetectedColumns { get; set; } = new();

    /// <summary>
    /// 缺少的必需列
    /// </summary>
    public List<string> MissingRequiredColumns { get; set; } = new();

    /// <summary>
    /// 缺少的可选列
    /// </summary>
    public List<string> MissingOptionalColumns { get; set; } = new();

    /// <summary>
    /// 数据行数
    /// </summary>
    public int DataRowCount { get; set; }
}