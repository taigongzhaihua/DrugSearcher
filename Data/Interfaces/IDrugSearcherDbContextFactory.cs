namespace DrugSearcher.Data;

/// <summary>
/// DrugSearcherDbContext 工厂接口
/// </summary>
public interface IDrugSearcherDbContextFactory
{
    /// <summary>
    /// 创建 DrugSearcherDbContext 实例
    /// </summary>
    DrugSearcherDbContext CreateDbContext();
}