using DrugSearcher.Models;

namespace DrugSearcher.Services
{
    /// <summary>
    /// 药物搜索服务
    /// </summary>
    public class DrugSearchService(
        ILocalDrugService localDrugService,
        IOnlineDrugService? onlineDrugService = null,
        ICachedDrugService? cachedDrugService = null)
    {
        // 在线搜索服务，可选
        // 缓存服务，可选

        /// <summary>
        /// 搜索药物
        /// </summary>
        /// <param name="criteria">搜索条件</param>
        /// <returns>药物信息列表</returns>
        public async Task<List<DrugInfo>> SearchDrugsAsync(DrugSearchCriteria criteria)
        {
            var results = new List<DrugInfo>();

            try
            {
                // 本地数据库搜索
                if (criteria.SearchLocalDb)
                {
                    var localResults = await localDrugService.SearchDrugsAsync(criteria.SearchTerm ?? string.Empty);
                    results.AddRange(localResults);
                }

                // 在线搜索
                if (criteria.SearchOnline)
                {
                    // 先尝试从缓存获取
                    if (cachedDrugService != null)
                    {
                        var cachedResults = await cachedDrugService.SearchCachedDrugsAsync(criteria.SearchTerm ?? string.Empty);
                        results.AddRange(cachedResults);
                    }

                    // 如果启用在线搜索且在线服务可用
                    if (onlineDrugService != null)
                    {
                        try
                        {
                            var onlineResults = await onlineDrugService.SearchOnlineDrugsAsync(criteria.SearchTerm ?? string.Empty);
                            results.AddRange(onlineResults);
                        }
                        catch (Exception ex)
                        {
                            // 在线搜索失败不影响整体搜索结果
                            System.Diagnostics.Debug.WriteLine($"在线搜索失败: {ex.Message}");
                        }
                    }
                }

                // 去重处理（基于药物名称和批准文号）
                results = results
                    .GroupBy(d => new { d.DrugName, d.ApprovalNumber })
                    .Select(g => g.First())
                    .OrderBy(d => d.DrugName)
                    .ToList();

                return results;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"搜索药物时发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取药物详情
        /// </summary>
        /// <param name="id">药物ID</param>
        /// <param name="dataSource">数据来源</param>
        /// <returns>药物详细信息</returns>
        public async Task<DrugInfo?> GetDrugDetailsAsync(int id, DataSource dataSource)
        {
            try
            {
                return dataSource switch
                {
                    DataSource.LocalDatabase => await localDrugService.GetDrugDetailAsync(id),
                    DataSource.OnlineSearch => await GetOnlineDrugDetailsAsync(id),
                    DataSource.CachedDocuments => await GetCachedDrugDetailsAsync(id),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取药物详情时发生错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 更新缓存数据
        /// </summary>
        /// <param name="id">药物ID</param>
        /// <returns></returns>
        public async Task UpdateCachedDataAsync(int id)
        {
            try
            {
                if (cachedDrugService != null && onlineDrugService != null)
                {
                    // 从在线获取最新数据
                    var onlineData = await onlineDrugService.GetDrugDetailByIdAsync(id);
                    if (onlineData != null)
                    {
                        // 更新缓存
                        await cachedDrugService.UpdateCachedDrugAsync(onlineData);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新缓存数据失败: {ex.Message}");
                // 静默处理，不影响用户体验
            }
        }

        /// <summary>
        /// 获取在线药物详情
        /// </summary>
        /// <param name="id">药物ID</param>
        /// <returns>药物信息</returns>
        private async Task<DrugInfo?> GetOnlineDrugDetailsAsync(int id)
        {
            if (onlineDrugService == null)
                return null;

            try
            {
                return await onlineDrugService.GetDrugDetailByIdAsync(id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取在线药物详情失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取缓存药物详情
        /// </summary>
        /// <param name="id">药物ID</param>
        /// <returns>药物信息</returns>
        private async Task<DrugInfo?> GetCachedDrugDetailsAsync(int id)
        {
            if (cachedDrugService == null)
                return null;

            try
            {
                return await cachedDrugService.GetCachedDrugDetailAsync(id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取缓存药物详情失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取搜索建议
        /// </summary>
        /// <param name="keyword">关键词</param>
        /// <returns>建议列表</returns>
        public async Task<List<string>> GetSearchSuggestionsAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<string>();

            try
            {
                var suggestions = new List<string>();

                // 从本地数据库获取建议
                var localSuggestions = await localDrugService.GetDrugNameSuggestionsAsync(keyword);
                suggestions.AddRange(localSuggestions);

                // 从缓存获取建议
                if (cachedDrugService != null)
                {
                    var cachedSuggestions = await cachedDrugService.GetCachedDrugNameSuggestionsAsync(keyword);
                    suggestions.AddRange(cachedSuggestions);
                }

                // 去重并排序
                return suggestions.Distinct().OrderBy(s => s).Take(10).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取搜索建议失败: {ex.Message}");
                return new List<string>();
            }
        }
    }
}