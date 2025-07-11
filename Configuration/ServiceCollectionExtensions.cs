using DrugSearcher.Models;
using DrugSearcher.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace DrugSearcher.Configuration;

/// <summary>
/// 依赖注入配置类
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加网络爬虫服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">爬虫配置</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddCrawlerServices(this IServiceCollection services, DrugCrawlerConfiguration? configuration = null)
    {
        // 添加默认配置
        var config = configuration ?? new DrugCrawlerConfiguration();
        services.AddSingleton(config);

        // 添加HttpClient服务
        services.AddHttpClient<YaoZsOnlineDrugService>(client =>
        {
            client.Timeout = TimeSpan.FromMilliseconds(config.RequestTimeoutMs);
            client.DefaultRequestHeaders.Add("User-Agent", config.UserAgent);
        });

        // 添加内存缓存
        services.AddMemoryCache();

        // 添加日志服务
        services.AddLogging(builder =>
        {
            if (config.EnableLogging)
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            }
        });

        // 注册爬虫服务
        services.AddScoped<IOnlineDrugService, YaoZsOnlineDrugService>();

        return services;
    }

    /// <summary>
    /// 添加带有自定义配置的网络爬虫服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configAction">配置操作</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddCrawlerServices(this IServiceCollection services, Action<DrugCrawlerConfiguration> configAction)
    {
        var config = new DrugCrawlerConfiguration();
        configAction(config);
        return services.AddCrawlerServices(config);
    }
}