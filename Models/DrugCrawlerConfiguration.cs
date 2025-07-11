namespace DrugSearcher.Models;

/// <summary>
/// 药物爬虫配置类
/// </summary>
public class DrugCrawlerConfiguration
{
    /// <summary>
    /// 基础URL
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.yaozs.com/";

    /// <summary>
    /// 用户代理
    /// </summary>
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

    /// <summary>
    /// 请求间隔（毫秒）
    /// </summary>
    public int RequestIntervalMs { get; set; } = 1000;

    /// <summary>
    /// 请求超时时间（毫秒）
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// 重试次数
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// 重试间隔（毫秒）
    /// </summary>
    public int RetryIntervalMs { get; set; } = 2000;

    /// <summary>
    /// 是否启用缓存
    /// </summary>
    public bool EnableCache { get; set; } = true;

    /// <summary>
    /// 缓存过期时间（小时）
    /// </summary>
    public int CacheExpirationHours { get; set; } = 24;

    /// <summary>
    /// 最大并发请求数
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// 起始ID
    /// </summary>
    public int StartId { get; set; } = 1;

    /// <summary>
    /// 结束ID
    /// </summary>
    public int EndId { get; set; } = 124051;

    /// <summary>
    /// 是否启用日志
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// 是否遵循robots.txt
    /// </summary>
    public bool RespectRobotsTxt { get; set; } = true;
}