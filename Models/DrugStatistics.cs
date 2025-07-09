namespace DrugSearcher.Models;

/// <summary>
/// 药物统计信息
/// </summary>
public class DrugStatistics
{
    /// <summary>
    /// 总药物数量
    /// </summary>
    public int TotalDrugs { get; set; }

    /// <summary>
    /// 今日新增数量
    /// </summary>
    public int TodayAdded { get; set; }

    /// <summary>
    /// 本周新增数量
    /// </summary>
    public int WeekAdded { get; set; }

    /// <summary>
    /// 本月新增数量
    /// </summary>
    public int MonthAdded { get; set; }
}