using DrugSearcher.Models;

namespace DrugSearcher.Services;

/// <summary>
/// 在线药物服务接口
/// </summary>
public interface IOnlineDrugService
{
    /// <summary>
    /// 搜索在线药物
    /// </summary>
    Task<List<OnlineDrugInfo>> SearchOnlineDrugsAsync(string keyword);

    /// <summary>
    /// 根据ID获取药物详情
    /// </summary>
    Task<OnlineDrugInfo?> GetDrugDetailByIdAsync(int id);

    /// <summary>
    /// 爬取单个药物信息
    /// </summary>
    Task<OnlineDrugInfo?> CrawlSingleDrugInfoAsync(int id);

    /// <summary>
    /// 批量爬取药物信息
    /// </summary>
    Task<CrawlResult> CrawlDrugInfosAsync(int startId, int endId, int batchSize = 10, int delayMs = 1000, IProgress<CrawlProgress>? progress = null);

    /// <summary>
    /// 获取已爬取的药物数量
    /// </summary>
    Task<int> GetCrawledDrugCountAsync();

    /// <summary>
    /// 获取失败的药物ID列表
    /// </summary>
    Task<List<int>> GetFailedDrugIdsAsync();

    /// <summary>
    /// 重新爬取失败的药物
    /// </summary>
    Task<CrawlResult> RetryCrawlFailedDrugsAsync(List<int> failedIds, IProgress<CrawlProgress>? progress = null);

    /// <summary>
    /// 获取爬取统计信息
    /// </summary>
    Task<CrawlStatistics> GetCrawlStatisticsAsync();

    /// <summary>
    /// 清理旧的失败记录
    /// </summary>
    /// <param name="olderThanDays">清理多少天之前的记录</param>
    /// <returns>清理的记录数量</returns>
    Task<int> CleanupOldFailedRecordsAsync(int olderThanDays = 30);

    /// <summary>
    /// 获取最近爬取的药物
    /// </summary>
    /// <param name="count">获取数量</param>
    /// <returns>最近爬取的药物列表</returns>
    Task<List<OnlineDrugInfo>> GetRecentCrawledDrugsAsync(int count = 10);
}