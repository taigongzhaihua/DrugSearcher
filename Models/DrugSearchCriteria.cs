namespace DrugSearcher.Models;

/// <summary>
/// 药物搜索条件
/// </summary>
public class DrugSearchCriteria
{
    /// <summary>
    /// 搜索关键词
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// 是否搜索本地数据库
    /// </summary>
    public bool SearchLocalDb { get; set; } = true;

    /// <summary>
    /// 是否搜索在线数据
    /// </summary>
    public bool SearchOnline { get; set; } = true;

    /// <summary>
    /// 最大结果数量
    /// </summary>
    public int? MaxResults { get; set; } = 100;

    /// <summary>
    /// 最小匹配度（0-1之间）
    /// </summary>
    public double MinMatchScore { get; set; } = 0.0;

    /// <summary>
    /// 数据源过滤
    /// </summary>
    public List<DataSource>? DataSources { get; set; }

    /// <summary>
    /// 是否只返回精确匹配
    /// </summary>
    public bool ExactMatchOnly { get; set; } = false;
}