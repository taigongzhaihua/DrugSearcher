using DrugSearcher.Models;

namespace DrugSearcher.Services;

/// <summary>
/// 缓存药物服务接口
/// </summary>
public interface ICachedDrugService
{
    /// <summary>
    /// 搜索缓存的药物
    /// </summary>
    /// <param name="keyword">关键词</param>
    /// <returns>药物列表</returns>
    Task<List<DrugInfo>> SearchCachedDrugsAsync(string keyword);

    /// <summary>
    /// 获取缓存的药物详情
    /// </summary>
    /// <param name="id">药物ID</param>
    /// <returns>药物信息</returns>
    Task<DrugInfo?> GetCachedDrugDetailAsync(int id);

    /// <summary>
    /// 更新缓存的药物数据
    /// </summary>
    /// <param name="drugInfo">药物信息</param>
    /// <returns></returns>
    Task UpdateCachedDrugAsync(DrugInfo drugInfo);

    /// <summary>
    /// 获取缓存的药物名称建议
    /// </summary>
    /// <param name="keyword">关键词</param>
    /// <returns>建议列表</returns>
    Task<List<string>> GetCachedDrugNameSuggestionsAsync(string keyword);
}

/// <summary>
/// 缓存药物服务实现（暂时空实现）
/// </summary>
public class CachedDrugService : ICachedDrugService
{
    public async Task<List<DrugInfo>> SearchCachedDrugsAsync(string keyword)
    {
        // TODO: 实现缓存搜索逻辑
        await Task.Delay(50); // 模拟查询延迟
        return [];
    }

    public async Task<DrugInfo?> GetCachedDrugDetailAsync(int id)
    {
        // TODO: 实现缓存详情获取逻辑
        await Task.Delay(50); // 模拟查询延迟
        return null;
    }

    public async Task UpdateCachedDrugAsync(DrugInfo drugInfo)
    {
        // TODO: 实现缓存更新逻辑
        await Task.Delay(50); // 模拟更新延迟
    }

    public async Task<List<string>> GetCachedDrugNameSuggestionsAsync(string keyword)
    {
        // TODO: 实现缓存建议获取逻辑
        await Task.Delay(50); // 模拟查询延迟
        return [];
    }
}