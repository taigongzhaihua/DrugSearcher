namespace DrugSearcher.Models;

/// <summary>
/// 爬取统计信息
/// </summary>
public class CrawlStatistics
{
    /// <summary>
    /// 总记录数
    /// </summary>
    public int TotalRecords { get; set; }

    /// <summary>
    /// 成功数量
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// 失败数量
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// 未找到数量
    /// </summary>
    public int NotFoundCount { get; set; }

    /// <summary>
    /// 解析错误数量
    /// </summary>
    public int ParseErrorCount { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// 今日爬取数量
    /// </summary>
    public int TodayCrawled { get; set; }

    /// <summary>
    /// 本周爬取数量
    /// </summary>
    public int WeekCrawled { get; set; }

    /// <summary>
    /// 本月爬取数量
    /// </summary>
    public int MonthCrawled { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}