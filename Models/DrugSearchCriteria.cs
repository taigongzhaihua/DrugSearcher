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
    /// 页码（用于分页）
    /// </summary>
    public int PageIndex { get; set; } = 0;

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; set; } = 50;
}