namespace DrugSearcher.Models;

/// <summary>
/// 导入结果
/// </summary>
public class ImportResult
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 总记录数
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// 成功导入记录数
    /// </summary>
    public int SuccessRecords { get; set; }

    /// <summary>
    /// 重复记录数
    /// </summary>
    public int DuplicateRecords { get; set; }

    /// <summary>
    /// 失败记录数
    /// </summary>
    public int FailedRecords { get; set; }

    /// <summary>
    /// 错误详情
    /// </summary>
    public List<string> ErrorDetails { get; set; } = [];
}