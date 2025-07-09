using DrugSearcher.Models;

namespace DrugSearcher.Services
{
    /// <summary>
    /// 本地药物数据服务接口
    /// </summary>
    public interface ILocalDrugService
    {
        /// <summary>
        /// 搜索药物
        /// </summary>
        Task<List<DrugInfo>> SearchDrugsAsync(string keyword);

        /// <summary>
        /// 获取药物详情
        /// </summary>
        Task<DrugInfo?> GetDrugDetailAsync(int id);

        /// <summary>
        /// 获取药物名称建议
        /// </summary>
        Task<List<string>> GetDrugNameSuggestionsAsync(string keyword);

        /// <summary>
        /// 添加药物
        /// </summary>
        Task<DrugInfo> AddDrugAsync(DrugInfo drugInfo);

        /// <summary>
        /// 更新药物
        /// </summary>
        Task<DrugInfo> UpdateDrugAsync(DrugInfo drugInfo);

        /// <summary>
        /// 删除药物
        /// </summary>
        Task<bool> DeleteDrugAsync(int id);

        /// <summary>
        /// 批量删除药物
        /// </summary>
        Task<bool> DeleteDrugsAsync(List<int> ids);

        /// <summary>
        /// 从Excel导入药物数据
        /// </summary>
        Task<ImportResult> ImportFromExcelAsync(string filePath);

        /// <summary>
        /// 导出药物数据到Excel
        /// </summary>
        Task<bool> ExportToExcelAsync(string filePath);

        /// <summary>
        /// 获取所有药物（分页）
        /// </summary>
        Task<(List<DrugInfo> Items, int TotalCount)> GetDrugsPagedAsync(int pageIndex, int pageSize);

        /// <summary>
        /// 获取药物统计信息
        /// </summary>
        Task<DrugStatistics> GetStatisticsAsync();
    }
}