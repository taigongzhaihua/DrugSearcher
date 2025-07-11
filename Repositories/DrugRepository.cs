using DrugSearcher.Data.Interfaces;
using DrugSearcher.Models;
using Microsoft.EntityFrameworkCore;

namespace DrugSearcher.Repositories;

/// <summary>
/// 药物仓储实现（使用工厂模式）
/// </summary>
public class DrugRepository(IDrugSearcherDbContextFactory contextFactory) : IDrugRepository
{
    /// <summary>
    /// 根据ID获取药物信息
    /// </summary>
    public async Task<DrugInfo?> GetByIdAsync(int id)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.DrugInfos.FindAsync(id);
    }

    /// <summary>
    /// 获取所有药物信息
    /// </summary>
    public async Task<List<DrugInfo>> GetAllAsync()
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.DrugInfos
            .OrderBy(d => d.DrugName)
            .ToListAsync();
    }

    /// <summary>
    /// 分页获取药物信息
    /// </summary>
    public async Task<(List<DrugInfo> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize)
    {
        await using var context = contextFactory.CreateDbContext();

        var totalCount = await context.DrugInfos.CountAsync();
        var items = await context.DrugInfos
            .OrderBy(d => d.DrugName)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// 根据关键词搜索药物
    /// </summary>
    public async Task<List<DrugInfo>> SearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        await using var context = contextFactory.CreateDbContext();

        // 将关键词转换为小写，用于不区分大小写的搜索
        var keywordLower = keyword.Trim().ToLower();

        return await context.DrugInfos
            .Where(d =>
                // 使用 EF.Functions.Like 进行模糊匹配，支持不区分大小写
                EF.Functions.Like(d.DrugName.ToLower(), $"%{keywordLower}%") ||
                (d.GenericName != null && EF.Functions.Like(d.GenericName.ToLower(), $"%{keywordLower}%")) ||
                (d.ApprovalNumber != null && EF.Functions.Like(d.ApprovalNumber.ToLower(), $"%{keywordLower}%")) ||
                (d.Manufacturer != null && EF.Functions.Like(d.Manufacturer.ToLower(), $"%{keywordLower}%")) ||
                (d.Specification != null && EF.Functions.Like(d.Specification.ToLower(), $"%{keywordLower}%"))
            )
            .OrderBy(d => d.DrugName)
            .ToListAsync();
    }

    /// <summary>
    /// 获取药物名称建议
    /// </summary>
    public async Task<List<string>> GetDrugNameSuggestionsAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        await using var context = contextFactory.CreateDbContext();

        var keywordLower = keyword.Trim().ToLower();

        return await context.DrugInfos
            .Where(d => EF.Functions.Like(d.DrugName.ToLower(), $"%{keywordLower}%"))
            .Select(d => d.DrugName)
            .Distinct()
            .Take(10)
            .ToListAsync();
    }

    /// <summary>
    /// 检查药物是否存在（根据名称、规格、厂家）
    /// </summary>
    public async Task<bool> ExistsAsync(string drugName, string? specification, string? manufacturer)
    {
        await using var context = contextFactory.CreateDbContext();

        return await context.DrugInfos
            .AnyAsync(d => d.DrugName == drugName &&
                           d.Specification == specification &&
                           d.Manufacturer == manufacturer);
    }

    /// <summary>
    /// 添加药物信息
    /// </summary>
    public async Task<DrugInfo> AddAsync(DrugInfo drugInfo)
    {
        await using var context = contextFactory.CreateDbContext();

        context.DrugInfos.Add(drugInfo);
        await context.SaveChangesAsync();
        return drugInfo;
    }

    /// <summary>
    /// 批量添加药物信息
    /// </summary>
    public async Task<List<DrugInfo>> AddRangeAsync(List<DrugInfo> drugInfos)
    {
        await using var context = contextFactory.CreateDbContext();

        context.DrugInfos.AddRange(drugInfos);
        await context.SaveChangesAsync();
        return drugInfos;
    }

    /// <summary>
    /// 更新药物信息
    /// </summary>
    public async Task<DrugInfo> UpdateAsync(DrugInfo drugInfo)
    {
        await using var context = contextFactory.CreateDbContext();

        context.DrugInfos.Update(drugInfo);
        await context.SaveChangesAsync();
        return drugInfo;
    }

    /// <summary>
    /// 批量更新药物信息
    /// </summary>
    public async Task<List<DrugInfo>> UpdateRangeAsync(List<DrugInfo> drugInfos)
    {
        await using var context = contextFactory.CreateDbContext();

        context.DrugInfos.UpdateRange(drugInfos);
        await context.SaveChangesAsync();
        return drugInfos;
    }

    /// <summary>
    /// 删除药物信息
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = contextFactory.CreateDbContext();

        var drugInfo = await context.DrugInfos.FindAsync(id);
        if (drugInfo == null)
            return false;

        context.DrugInfos.Remove(drugInfo);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 批量删除药物信息
    /// </summary>
    public async Task<bool> DeleteRangeAsync(List<int> ids)
    {
        await using var context = contextFactory.CreateDbContext();

        var drugInfos = await context.DrugInfos
            .Where(d => ids.Contains(d.Id))
            .ToListAsync();

        if (drugInfos.Count == 0)
            return false;

        context.DrugInfos.RemoveRange(drugInfos);
        await context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 获取重复的药物记录
    /// </summary>
    public async Task<List<DrugInfo>> GetDuplicatesAsync(List<ExcelImportDto> importData)
    {
        await using var context = contextFactory.CreateDbContext();

        var duplicates = new List<DrugInfo>();

        foreach (var item in importData)
        {
            var existing = await context.DrugInfos
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