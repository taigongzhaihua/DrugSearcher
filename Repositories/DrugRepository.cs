using DrugSearcher.Data;
using DrugSearcher.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace DrugSearcher.Repositories;

/// <summary>
/// 本地药物仓储适配器 - 保持向后兼容
/// </summary>
public class DrugRepository(IDrugDbContextFactory contextFactory) : IDrugRepository
{
    public async Task<LocalDrugInfo?> GetByIdAsync(int id)
    {
        await using var context = contextFactory.CreateDbContext();
        Debug.Assert(context.LocalDrugInfos != null);
        return await context.LocalDrugInfos.FindAsync(id);
    }

    public async Task<List<LocalDrugInfo>> GetAllAsync()
    {
        await using var context = contextFactory.CreateDbContext();
        Debug.Assert(context.LocalDrugInfos != null);
        return await context.LocalDrugInfos
            .OrderBy(d => d.DrugName)
            .ToListAsync();
    }

    public async Task<(List<LocalDrugInfo> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize)
    {
        await using var context = contextFactory.CreateDbContext();

        if (context.LocalDrugInfos == null) return (null, 0);
        var totalCount = await context.LocalDrugInfos.CountAsync();
        var items = await context.LocalDrugInfos
            .OrderBy(d => d.DrugName)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);

    }

    public async Task<List<LocalDrugInfo>> SearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        await using var context = contextFactory.CreateDbContext();
        var keywordLower = keyword.Trim().ToLower();

        return await (context.LocalDrugInfos ?? throw new InvalidOperationException())
            .Where(d =>
                EF.Functions.Like(d.DrugName.ToLower(), $"%{keywordLower}%") ||
                (d.GenericName != null && EF.Functions.Like(d.GenericName.ToLower(), $"%{keywordLower}%")) ||
                (d.ApprovalNumber != null && EF.Functions.Like(d.ApprovalNumber.ToLower(), $"%{keywordLower}%")) ||
                (d.Manufacturer != null && EF.Functions.Like(d.Manufacturer.ToLower(), $"%{keywordLower}%")) ||
                (d.Specification != null && EF.Functions.Like(d.Specification.ToLower(), $"%{keywordLower}%")))
            .OrderBy(d => d.DrugName)
            .ToListAsync();
    }

    public async Task<List<string>> GetDrugNameSuggestionsAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        await using var context = contextFactory.CreateDbContext();
        var keywordLower = keyword.Trim().ToLower();

        return await (context.LocalDrugInfos ?? throw new InvalidOperationException())
            .Where(d => EF.Functions.Like(d.DrugName.ToLower(), $"%{keywordLower}%"))
            .Select(d => d.DrugName)
            .Distinct()
            .Take(10)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(string drugName, string? specification, string? manufacturer)
    {
        await using var context = contextFactory.CreateDbContext();

        return await (context.LocalDrugInfos ?? throw new InvalidOperationException())
            .AnyAsync(d => d.DrugName == drugName &&
                           d.Specification == specification &&
                           d.Manufacturer == manufacturer);
    }

    public async Task<LocalDrugInfo> AddAsync(LocalDrugInfo localDrugInfo)
    {
        await using var context = contextFactory.CreateDbContext();
        (context.LocalDrugInfos ?? throw new InvalidOperationException()).Add(localDrugInfo);
        await context.SaveChangesAsync();
        return localDrugInfo;
    }

    public async Task<List<LocalDrugInfo>> AddRangeAsync(List<LocalDrugInfo> drugInfos)
    {
        await using var context = contextFactory.CreateDbContext();
        (context.LocalDrugInfos ?? throw new InvalidOperationException()).AddRange(drugInfos);
        await context.SaveChangesAsync();
        return drugInfos;
    }

    public async Task<LocalDrugInfo> UpdateAsync(LocalDrugInfo localDrugInfo)
    {
        await using var context = contextFactory.CreateDbContext();
        (context.LocalDrugInfos ?? throw new InvalidOperationException()).Update(localDrugInfo);
        await context.SaveChangesAsync();
        return localDrugInfo;
    }

    public async Task<List<LocalDrugInfo>> UpdateRangeAsync(List<LocalDrugInfo> drugInfos)
    {
        await using var context = contextFactory.CreateDbContext();
        (context.LocalDrugInfos ?? throw new InvalidOperationException()).UpdateRange(drugInfos);
        await context.SaveChangesAsync();
        return drugInfos;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = contextFactory.CreateDbContext();
        Debug.Assert(context.LocalDrugInfos != null);
        var drugInfo = await context.LocalDrugInfos.FindAsync(id);
        if (drugInfo == null) return false;

        context.LocalDrugInfos.Remove(drugInfo);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRangeAsync(List<int> ids)
    {
        await using var context = contextFactory.CreateDbContext();
        var drugInfos = await (context.LocalDrugInfos ?? throw new InvalidOperationException())
            .Where(d => ids.Contains(d.Id))
            .ToListAsync();

        if (drugInfos.Count == 0) return false;

        context.LocalDrugInfos.RemoveRange(drugInfos);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<List<LocalDrugInfo>> GetDuplicatesAsync(List<ExcelImportDto> importData)
    {
        await using var context = contextFactory.CreateDbContext();
        var duplicates = new List<LocalDrugInfo>();

        foreach (var item in importData)
        {
            var existing = await (context.LocalDrugInfos ?? throw new InvalidOperationException())
                .FirstOrDefaultAsync(d => d.DrugName == item.DrugName &&
                                          d.Specification == item.Specification &&
                                          d.Manufacturer == item.Manufacturer);

            if (existing != null)
            {
                duplicates.Add(existing);
            }
        }

        return duplicates;
    }
}