using DrugSearcher.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DrugSearcher.Services;

public class DatabaseInitializationService(
    IDrugDbContextFactory drugDbContextFactory,
    IApplicationDbContextFactory applicationDbContextFactory,
    ILogger<DatabaseInitializationService> logger)
    : IDatabaseInitializationService
{
    public async Task InitializeAsync()
    {
        try
        {
            logger.LogInformation("开始初始化所有数据库...");

            // 初始化应用程序数据库
            await using var appContext = applicationDbContextFactory.CreateDbContext();
            await appContext.Database.EnsureCreatedAsync();
            logger.LogInformation("应用程序数据库初始化完成");

            // 初始化药物数据库（包含两个表）
            await using var drugContext = drugDbContextFactory.CreateDbContext();
            await drugContext.Database.EnsureCreatedAsync();

            // 验证表是否存在
            var localCount = await (drugContext.LocalDrugInfos ?? throw new InvalidOperationException()).CountAsync();
            var onlineCount = await (drugContext.OnlineDrugInfos ?? throw new InvalidOperationException()).CountAsync();
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation($"药物数据库初始化完成 - 本地药物: {localCount}条, 在线药物: {onlineCount}条");

                logger.LogInformation("所有数据库初始化完成");
            }
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError(ex, "数据库初始化失败");
            throw;
        }
    }

    public async Task<bool> CheckDatabaseStatusAsync()
    {
        try
        {
            await using var context = drugDbContextFactory.CreateDbContext();

            // 检查数据库连接
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect) return false;

            // 测试表访问
            await (context.LocalDrugInfos ?? throw new InvalidOperationException()).CountAsync();
            await (context.OnlineDrugInfos ?? throw new InvalidOperationException()).CountAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "数据库状态检查失败");
            return false;
        }
    }
}