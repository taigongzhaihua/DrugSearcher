using DrugSearcher.Models;

namespace DrugSearcher.Repositories;

/// <summary>
/// 药物仓储接口
/// </summary>
public interface IDrugRepository
{
    /// <summary>
    /// 根据ID获取药物信息
    /// </summary>
    Task<DrugInfo?> GetByIdAsync(int id);

    /// <summary>
    /// 获取所有药物信息
    /// </summary>
    Task<List<DrugInfo>> GetAllAsync();

    /// <summary>
    /// 分页获取药物信息
    /// </summary>
    Task<(List<DrugInfo> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize);

    /// <summary>
    /// 根据关键词搜索药物
    /// </summary>
    Task<List<DrugInfo>> SearchAsync(string keyword);

    /// <summary>
    /// 获取药物名称建议
    /// </summary>
    Task<List<string>> GetDrugNameSuggestionsAsync(string keyword);

    /// <summary>
    /// 检查药物是否存在（根据名称、规格、厂家）
    /// </summary>
    Task<bool> ExistsAsync(string drugName, string? specification, string? manufacturer);

    /// <summary>
    /// 添加药物信息
    /// </summary>
    Task<DrugInfo> AddAsync(DrugInfo drugInfo);

    /// <summary>
    /// 批量添加药物信息
    /// </summary>
    Task<List<DrugInfo>> AddRangeAsync(List<DrugInfo> drugInfos);

    /// <summary>
    /// 更新药物信息
    /// </summary>
    Task<DrugInfo> UpdateAsync(DrugInfo drugInfo);

    /// <summary>
    /// 批量更新药物信息
    /// </summary>
    Task<List<DrugInfo>> UpdateRangeAsync(List<DrugInfo> drugInfos);

    /// <summary>
    /// 删除药物信息
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 批量删除药物信息
    /// </summary>
    Task<bool> DeleteRangeAsync(List<int> ids);

    /// <summary>
    /// 获取重复的药物记录
    /// </summary>
    Task<List<DrugInfo>> GetDuplicatesAsync(List<ExcelImportDto> importData);
}