using Microsoft.EntityFrameworkCore;

namespace DrugSearcher.Data;

/// <summary>
/// 药物数据库上下文工厂实现
/// </summary>
public class DrugDbContextFactory(DbContextOptions<DrugDbContext> options) : IDrugDbContextFactory
{
    private static bool _databaseInitialized = false;
    private static readonly Lock Lock = new();

    /// <summary>
    /// 创建药物数据库上下文实例
    /// </summary>
    public DrugDbContext CreateDbContext()
    {
        var context = new DrugDbContext(options);

        // 确保数据库和表已创建
        lock (Lock)
        {
            if (_databaseInitialized) return context;
        }
        lock (Lock)
        {
            if (_databaseInitialized) return context;
            try
            {
                // 确保数据库存在
                context.Database.EnsureCreated();
                _databaseInitialized = true;

                System.Diagnostics.Debug.WriteLine("药物数据库初始化完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"药物数据库初始化失败: {ex.Message}");
            }
        }

        return context;
    }
}