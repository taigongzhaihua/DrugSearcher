using Microsoft.EntityFrameworkCore;

namespace DrugSearcher.Data;

/// <summary>
/// DrugSearcherDbContext 工厂实现
/// </summary>
public class DrugSearcherDbContextFactory(DbContextOptions<DrugSearcherDbContext> options)
    : IDrugSearcherDbContextFactory
{
    /// <summary>
    /// 创建 DrugSearcherDbContext 实例
    /// </summary>
    public DrugSearcherDbContext CreateDbContext()
    {
        return new DrugSearcherDbContext(options);
    }
}