using DrugSearcher.Data;
using DrugSearcher.Enums;
using DrugSearcher.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DrugSearcher.Repositories;

/// <summary>
/// 在线药物仓储适配器 - 优化版本带缓存机制
/// </summary>
public class OnlineDrugRepository(
    IDrugDbContextFactory contextFactory,
    IMemoryCache cache,
    ILogger<OnlineDrugRepository> logger) : IOnlineDrugRepository
{
    private static readonly TimeSpan DefaultCacheExpiration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ShortCacheExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LongCacheExpiration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan StatisticsCacheExpiration = TimeSpan.FromMinutes(15);

    private const string CACHE_KEY_PREFIX = "OnlineDrug";
    private const string CACHE_KEY_ALL = $"{CACHE_KEY_PREFIX}_All";
    private const string CACHE_KEY_STATISTICS = $"{CACHE_KEY_PREFIX}_Statistics";
    private const string CACHE_KEY_COUNT_PREFIX = $"{CACHE_KEY_PREFIX}_Count";
    private const string CACHE_KEY_SEARCH_PREFIX = $"{CACHE_KEY_PREFIX}_Search";
    private const string CACHE_KEY_SUGGESTIONS_PREFIX = $"{CACHE_KEY_PREFIX}_Suggestions";
    private const string CACHE_KEY_RECENT_PREFIX = $"{CACHE_KEY_PREFIX}_Recent";
    private const string CACHE_KEY_PAGED_PREFIX = $"{CACHE_KEY_PREFIX}_Paged";

    public async Task<OnlineDrugInfo?> GetByIdAsync(int id)
    {
        var cacheKey = $"{CACHE_KEY_PREFIX}_ById_{id}";

        if (cache.TryGetValue<OnlineDrugInfo>(cacheKey, out var cached))
        {
            return cached;
        }

        await using var context = contextFactory.CreateDbContext();
        var result = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);

        if (result != null)
        {
            cache.Set(cacheKey, result, DefaultCacheExpiration);
        }

        return result;
    }

    // Fix for CA1873: Wrap expensive logging arguments in a conditional check for IsEnabled
    public async Task<List<OnlineDrugInfo>> GetAllAsync()
    {
        if (cache.TryGetValue<List<OnlineDrugInfo>>(CACHE_KEY_ALL, out var cached))
        {
            if (cached != null) return cached;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await using var context = contextFactory.CreateDbContext();

        var result = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .OrderBy(d => d.DrugName)
            .ToListAsync();

        sw.Stop();

        // Check if logging is enabled before evaluating the expensive argument
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug($"GetAllAsync 查询耗时: {sw.ElapsedMilliseconds}ms, 返回 {result.Count} 条记录");
        }

        // 使用较短的缓存时间，因为数据量可能很大
        cache.Set(CACHE_KEY_ALL, result, ShortCacheExpiration);
        return result;
    }

    public async Task<(List<OnlineDrugInfo> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize, CrawlStatus? status = null)
    {
        var cacheKey = $"{CACHE_KEY_PAGED_PREFIX}_{pageIndex}_{pageSize}_{status?.ToString() ?? "null"}";

        if (cache.TryGetValue<(List<OnlineDrugInfo>, int)>(cacheKey, out var cached))
        {
            return cached;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await using var context = contextFactory.CreateDbContext();

        var query = (context.OnlineDrugInfos ?? throw new InvalidOperationException()).AsNoTracking().AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(d => d.CrawlStatus == status.Value);
        }

        // 并行执行计数和数据查询
        var countTask = query.CountAsync();
        var itemsTask = query
            .OrderBy(d => d.DrugName)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        await Task.WhenAll(countTask, itemsTask);

        var result = (await itemsTask, await countTask);

        sw.Stop();

        // Check if logging is enabled before evaluating the expensive argument
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug($"GetPagedAsync 查询耗时: {sw.ElapsedMilliseconds}ms");
        }

        cache.Set(cacheKey, result, DefaultCacheExpiration);
        return result;
    }

    public async Task<List<OnlineDrugInfo>> SearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        var cacheKey = $"{CACHE_KEY_SEARCH_PREFIX}_{keyword.ToLower().Trim()}";

        if (cache.TryGetValue<List<OnlineDrugInfo>>(cacheKey, out var cached))
        {
            if (cached != null) return cached;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await using var context = contextFactory.CreateDbContext();

        var keywordTrimmed = keyword.Trim();

        var result = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .Where(d => d.CrawlStatus == CrawlStatus.Success &&
                       (EF.Functions.Like(d.DrugName, $"%{keywordTrimmed}%") ||
                        EF.Functions.Like(d.TradeName ?? "", $"%{keywordTrimmed}%") ||
                        EF.Functions.Like(d.Manufacturer ?? "", $"%{keywordTrimmed}%") ||
                        EF.Functions.Like(d.ApprovalNumber ?? "", $"%{keywordTrimmed}%") ||
                        EF.Functions.Like(d.Specification ?? "", $"%{keywordTrimmed}%")))
            .OrderBy(d => d.DrugName)
            .ToListAsync();

        sw.Stop();

        // Check if logging is enabled before evaluating the expensive argument
        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug($"SearchAsync '{keyword}' 耗时: {sw.ElapsedMilliseconds}ms, 返回 {result.Count} 条记录");
        }

        cache.Set(cacheKey, result, DefaultCacheExpiration);
        return result;
    }

    public async Task<PaginatedResult<OnlineDrugInfo>> SearchWithPaginationOptimizedAsync(
        string keyword,
        int pageIndex = 0,
        int pageSize = 20,
        bool includeCount = true)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return new PaginatedResult<OnlineDrugInfo>
            {
                Items = [],
                TotalCount = 0,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        var cacheKey = $"{CACHE_KEY_SEARCH_PREFIX}_Paginated_{keyword.ToLower().Trim()}_{pageIndex}_{pageSize}_{includeCount}";

        if (cache.TryGetValue<PaginatedResult<OnlineDrugInfo>>(cacheKey, out var cached))
        {
            if (cached != null) return cached;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await using var context = contextFactory.CreateDbContext();

        var keywordTrimmed = keyword.Trim();
        var query = (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .Where(d => d.CrawlStatus == CrawlStatus.Success)
            .Where(d =>
                EF.Functions.Like(d.DrugName, $"%{keywordTrimmed}%") ||
                EF.Functions.Like(d.TradeName ?? "", $"%{keywordTrimmed}%") ||
                EF.Functions.Like(d.Manufacturer ?? "", $"%{keywordTrimmed}%") ||
                EF.Functions.Like(d.ApprovalNumber ?? "", $"%{keywordTrimmed}%") ||
                EF.Functions.Like(d.Specification ?? "", $"%{keywordTrimmed}%"));

        // 并行执行计数和数据查询
        var countTask = includeCount || pageIndex == 0
            ? query.CountAsync()
            : Task.FromResult(0);

        var itemsTask = query
            .OrderBy(d => d.DrugName)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        await Task.WhenAll(countTask, itemsTask);

        var result = new PaginatedResult<OnlineDrugInfo>
        {
            Items = await itemsTask,
            TotalCount = await countTask,
            PageIndex = pageIndex,
            PageSize = pageSize
        };

        sw.Stop();
        var message = $"SearchWithPaginationOptimizedAsync '{keyword}' 耗时: {sw.ElapsedMilliseconds}ms";
        logger.LogDebug(message);

        cache.Set(cacheKey, result, ShortCacheExpiration);
        return result;
    }

    public async Task<List<string>> GetDrugNameSuggestionsAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        var cacheKey = $"{CACHE_KEY_SUGGESTIONS_PREFIX}_{keyword.ToLower().Trim()}";

        if (cache.TryGetValue<List<string>>(cacheKey, out var cached))
        {
            if (cached != null) return cached;
        }

        await using var context = contextFactory.CreateDbContext();
        var keywordTrimmed = keyword.Trim();

        var result = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .Where(d => d.CrawlStatus == CrawlStatus.Success &&
                       EF.Functions.Like(d.DrugName, $"%{keywordTrimmed}%"))
            .Select(d => d.DrugName)
            .Distinct()
            .Take(10)
            .ToListAsync();

        cache.Set(cacheKey, result, LongCacheExpiration);
        return result;
    }

    public async Task<bool> ExistsAsync(int id)
    {
        var cacheKey = $"{CACHE_KEY_PREFIX}_Exists_{id}";

        if (cache.TryGetValue<bool>(cacheKey, out var cached))
        {
            return cached;
        }

        await using var context = contextFactory.CreateDbContext();
        var result = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .AnyAsync(d => d.Id == id);

        cache.Set(cacheKey, result, LongCacheExpiration);
        return result;
    }

    public async Task<OnlineDrugInfo> AddOrUpdateAsync(OnlineDrugInfo onlineDrugInfo)
    {
        await using var context = contextFactory.CreateDbContext();

        var existing = await (context.OnlineDrugInfos ?? throw new InvalidOperationException()).FindAsync(onlineDrugInfo.Id);

        if (existing == null)
        {
            context.OnlineDrugInfos.Add(onlineDrugInfo);
        }
        else
        {
            context.Entry(existing).CurrentValues.SetValues(onlineDrugInfo);
            existing.UpdatedAt = DateTime.Now;
        }

        await context.SaveChangesAsync();

        // 清除相关缓存
        InvalidateRelatedCaches(onlineDrugInfo.Id);

        return onlineDrugInfo;
    }

    public async Task<List<OnlineDrugInfo>> AddOrUpdateRangeAsync(List<OnlineDrugInfo> drugInfos)
    {
        await using var context = contextFactory.CreateDbContext();

        foreach (var drugInfo in drugInfos)
        {
            var existing = await (context.OnlineDrugInfos ?? throw new InvalidOperationException()).FindAsync(drugInfo.Id);

            if (existing == null)
            {
                (context.OnlineDrugInfos ?? throw new InvalidOperationException()).Add(drugInfo);
            }
            else
            {
                context.Entry(existing).CurrentValues.SetValues(drugInfo);
                existing.UpdatedAt = DateTime.Now;
            }
        }

        await context.SaveChangesAsync();

        // 清除相关缓存
        InvalidateAllCaches();

        return drugInfos;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = contextFactory.CreateDbContext();
        var drugInfo = await (context.OnlineDrugInfos ?? throw new InvalidOperationException()).FindAsync(id);
        if (drugInfo == null) return false;

        context.OnlineDrugInfos.Remove(drugInfo);
        await context.SaveChangesAsync();

        // 清除相关缓存
        InvalidateRelatedCaches(id);

        return true;
    }

    public async Task<bool> DeleteRangeAsync(List<int> ids)
    {
        await using var context = contextFactory.CreateDbContext();
        var drugInfos = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .Where(d => ids.Contains(d.Id))
            .ToListAsync();

        if (drugInfos.Count == 0) return false;

        context.OnlineDrugInfos.RemoveRange(drugInfos);
        await context.SaveChangesAsync();

        // 清除相关缓存
        InvalidateAllCaches();

        return true;
    }

    public async Task<int> GetCountByStatusAsync(CrawlStatus status)
    {
        var cacheKey = $"{CACHE_KEY_COUNT_PREFIX}_{status}";

        if (cache.TryGetValue<int>(cacheKey, out var cached))
        {
            return cached;
        }

        await using var context = contextFactory.CreateDbContext();
        var result = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .CountAsync(d => d.CrawlStatus == status);

        cache.Set(cacheKey, result, DefaultCacheExpiration);
        return result;
    }

    public async Task<List<int>> GetFailedDrugIdsAsync()
    {
        const string cacheKey = $"{CACHE_KEY_PREFIX}_FailedIds";

        if (cache.TryGetValue<List<int>>(cacheKey, out var cached))
        {
            if (cached != null) return cached;
        }

        await using var context = contextFactory.CreateDbContext();
        var result = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .Where(d => d.CrawlStatus != CrawlStatus.Success)
            .Select(d => d.Id)
            .ToListAsync();

        cache.Set(cacheKey, result, DefaultCacheExpiration);
        return result;
    }

    public async Task<int> GetSuccessCountAsync() => await GetCountByStatusAsync(CrawlStatus.Success);

    public async Task<CrawlStatistics> GetCrawlStatisticsAsync()
    {
        if (cache.TryGetValue<CrawlStatistics>(CACHE_KEY_STATISTICS, out var cached))
        {
            if (cached != null) return cached;
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await using var context = contextFactory.CreateDbContext();

        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        // 使用批量查询优化
        var allRecords = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .Select(d => new { d.CrawlStatus, d.CrawledAt })
            .ToListAsync();

        var statistics = new CrawlStatistics
        {
            TotalRecords = allRecords.Count,
            SuccessCount = allRecords.Count(d => d.CrawlStatus == CrawlStatus.Success),
            FailedCount = allRecords.Count(d => d.CrawlStatus == CrawlStatus.Failed),
            NotFoundCount = allRecords.Count(d => d.CrawlStatus == CrawlStatus.NotFound),
            ParseErrorCount = allRecords.Count(d => d.CrawlStatus == CrawlStatus.ParseError),
            TodayCrawled = allRecords.Count(d => d.CrawledAt.Date == today),
            WeekCrawled = allRecords.Count(d => d.CrawledAt.Date >= weekStart),
            MonthCrawled = allRecords.Count(d => d.CrawledAt.Date >= monthStart)
        };

        statistics.SuccessRate = statistics.TotalRecords > 0
            ? (double)statistics.SuccessCount / statistics.TotalRecords * 100
            : 0;

        sw.Stop();
        string message = $"GetCrawlStatisticsAsync 查询耗时: {sw.ElapsedMilliseconds}ms";
        logger.LogDebug(message);

        cache.Set(CACHE_KEY_STATISTICS, statistics, StatisticsCacheExpiration);
        return statistics;
    }

    public async Task<List<OnlineDrugInfo>> GetByStatusAsync(CrawlStatus status, int? limit = null)
    {
        var cacheKey = $"{CACHE_KEY_PREFIX}_ByStatus_{status}_{limit?.ToString() ?? "null"}";

        if (cache.TryGetValue<List<OnlineDrugInfo>>(cacheKey, out var cached))
        {
            if (cached != null) return cached;
        }

        await using var context = contextFactory.CreateDbContext();

        var query = (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .Where(d => d.CrawlStatus == status)
            .OrderByDescending(d => d.CrawledAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<OnlineDrugInfo>)query.Take(limit.Value);
        }

        var result = await query.ToListAsync();

        cache.Set(cacheKey, result, DefaultCacheExpiration);
        return result;
    }

    public async Task<List<OnlineDrugInfo>> GetRecentCrawledAsync(int count = 10)
    {
        var cacheKey = $"{CACHE_KEY_RECENT_PREFIX}_{count}";

        if (cache.TryGetValue<List<OnlineDrugInfo>>(cacheKey, out var cached))
        {
            if (cached != null) return cached;
        }

        await using var context = contextFactory.CreateDbContext();

        var result = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .AsNoTracking()
            .Where(d => d.CrawlStatus == CrawlStatus.Success)
            .OrderByDescending(d => d.CrawledAt)
            .Take(count)
            .ToListAsync();

        cache.Set(cacheKey, result, ShortCacheExpiration);
        return result;
    }

    public async Task<int> CleanupOldRecordsAsync(CrawlStatus status, DateTime olderThan)
    {
        await using var context = contextFactory.CreateDbContext();

        var oldRecords = await (context.OnlineDrugInfos ?? throw new InvalidOperationException())
            .Where(d => d.CrawlStatus == status && d.CrawledAt < olderThan)
            .ToListAsync();

        if (oldRecords.Count <= 0) return oldRecords.Count;
        context.OnlineDrugInfos.RemoveRange(oldRecords);
        await context.SaveChangesAsync();

        // 清除相关缓存
        InvalidateAllCaches();

        return oldRecords.Count;
    }

    #region 缓存管理方法

    /// <summary>
    /// 清除特定ID相关的缓存
    /// </summary>
    private void InvalidateRelatedCaches(int id)
    {
        // 清除特定ID的缓存
        cache.Remove($"{CACHE_KEY_PREFIX}_ById_{id}");
        cache.Remove($"{CACHE_KEY_PREFIX}_Exists_{id}");

        // 清除统计相关缓存
        InvalidateStatisticsCaches();

        // 清除列表相关缓存
        InvalidateListCaches();
    }

    /// <summary>
    /// 清除所有缓存
    /// </summary>
    private void InvalidateAllCaches()
    {
        // 清除所有主要缓存键
        cache.Remove(CACHE_KEY_ALL);
        cache.Remove(CACHE_KEY_STATISTICS);

        // 清除统计相关缓存
        InvalidateStatisticsCaches();

        // 清除列表相关缓存
        InvalidateListCaches();

        // 清除搜索相关缓存
        InvalidateSearchCaches();

        logger.LogDebug("已清除所有相关缓存");
    }

    /// <summary>
    /// 清除统计相关缓存
    /// </summary>
    private void InvalidateStatisticsCaches()
    {
        cache.Remove(CACHE_KEY_STATISTICS);

        // 清除各状态的计数缓存
        foreach (var status in Enum.GetValues<CrawlStatus>())
        {
            cache.Remove($"{CACHE_KEY_COUNT_PREFIX}_{status}");
        }

        cache.Remove($"{CACHE_KEY_PREFIX}_FailedIds");
    }

    /// <summary>
    /// 清除列表相关缓存
    /// </summary>
    private void InvalidateListCaches()
    {
        cache.Remove(CACHE_KEY_ALL);

        // 清除最近记录缓存
        for (var i = 1; i <= 50; i += 5) // 常用的数量
        {
            cache.Remove($"{CACHE_KEY_RECENT_PREFIX}_{i}");
        }

        // 清除状态相关缓存
        foreach (var status in Enum.GetValues<CrawlStatus>())
        {
            cache.Remove($"{CACHE_KEY_PREFIX}_ByStatus_{status}_null");
            for (var limit = 10; limit <= 100; limit += 10)
            {
                cache.Remove($"{CACHE_KEY_PREFIX}_ByStatus_{status}_{limit}");
            }
        }
    }

    /// <summary>
    /// 清除搜索相关缓存（这个方法需要更智能的缓存键管理）
    /// </summary>
    private void InvalidateSearchCaches()
    {
        // 注意：由于搜索缓存键包含动态关键词，这里只能清除已知的缓存
        // 实际项目中可能需要实现更复杂的缓存键管理机制
        logger.LogDebug("搜索缓存将在过期时间后自动失效");
    }

    #endregion
}