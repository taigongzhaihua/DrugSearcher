using DrugSearcher.Models;
using Microsoft.EntityFrameworkCore;

namespace DrugSearcher.Data;

/// <summary>
/// 药物搜索数据库上下文
/// </summary>
public class DrugSearcherDbContext(DbContextOptions<DrugSearcherDbContext> options) : DbContext(options)
{
    /// <summary>
    /// 药物信息表
    /// </summary>
    public DbSet<DrugInfo> DrugInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置DrugInfo实体
        modelBuilder.Entity<DrugInfo>(entity =>
        {
            // 设置表名
            entity.ToTable("DrugInfos");

            // 配置索引
            entity.HasIndex(e => e.DrugName)
                .HasDatabaseName("IX_DrugInfos_DrugName");

            entity.HasIndex(e => e.Manufacturer)
                .HasDatabaseName("IX_DrugInfos_Manufacturer");

            entity.HasIndex(e => e.ApprovalNumber)
                .HasDatabaseName("IX_DrugInfos_ApprovalNumber");

            // 配置复合索引用于去重
            entity.HasIndex(e => new { e.DrugName, e.Specification, e.Manufacturer })
                .HasDatabaseName("IX_DrugInfos_Unique")
                .IsUnique();

            // 配置自动更新时间
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("datetime('now', 'localtime')");

            entity.Property(e => e.UpdatedAt)
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
            .Where(x => x is { Entity: DrugInfo, State: EntityState.Added or EntityState.Modified });

        foreach (var entity in entities)
        {
            var drugInfo = (DrugInfo)entity.Entity;

            if (entity.State == EntityState.Added)
            {
                drugInfo.CreatedAt = DateTime.Now;
            }

            drugInfo.UpdatedAt = DateTime.Now;
        }
    }
}