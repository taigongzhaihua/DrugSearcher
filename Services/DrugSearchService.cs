using DrugSearcher.Models;
using Microsoft.Extensions.Logging;

namespace DrugSearcher.Services;

/// <summary>
/// 药物搜索服务
/// </summary>
public class DrugSearchService(
    ILocalDrugService localDrugService,
    ILogger<DrugSearchService> logger,
    IOnlineDrugService? onlineDrugService,
    ICachedDrugService? cachedDrugService)
{
    /// <summary>
    /// 统一搜索药物
    /// </summary>
    /// <param name="criteria">搜索条件</param>
    /// <returns>统一的搜索结果列表</returns>
    public async Task<List<UnifiedDrugSearchResult?>> SearchDrugsAsync(DrugSearchCriteria criteria)
    {
        var allResults = new List<UnifiedDrugSearchResult>();

        try
        {
            // 本地数据库搜索
            if (criteria.SearchLocalDb)
            {
                var localResults = await SearchLocalDrugsAsync(criteria.SearchTerm ?? string.Empty);
                allResults.AddRange(localResults);
            }

            // 在线搜索
            if (criteria.SearchOnline && onlineDrugService != null)
            {
                var onlineResults = await SearchOnlineDrugsAsync(criteria.SearchTerm ?? string.Empty);
                allResults.AddRange(onlineResults);
            }
            //
            // // 缓存搜索
            // if (criteria.SearchOnline && cachedDrugService != null)
            // {
            //     var cachedResults = await SearchCachedDrugsAsync(criteria.SearchTerm ?? string.Empty);
            //     allResults.AddRange(cachedResults);
            // }

            // 去重、排序和限制结果数量
            var deduplicatedResults = DeduplicateResults(allResults);
            var sortedResults = SortResults(deduplicatedResults);

            return [.. sortedResults.Take(criteria.MaxResults ?? 100)];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "搜索药物时发生错误");
            throw;
        }
    }

    /// <summary>
    /// 获取药物详情（支持不同数据源）
    /// </summary>
    /// <param name="id">药物ID</param>
    /// <param name="dataSource">数据来源</param>
    /// <returns>药物详细信息</returns>
    public async Task<BaseDrugInfo?> GetDrugDetailsAsync(int id, DataSource dataSource)
    {
        try
        {
#pragma warning disable CS8602 // 解引用可能出现空引用。
            return dataSource switch
            {
                DataSource.LocalDatabase => await localDrugService.GetDrugDetailAsync(id),
                DataSource.OnlineSearch => await onlineDrugService?.GetDrugDetailByIdAsync(id),
                DataSource.CachedDocuments => await cachedDrugService?.GetCachedDrugDetailAsync(id),
                _ => null
            };
#pragma warning restore CS8602 // 解引用可能出现空引用。
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取药物详情时发生错误，ID: {Id}, 数据源: {DataSource}", id, dataSource);
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
            return [];

        try
        {
            var suggestions = new List<string>();

            // 从本地数据库获取建议
            var localSuggestions = await localDrugService.GetDrugNameSuggestionsAsync(keyword);
            suggestions.AddRange(localSuggestions);

            // 从在线数据获取建议
            if (onlineDrugService != null)
            {
                var onlineResults = await onlineDrugService.SearchOnlineDrugsAsync(keyword);
                var onlineSuggestions = onlineResults
                    .Select(d => d.DrugName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct();
                suggestions.AddRange(onlineSuggestions);
            }
            //
            // // 从缓存获取建议
            // if (cachedDrugService != null)
            // {
            //     var cachedSuggestions = await cachedDrugService.GetCachedDrugNameSuggestionsAsync(keyword);
            //     suggestions.AddRange(cachedSuggestions);
            // }

            // 去重、排序并限制数量
            return
            [
                .. suggestions
                    .Distinct()
                    .Where(s => s.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s)
                    .Take(10)
            ];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "获取搜索建议失败，关键词: {keyword}", keyword);
            return [];
        }
    }

    /// <summary>
    /// 搜索本地药物
    /// </summary>
    private async Task<List<UnifiedDrugSearchResult>> SearchLocalDrugsAsync(string searchTerm)
    {
        try
        {
            var localDrugs = await localDrugService.SearchDrugsAsync(searchTerm);
            return
            [
                .. localDrugs.Select(drug => new UnifiedDrugSearchResult
                {
                    DrugInfo = drug,
                    MatchScore = CalculateMatchScore(drug, searchTerm),
                    MatchedFields = GetMatchedFields(drug, searchTerm),
                    IsExactMatch = IsExactMatch(drug, searchTerm)
                })
            ];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "搜索本地药物失败，搜索词: {searchTerm}", searchTerm);
            return [];
        }
    }

    /// <summary>
    /// 搜索在线药物
    /// </summary>
    private async Task<List<UnifiedDrugSearchResult>> SearchOnlineDrugsAsync(string searchTerm)
    {
        try
        {
            if (onlineDrugService == null)
                return [];

            var onlineDrugs = await onlineDrugService.SearchOnlineDrugsAsync(searchTerm);
            return
            [
                .. onlineDrugs.Select(drug => new UnifiedDrugSearchResult
                {
                    DrugInfo = drug,
                    MatchScore = CalculateMatchScore(drug, searchTerm),
                    MatchedFields = GetMatchedFields(drug, searchTerm),
                    IsExactMatch = IsExactMatch(drug, searchTerm)
                })
            ];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "搜索在线药物失败，搜索词: {searchTerm}", searchTerm);
            return [];
        }
    }

    /// <summary>
    /// 搜索缓存药物
    /// </summary>
    private async Task<List<UnifiedDrugSearchResult>> SearchCachedDrugsAsync(string searchTerm)
    {
        try
        {
            if (cachedDrugService == null)
                return [];

            var cachedDrugs = await cachedDrugService.SearchCachedDrugsAsync(searchTerm);
            return
            [
                .. cachedDrugs.Select(drug => new UnifiedDrugSearchResult
                {
                    DrugInfo = drug,
                    MatchScore = CalculateMatchScore(drug, searchTerm),
                    MatchedFields = GetMatchedFields(drug, searchTerm),
                    IsExactMatch = IsExactMatch(drug, searchTerm)
                })
            ];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "搜索缓存药物失败，搜索词: {searchTerm}", searchTerm);
            return [];
        }
    }

    /// <summary>
    /// 计算匹配度
    /// </summary>
    private static double CalculateMatchScore(BaseDrugInfo drug, string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return 0.0;

        var searchTermLower = searchTerm.ToLower();
        var fullDescription = drug.GetFullDescription().ToLower();

        // 精确匹配得分最高
        if (drug.DrugName.Equals(searchTerm, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        // 药物名称包含搜索词
        if (drug.DrugName.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase))
            return 0.8;

        // 制造商匹配
        if (!string.IsNullOrEmpty(drug.Manufacturer) &&
            drug.Manufacturer.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase))
            return 0.6;

        // 完整描述包含搜索词
        if (fullDescription.Contains(searchTermLower))
            return 0.4;

        return 0.0;
    }

    /// <summary>
    /// 获取匹配的字段
    /// </summary>
    private static List<string> GetMatchedFields(BaseDrugInfo drug, string searchTerm)
    {
        var matchedFields = new List<string>();
        if (string.IsNullOrWhiteSpace(searchTerm))
            return matchedFields;

        var searchTermLower = searchTerm.ToLower();

        if (drug.DrugName.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase))
            matchedFields.Add("药物名称");

        if (!string.IsNullOrEmpty(drug.Manufacturer) &&
            drug.Manufacturer.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase))
            matchedFields.Add("生产厂家");

        if (!string.IsNullOrEmpty(drug.Specification) &&
            drug.Specification.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase))
            matchedFields.Add("规格");

        if (!string.IsNullOrEmpty(drug.ApprovalNumber) &&
            drug.ApprovalNumber.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase))
            matchedFields.Add("批准文号");

        return matchedFields;
    }

    /// <summary>
    /// 判断是否为精确匹配
    /// </summary>
    private static bool IsExactMatch(BaseDrugInfo drug, string searchTerm) => drug.DrugName.Equals(searchTerm, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 去重结果
    /// </summary>
    private static List<UnifiedDrugSearchResult> DeduplicateResults(List<UnifiedDrugSearchResult> results) =>
        // 基于药物名称、规格和制造商进行去重，保留数据源优先级最高的
        [
            .. results
                .GroupBy(r => new
                {
                    r.DrugInfo.DrugName,
                    r.DrugInfo.Specification,
                    r.DrugInfo.Manufacturer
                })
                .Select(g => g.OrderBy(r => r.DataSourcePriority).ThenByDescending(r => r.MatchScore).First())
        ];

    /// <summary>
    /// 排序结果
    /// </summary>
    private static List<UnifiedDrugSearchResult> SortResults(List<UnifiedDrugSearchResult> results) => [
            .. results
                .OrderByDescending(r => r.IsExactMatch)
                .ThenByDescending(r => r.MatchScore)
                .ThenBy(r => r.DataSourcePriority)
                .ThenBy(r => r.DrugInfo.DrugName)
        ];
}