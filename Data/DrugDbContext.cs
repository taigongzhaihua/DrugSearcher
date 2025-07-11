using DrugSearcher.Models;
using Microsoft.EntityFrameworkCore;

namespace DrugSearcher.Data;

/// <summary>
/// 药物数据库上下文 - 包含本地和在线药物数据
/// </summary>
public class DrugDbContext(DbContextOptions<DrugDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 本地药物信息表
    /// </summary>
    public DbSet<LocalDrugInfo> LocalDrugInfos { get; set; }

    /// <summary>
    /// 在线药物信息表
    /// </summary>
    public DbSet<OnlineDrugInfo> OnlineDrugInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置本地药物信息实体
        ConfigureLocalDrugInfo(modelBuilder);

        // 配置在线药物信息实体
        ConfigureOnlineDrugInfo(modelBuilder);
    }

    private static void ConfigureLocalDrugInfo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LocalDrugInfo>(entity =>
        {
            // 设置表名
            entity.ToTable("LocalDrugInfos");

            // 配置索引
            entity.HasIndex(e => e.DrugName)
                .HasDatabaseName("IX_LocalDrugInfos_DrugName");

            entity.HasIndex(e => e.Manufacturer)
                .HasDatabaseName("IX_LocalDrugInfos_Manufacturer");

            entity.HasIndex(e => e.ApprovalNumber)
                .HasDatabaseName("IX_LocalDrugInfos_ApprovalNumber");

            // 配置复合索引用于去重
            entity.HasIndex(e => new { e.DrugName, e.Specification, e.Manufacturer })
                .HasDatabaseName("IX_LocalDrugInfos_Unique")
                .IsUnique();

            // 配置自动更新时间
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now', 'localtime')");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("datetime('now', 'localtime')");
        });
    }

    private static void ConfigureOnlineDrugInfo(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OnlineDrugInfo>(entity =>
        {
            // 设置表名
            entity.ToTable("OnlineDrugInfos");

            // 配置索引
            entity.HasIndex(e => e.DrugName)
                .HasDatabaseName("IX_OnlineDrugInfos_DrugName");

            entity.HasIndex(e => e.Manufacturer)
                .HasDatabaseName("IX_OnlineDrugInfos_Manufacturer");

            entity.HasIndex(e => e.ApprovalNumber)
                .HasDatabaseName("IX_OnlineDrugInfos_ApprovalNumber");

            entity.HasIndex(e => e.CrawlStatus)
                .HasDatabaseName("IX_OnlineDrugInfos_CrawlStatus");

            entity.HasIndex(e => e.CrawledAt)
                .HasDatabaseName("IX_OnlineDrugInfos_CrawledAt");

            // 配置复合索引用于去重
            entity.HasIndex(e => new { e.DrugName, e.Specification, e.Manufacturer })
                .HasDatabaseName("IX_OnlineDrugInfos_Unique");

            // 配置自动更新时间
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now', 'localtime')");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("datetime('now', 'localtime')");

            entity.Property(e => e.CrawledAt)
                .HasDefaultValueSql("datetime('now', 'localtime')");
        });
    }

    /// <summary>
    /// 保存更改时自动更新UpdatedAt字段
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    /// <summary>
    /// 异步保存更改时自动更新UpdatedAt字段
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 更新时间戳
    /// </summary>
    private void UpdateTimestamps()
    {
        var entities = ChangeTracker.Entries()
            .Where(x => x.Entity is LocalDrugInfo or OnlineDrugInfo && 
                       x.State is EntityState.Added or EntityState.Modified);

        foreach (var entity in entities)
        {
            switch (entity.Entity)
            {
                case LocalDrugInfo localDrug:
                    if (entity.State == EntityState.Added)
                        localDrug.CreatedAt = DateTime.Now;
                    localDrug.UpdatedAt = DateTime.Now;
                    break;

                case OnlineDrugInfo onlineDrug:
                    if (entity.State == EntityState.Added)
                        onlineDrug.CreatedAt = DateTime.Now;
                    onlineDrug.UpdatedAt = DateTime.Now;
                    break;
            }
        }
    }
}