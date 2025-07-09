using DrugSearcher.Models;

namespace DrugSearcher.Services
{
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
    }

    /// <summary>
    /// 在线药物服务实现（暂时空实现）
    /// </summary>
    public class OnlineDrugService : IOnlineDrugService
    {
        public async Task<List<DrugInfo>> SearchOnlineDrugsAsync(string keyword)
        {
            // TODO: 实现在线搜索逻辑
            await Task.Delay(100); // 模拟网络延迟
            return [];
        }

        public async Task<DrugInfo?> GetDrugDetailByIdAsync(int id)
        {
            // TODO: 实现在线获取药物详情逻辑
            await Task.Delay(100); // 模拟网络延迟
            return null;
        }
    }
}