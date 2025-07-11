namespace DrugSearcher.Models;

/// <summary>
/// 统一的药物搜索结果
/// </summary>
public class UnifiedDrugSearchResult
{
    /// <summary>
    /// 药物信息
    /// </summary>
    public BaseDrugInfo DrugInfo { get; set; } = null!;

    /// <summary>
    /// 搜索匹配度（0-1之间）
    /// </summary>
    public double MatchScore { get; set; }

    /// <summary>
    /// 匹配的字段
    /// </summary>
    public List<string> MatchedFields { get; set; } = [];

    /// <summary>
    /// 是否为精确匹配
    /// </summary>
    public bool IsExactMatch { get; set; }

    /// <summary>
    /// 数据来源优先级（数字越小优先级越高）
    /// </summary>
    public int DataSourcePriority => DrugInfo.DataSource switch
    {
        DataSource.LocalDatabase => 1,
        DataSource.CachedDocuments => 2,
        DataSource.OnlineSearch => 3,
        _ => 999
    };
}