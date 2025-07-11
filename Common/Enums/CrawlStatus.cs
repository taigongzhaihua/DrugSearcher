namespace DrugSearcher.Enums;

/// <summary>
/// 爬取状态枚举
/// </summary>
public enum CrawlStatus
{
    /// <summary>
    /// 成功
    /// </summary>
    Success = 0,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 1,

    /// <summary>
    /// 页面不存在
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// 解析失败
    /// </summary>
    ParseError = 3
}