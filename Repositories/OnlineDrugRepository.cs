using DrugSearcher.Data;
using DrugSearcher.Enums;
using DrugSearcher.Models;
using Microsoft.EntityFrameworkCore;

namespace DrugSearcher.Repositories;

/// <summary>
/// 在线药物仓储适配器 - 保持向后兼容
/// </summary>
public class OnlineDrugRepository(IDrugDbContextFactory contextFactory) : IOnlineDrugRepository
{
    public async Task<OnlineDrugInfo?> GetByIdAsync(int id)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.OnlineDrugInfos.FindAsync(id);
    }

    public async Task<List<OnlineDrugInfo>> GetAllAsync()
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.OnlineDrugInfos
            .OrderBy(d => d.DrugName)
            .ToListAsync();
    }

    public async Task<(List<OnlineDrugInfo> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize, CrawlStatus? status = null)
    {
        await using var context = contextFactory.CreateDbContext();

        var query = context.OnlineDrugInfos.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(d => d.CrawlStatus == status.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(d => d.DrugName)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<OnlineDrugInfo>> SearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        await using var context = contextFactory.CreateDbContext();
        var keywordLower = keyword.Trim().ToLower();

        return await context.OnlineDrugInfos
            .Where(d => d.CrawlStatus == CrawlStatus.Success &&
                       (EF.Functions.Like(d.DrugName.ToLower(), $"%{keywordLower}%") ||
                        (d.TradeName != null && EF.Functions.Like(d.TradeName.ToLower(), $"%{keywordLower}%")) ||
                        (d.Manufacturer != null && EF.Functions.Like(d.Manufacturer.ToLower(), $"%{keywordLower}%")) ||
                        (d.ApprovalNumber != null && EF.Functions.Like(d.ApprovalNumber.ToLower(), $"%{keywordLower}%")) ||
                        (d.Specification != null && EF.Functions.Like(d.Specification.ToLower(), $"%{keywordLower}%"))))
            .OrderBy(d => d.DrugName)
            .Take(50)
            .ToListAsync();
    }

    public async Task<List<string>> GetDrugNameSuggestionsAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        await using var context = contextFactory.CreateDbContext();
        var keywordLower = keyword.Trim().ToLower();

        return await context.OnlineDrugInfos
            .Where(d => d.CrawlStatus == CrawlStatus.Success &&
                       EF.Functions.Like(d.DrugName.ToLower(), $"%{keywordLower}%"))
            .Select(d => d.DrugName)
            .Distinct()
            .Take(10)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(int id)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.OnlineDrugInfos.AnyAsync(d => d.Id == id);
    }

    public async Task<OnlineDrugInfo> AddOrUpdateAsync(OnlineDrugInfo onlineDrugInfo)
    {
        await using var context = contextFactory.CreateDbContext();

        var existing = await context.OnlineDrugInfos.FindAsync(onlineDrugInfo.Id);

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
        return onlineDrugInfo;
    }

    public async Task<List<OnlineDrugInfo>> AddOrUpdateRangeAsync(List<OnlineDrugInfo> drugInfos)
    {
        await using var context = contextFactory.CreateDbContext();

        foreach (var drugInfo in drugInfos)
        {
            var existing = await context.OnlineDrugInfos.FindAsync(drugInfo.Id);

            if (existing == null)
            {
                context.OnlineDrugInfos.Add(drugInfo);
            }
            else
            {
                context.Entry(existing).CurrentValues.SetValues(drugInfo);
                existing.UpdatedAt = DateTime.Now;
            }
        }

        await context.SaveChangesAsync();
        return drugInfos;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = contextFactory.CreateDbContext();
        var drugInfo = await context.OnlineDrugInfos.FindAsync(id);
        if (drugInfo == null) return false;

        context.OnlineDrugInfos.Remove(drugInfo);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRangeAsync(List<int> ids)
    {
        await using var context = contextFactory.CreateDbContext();
        var drugInfos = await context.OnlineDrugInfos
            .Where(d => ids.Contains(d.Id))
            .ToListAsync();

        if (drugInfos.Count == 0) return false;

        context.OnlineDrugInfos.RemoveRange(drugInfos);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetCountByStatusAsync(CrawlStatus status)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.OnlineDrugInfos.CountAsync(d => d.CrawlStatus == status);
    }

    public async Task<List<int>> GetFailedDrugIdsAsync()
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.OnlineDrugInfos
            .Where(d => d.CrawlStatus != CrawlStatus.Success)
            .Select(d => d.Id)
            .ToListAsync();
    }

    public async Task<int> GetSuccessCountAsync() => await GetCountByStatusAsync(CrawlStatus.Success);

    public async Task<CrawlStatistics> GetCrawlStatisticsAsync()
    {
        await using var context = contextFactory.CreateDbContext();

        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var statistics = new CrawlStatistics
        {
            TotalRecords = await context.OnlineDrugInfos.CountAsync(),
            SuccessCount = await context.OnlineDrugInfos.CountAsync(d => d.CrawlStatus == CrawlStatus.Success),
            FailedCount = await context.OnlineDrugInfos.CountAsync(d => d.CrawlStatus == CrawlStatus.Failed),
            NotFoundCount = await context.OnlineDrugInfos.CountAsync(d => d.CrawlStatus == CrawlStatus.NotFound),
            ParseErrorCount = await context.OnlineDrugInfos.CountAsync(d => d.CrawlStatus == CrawlStatus.ParseError),
            TodayCrawled = await context.OnlineDrugInfos.CountAsync(d => d.CrawledAt.Date == today),
            WeekCrawled = await context.OnlineDrugInfos.CountAsync(d => d.CrawledAt.Date >= weekStart),
            MonthCrawled = await context.OnlineDrugInfos.CountAsync(d => d.CrawledAt.Date >= monthStart)
        };

        statistics.SuccessRate = statistics.TotalRecords > 0
            ? (double)statistics.SuccessCount / statistics.TotalRecords * 100
            : 0;

        return statistics;
    }

    public async Task<List<OnlineDrugInfo>> GetByStatusAsync(CrawlStatus status, int? limit = null)
    {
        await using var context = contextFactory.CreateDbContext();

        var query = context.OnlineDrugInfos
            .Where(d => d.CrawlStatus == status)
            .OrderByDescending(d => d.CrawledAt);

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<OnlineDrugInfo>)query.Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<List<OnlineDrugInfo>> GetRecentCrawledAsync(int count = 10)
    {
        await using var context = contextFactory.CreateDbContext();

        return await context.OnlineDrugInfos
            .Where(d => d.CrawlStatus == CrawlStatus.Success)
            .OrderByDescending(d => d.CrawledAt)
            .Take(count)
            .ToListAsync();
    }

    public async Task<int> CleanupOldRecordsAsync(CrawlStatus status, DateTime olderThan)
    {
        await using var context = contextFactory.CreateDbContext();

        var oldRecords = await context.OnlineDrugInfos
            .Where(d => d.CrawlStatus == status && d.CrawledAt < olderThan)
            .ToListAsync();

        if (oldRecords.Count > 0)
        {
            context.OnlineDrugInfos.RemoveRange(oldRecords);
            await context.SaveChangesAsync();
        }

        return oldRecords.Count;
    }
}