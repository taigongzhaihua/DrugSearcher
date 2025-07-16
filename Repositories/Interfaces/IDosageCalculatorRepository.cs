using DrugSearcher.Models;

namespace DrugSearcher.Repositories;

/// <summary>
/// 剂量计算器仓储接口
/// </summary>
public interface IDosageCalculatorRepository
{
    /// <summary>
    /// 根据ID获取计算器
    /// </summary>
    Task<DosageCalculator?> GetByIdAsync(int id);

    /// <summary>
    /// 获取所有计算器
    /// </summary>
    Task<List<DosageCalculator>> GetAllAsync();

    /// <summary>
    /// 根据药物标识符获取计算器列表
    /// </summary>
    Task<List<DosageCalculator>> GetByDrugIdentifierAsync(string drugIdentifier);

    /// <summary>
    /// 根据数据源和药物ID获取计算器列表
    /// </summary>
    Task<List<DosageCalculator>> GetByDataSourceAndDrugIdAsync(DataSource dataSource, int drugId);

    /// <summary>
    /// 根据统一药物信息获取计算器列表
    /// </summary>
    Task<List<DosageCalculator>> GetByUnifiedDrugAsync(BaseDrugInfo drugInfo);

    /// <summary>
    /// 分页获取计算器
    /// </summary>
    Task<(List<DosageCalculator> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize);

    /// <summary>
    /// 搜索计算器
    /// </summary>
    Task<List<DosageCalculator>> SearchAsync(string keyword);

    /// <summary>
    /// 检查计算器是否存在
    /// </summary>
    Task<bool> ExistsAsync(DataSource dataSource, int drugId, string calculatorName);

    /// <summary>
    /// 添加计算器
    /// </summary>
    Task<DosageCalculator> AddAsync(DosageCalculator calculator);

    /// <summary>
    /// 更新计算器
    /// </summary>
    Task<DosageCalculator> UpdateAsync(DosageCalculator calculator);

    /// <summary>
    /// 删除计算器
    /// </summary>
    Task<bool> DeleteAsync(int id);

    /// <summary>
    /// 批量删除计算器
    /// </summary>
    Task<bool> DeleteRangeAsync(List<int> ids);

    /// <summary>
    /// 获取计算器统计信息
    /// </summary>
    Task<DosageCalculatorStatistics> GetStatisticsAsync();
}