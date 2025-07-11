namespace DrugSearcher.Data;

/// <summary>
/// 药物数据库上下文工厂接口
/// </summary>
public interface IDrugDbContextFactory
{
    /// <summary>
    /// 创建药物数据库上下文实例
    /// </summary>
    DrugDbContext CreateDbContext();
}