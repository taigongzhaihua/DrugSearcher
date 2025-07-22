using DrugSearcher.Enums;
using DrugSearcher.Models;

namespace DrugSearcher.Repositories;

/// <summary>
/// 在线药物仓储接口
/// </summary>
public interface IOnlineDrugRepository
{
    /// <summary>
    /// 根据ID获取在线药物信息
    /// </summary>
    Task<OnlineDrugInfo?> GetByIdAsync(int id);

    /// <summary>
    /// 获取所有在线药物信息
    /// </summary>
    Task<List<OnlineDrugInfo>> GetAllAsync();

    /// <summary>
    /// 分页获取在线药物信息
    /// </summary>
    Task<(List<OnlineDrugInfo> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize,
        CrawlStatus? status = null);

    /// <summary>
    /// 根据关键词搜索在线药物
    /// </summary>
    Task<List<OnlineDrugInfo>> SearchAsync(string keyword);

    Task<PaginatedResult<OnlineDrugInfo>> SearchWithPaginationOptimizedAsync(string keyword, int pageIndex = 0,
        int pageSize = 20, bool includeCount = true);

    /// <summary>
    /// 获取在线药物名称建议
    /// </summary>
    Task<List<string>> GetDrugNameSuggestionsAsync(string keyword);

    /// <summary>
    /// 检查药物是否存在
    /// </summary>
    Task<bool> ExistsAsync(int id);

    /// <summary>
    /// 添加或更新在线药物信息
    /// </summary>
    Task<OnlineDrugInfo> AddOrUpdateAsync(OnlineDrugInfo onlineDrugInfo);

    /// <summary>
    /// 批量添加或更新在线药物信息
    /// </summary>
    Task<List<OnlineDrugInfo>> AddOrUpdateRangeAsync(List<OnlineDrugInfo> drugInfos);

    /// <summary>
    /// 删除在线药物信息
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 批量删除在线药物信息
    /// </summary>
    Task<bool> DeleteRangeAsync(List<int> ids);

    /// <summary>
    /// 获取指定状态的药物数量
    /// </summary>
    Task<int> GetCountByStatusAsync(CrawlStatus status);

    /// <summary>
    /// 获取失败的药物ID列表
    /// </summary>
    Task<List<int>> GetFailedDrugIdsAsync();

    /// <summary>
    /// 获取成功爬取的药物数量
    /// </summary>
    Task<int> GetSuccessCountAsync();

    /// <summary>
    /// 获取爬取统计信息
    /// </summary>
    Task<CrawlStatistics> GetCrawlStatisticsAsync();

    /// <summary>
    /// 根据状态获取药物列表
    /// </summary>
    Task<List<OnlineDrugInfo>> GetByStatusAsync(CrawlStatus status, int? limit = null);

    /// <summary>
    /// 获取最近爬取的药物
    /// </summary>
    Task<List<OnlineDrugInfo>> GetRecentCrawledAsync(int count = 10);

    /// <summary>
    /// 清理指定状态的旧记录
    /// </summary>
    Task<int> CleanupOldRecordsAsync(CrawlStatus status, DateTime olderThan);
}