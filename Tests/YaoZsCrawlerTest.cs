using DrugSearcher.Configuration;
using DrugSearcher.Models;
using DrugSearcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DrugSearcher.Tests;

/// <summary>
/// 简单的测试程序，用于验证网络爬虫功能
/// </summary>
public class YaoZsCrawlerTest
{
    public static async Task Main(string[] args)
    {
        // 设置依赖注入
        var services = new ServiceCollection();
        
        // 添加爬虫服务
        services.AddCrawlerServices(config =>
        {
            config.RequestIntervalMs = 2000; // 2秒间隔，更加礼貌
            config.MaxConcurrentRequests = 2; // 降低并发数
            config.EnableLogging = true;
            config.StartId = 1;
            config.EndId = 10; // 测试前10个ID
        });
        
        var serviceProvider = services.BuildServiceProvider();
        
        // 获取服务
        var drugService = serviceProvider.GetRequiredService<IOnlineDrugService>();
        var logger = serviceProvider.GetRequiredService<ILogger<YaoZsCrawlerTest>>();
        
        try
        {
            logger.LogInformation("开始测试药智数据爬虫...");
            
            // 测试单个药物获取
            logger.LogInformation("测试获取单个药物信息...");
            var singleDrug = await drugService.GetDrugDetailByExternalIdAsync("sms000001");
            
            if (singleDrug != null)
            {
                logger.LogInformation("成功获取药物: {DrugName}", singleDrug.DrugName);
                logger.LogInformation("通用名称: {GenericName}", singleDrug.GenericName);
                logger.LogInformation("生产厂家: {Manufacturer}", singleDrug.Manufacturer);
                logger.LogInformation("适应症: {Indications}", singleDrug.Indications?.Substring(0, Math.Min(100, singleDrug.Indications?.Length ?? 0)));
            }
            else
            {
                logger.LogWarning("未能获取到药物信息");
            }
            
            // 测试批量获取
            logger.LogInformation("测试批量获取药物信息...");
            var batchResults = await drugService.GetDrugsBatchAsync(1, 5);
            
            logger.LogInformation("批量获取结果: {Count} 个药物", batchResults.Count);
            
            foreach (var drug in batchResults)
            {
                logger.LogInformation("- {DrugName} (ID: {ExternalId})", drug.DrugName, drug.ExternalId);
            }
            
            logger.LogInformation("测试完成!");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "测试过程中发生错误");
        }
        
        Console.WriteLine("按任意键退出...");
        Console.ReadKey();
    }
}