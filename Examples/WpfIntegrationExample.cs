using DrugSearcher.Configuration;
using DrugSearcher.Models;
using DrugSearcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DrugSearcher.Examples;

/// <summary>
/// WPF应用程序集成示例
/// </summary>
public class WpfIntegrationExample
{
    /// <summary>
    /// 配置服务容器（在App.xaml.cs中调用）
    /// </summary>
    /// <returns>配置好的服务容器</returns>
    public static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // 添加爬虫服务
        services.AddCrawlerServices(config =>
        {
            // 生产环境配置
            config.RequestIntervalMs = 1000; // 1秒间隔
            config.MaxConcurrentRequests = 3; // 适中的并发数
            config.EnableLogging = true;
            config.EnableCache = true;
            config.CacheExpirationHours = 24; // 24小时缓存
            config.RespectRobotsTxt = true; // 遵守robots.txt
            config.RetryCount = 3; // 重试3次
            config.RetryIntervalMs = 2000; // 重试间隔2秒
            config.StartId = 1;
            config.EndId = 124051; // 完整范围
        });

        // 添加其他WPF应用程序需要的服务
        // services.AddSingleton<IMainWindowViewModel, MainWindowViewModel>();
        // services.AddSingleton<IDialogService, DialogService>();
        // 等等...

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 使用爬虫服务的示例
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    public static async Task<List<DrugInfo>> SearchDrugsExample(ServiceProvider serviceProvider)
    {
        var onlineDrugService = serviceProvider.GetRequiredService<IOnlineDrugService>();
        var logger = serviceProvider.GetRequiredService<ILogger<WpfIntegrationExample>>();

        try
        {
            logger.LogInformation("开始在线搜索药物...");

            // 方法1：通过关键词搜索
            var searchResults = await onlineDrugService.SearchOnlineDrugsAsync("对乙酰氨基酚");
            
            // 方法2：通过具体ID获取
            var specificDrug = await onlineDrugService.GetDrugDetailByExternalIdAsync("sms000001");
            
            // 方法3：批量获取
            var batchResults = await onlineDrugService.GetDrugsBatchAsync(1, 100);

            logger.LogInformation("搜索完成，共找到 {Count} 个药物", searchResults.Count + batchResults.Count);
            
            var allResults = new List<DrugInfo>();
            allResults.AddRange(searchResults);
            allResults.AddRange(batchResults);
            
            if (specificDrug != null)
            {
                allResults.Add(specificDrug);
            }

            return allResults;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "搜索药物时发生错误");
            return new List<DrugInfo>();
        }
    }

    /// <summary>
    /// 后台批量同步示例
    /// </summary>
    /// <param name="serviceProvider">服务提供者</param>
    /// <param name="progress">进度报告</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task<List<DrugInfo>> BatchSyncExample(
        ServiceProvider serviceProvider, 
        IProgress<string> progress,
        CancellationToken cancellationToken = default)
    {
        var onlineDrugService = serviceProvider.GetRequiredService<IOnlineDrugService>();
        var logger = serviceProvider.GetRequiredService<ILogger<WpfIntegrationExample>>();

        try
        {
            logger.LogInformation("开始批量同步药物数据...");
            progress.Report("开始批量同步药物数据...");

            var allDrugs = new List<DrugInfo>();
            const int batchSize = 100;
            const int maxId = 124051;

            for (int startId = 1; startId <= maxId; startId += batchSize)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var endId = Math.Min(startId + batchSize - 1, maxId);
                
                progress.Report($"正在同步 {startId} - {endId} 范围内的药物数据...");
                
                var batchResults = await onlineDrugService.GetDrugsBatchAsync(startId, endId, cancellationToken);
                allDrugs.AddRange(batchResults);

                var currentProgress = (double)endId / maxId * 100;
                progress.Report($"已完成 {currentProgress:F1}%，共获取 {allDrugs.Count} 个药物");

                logger.LogInformation("完成批次 {StartId}-{EndId}，累计获取 {Count} 个药物", startId, endId, allDrugs.Count);
            }

            progress.Report($"批量同步完成，共获取 {allDrugs.Count} 个药物");
            logger.LogInformation("批量同步完成，共获取 {Count} 个药物", allDrugs.Count);

            return allDrugs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "批量同步时发生错误");
            progress.Report($"批量同步失败: {ex.Message}");
            return new List<DrugInfo>();
        }
    }
}