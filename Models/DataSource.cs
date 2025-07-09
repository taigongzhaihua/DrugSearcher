namespace DrugSearcher.Models;

/// <summary>
/// 数据来源枚举
/// </summary>
public enum DataSource
{
    /// <summary>
    /// 本地数据库
    /// </summary>
    LocalDatabase,

    /// <summary>
    /// 在线搜索
    /// </summary>
    OnlineSearch,

    /// <summary>
    /// 缓存文档
    /// </summary>
    CachedDocuments
}