using DrugSearcher.Models;

namespace DrugSearcher.Services;

/// <summary>
/// 在线药物服务接口
/// </summary>
public interface IOnlineDrugService
{
    /// <summary>
    /// 在线搜索药物
    /// </summary>
    /// <param name="keyword">关键词</param>
    /// <returns>药物列表</returns>
    Task<List<DrugInfo>> SearchOnlineDrugsAsync(string keyword);

    /// <summary>
    /// 根据ID获取在线药物详情
    /// </summary>
    /// <param name="id">药物ID</param>
    /// <returns>药物信息</returns>
    Task<DrugInfo?> GetDrugDetailByIdAsync(int id);

    /// <summary>
    /// 根据外部ID获取药物详情
    /// </summary>
    /// <param name="externalId">外部ID</param>
    /// <returns>药物信息</returns>
    Task<DrugInfo?> GetDrugDetailByExternalIdAsync(string externalId);

    /// <summary>
    /// 批量获取药物信息
    /// </summary>
    /// <param name="startId">起始ID</param>
    /// <param name="endId">结束ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>药物信息列表</returns>
    Task<List<DrugInfo>> GetDrugsBatchAsync(int startId, int endId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可用的药物总数
    /// </summary>
    /// <returns>总数</returns>
    Task<int> GetAvailableDrugCountAsync();
}