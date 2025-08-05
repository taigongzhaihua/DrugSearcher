namespace DrugSearcher.Models;

/// <summary>
/// 爬取结果
/// </summary>
public class CrawlResult
{
    public int TotalProcessed { get; set; }

    public int SuccessCount { get; set; }

    public int FailedCount { get; set; }

    public List<OnlineDrugInfo> CrawledDrugs { get; set; } = [];

    public List<int> FailedIds { get; set; } = [];

    public DateTime StartTime { get; set; } = DateTime.Now;

    public DateTime EndTime { get; set; }

    public TimeSpan Duration => EndTime - StartTime;
}

/// <summary>
/// 爬取进度
/// </summary>
public class CrawlProgress
{
    public int TotalProcessed { get; set; }

    public int SuccessCount { get; set; }

    public int FailedCount { get; set; }

    public int CurrentId { get; set; }

    public double ProgressPercentage { get; set; }
}