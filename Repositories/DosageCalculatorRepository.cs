using DrugSearcher.Data;
using DrugSearcher.Models;
using Microsoft.EntityFrameworkCore;

namespace DrugSearcher.Repositories;

/// <summary>
/// 剂量计算器仓储适配器
/// </summary>
public class DosageCalculatorRepository(IDrugDbContextFactory contextFactory) : IDosageCalculatorRepository
{
    public async Task<DosageCalculator?> GetByIdAsync(int id)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.DosageCalculators.FindAsync(id);
    }

    public async Task<List<DosageCalculator>> GetAllAsync()
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.DosageCalculators
            .Where(c => c.IsActive)
            .OrderBy(c => c.DrugName)
            .ThenBy(c => c.CalculatorName)
            .ToListAsync();
    }

    public async Task<List<DosageCalculator>> GetByDrugIdentifierAsync(string drugIdentifier)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.DosageCalculators
            .Where(c => c.DrugIdentifier == drugIdentifier && c.IsActive)
            .OrderBy(c => c.CalculatorName)
            .ToListAsync();
    }

    public async Task<List<DosageCalculator>> GetByDataSourceAndDrugIdAsync(DataSource dataSource, int drugId)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.DosageCalculators
            .Where(c => c.DataSource == dataSource && c.OriginalDrugId == drugId && c.IsActive)
            .OrderBy(c => c.CalculatorName)
            .ToListAsync();
    }

    public async Task<List<DosageCalculator>> GetByUnifiedDrugAsync(BaseDrugInfo drugInfo)
    {
        var drugId = drugInfo.Id;
        return await GetByDataSourceAndDrugIdAsync(drugInfo.DataSource, drugId);
    }

    public async Task<(List<DosageCalculator> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize)
    {
        await using var context = contextFactory.CreateDbContext();

        var query = context.DosageCalculators.Where(c => c.IsActive);
        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(c => c.DrugName)
            .ThenBy(c => c.CalculatorName)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<DosageCalculator>> SearchAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        await using var context = contextFactory.CreateDbContext();
        var keywordLower = keyword.Trim().ToLower();

        return await context.DosageCalculators
            .Where(c => c.IsActive &&
                       (EF.Functions.Like(c.DrugName.ToLower(), $"%{keywordLower}%") ||
                        EF.Functions.Like(c.CalculatorName.ToLower(), $"%{keywordLower}%") ||
                        EF.Functions.Like(c.Description.ToLower(), $"%{keywordLower}%")))
            .OrderBy(c => c.DrugName)
            .ThenBy(c => c.CalculatorName)
            .ToListAsync();
    }

    public async Task<bool> ExistsAsync(DataSource dataSource, int drugId, string calculatorName)
    {
        await using var context = contextFactory.CreateDbContext();
        return await context.DosageCalculators
            .AnyAsync(c => c.DataSource == dataSource &&
                          c.OriginalDrugId == drugId &&
                          c.CalculatorName == calculatorName &&
                          c.IsActive);
    }

    public async Task<DosageCalculator> AddAsync(DosageCalculator calculator)
    {
        await using var context = contextFactory.CreateDbContext();
        calculator.CreatedAt = DateTime.Now;
        calculator.UpdatedAt = DateTime.Now;
        context.DosageCalculators.Add(calculator);
        await context.SaveChangesAsync();
        return calculator;
    }

    public async Task<DosageCalculator> UpdateAsync(DosageCalculator calculator)
    {
        await using var context = contextFactory.CreateDbContext();
        calculator.UpdatedAt = DateTime.Now;
        context.DosageCalculators.Update(calculator);
        await context.SaveChangesAsync();
        return calculator;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await using var context = contextFactory.CreateDbContext();
        var calculator = await context.DosageCalculators.FindAsync(id);
        if (calculator == null) return false;

        // 软删除
        calculator.IsActive = false;
        calculator.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteRangeAsync(List<int> ids)
    {
        await using var context = contextFactory.CreateDbContext();
        var calculators = await context.DosageCalculators
            .Where(c => ids.Contains(c.Id))
            .ToListAsync();

        if (calculators.Count == 0) return false;

        foreach (var calculator in calculators)
        {
            calculator.IsActive = false;
            calculator.UpdatedAt = DateTime.Now;
        }

        await context.SaveChangesAsync();
        return true;
    }

    public async Task<DosageCalculatorStatistics> GetStatisticsAsync()
    {
        await using var context = contextFactory.CreateDbContext();

        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var statistics = new DosageCalculatorStatistics
        {
            TotalCalculators = await context.DosageCalculators.CountAsync(),
            ActiveCalculators = await context.DosageCalculators.CountAsync(c => c.IsActive),
            TodayCreated = await context.DosageCalculators.CountAsync(c => c.CreatedAt.Date == today),
            WeekCreated = await context.DosageCalculators.CountAsync(c => c.CreatedAt.Date >= weekStart),
            MonthCreated = await context.DosageCalculators.CountAsync(c => c.CreatedAt.Date >= monthStart)
        };

        // 统计各药物的计算器数量
        var calculatorsByDrug = await context.DosageCalculators
            .Where(c => c.IsActive)
            .GroupBy(c => c.DrugName)
            .Select(g => new { DrugName = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        statistics.CalculatorsByDrug = calculatorsByDrug
            .ToDictionary(x => x.DrugName, x => x.Count);

        return statistics;
    }
}