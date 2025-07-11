using System.ComponentModel.DataAnnotations;

namespace DrugSearcher.Models;

/// <summary>
/// 药物信息基类
/// </summary>
public abstract class BaseDrugInfo
{
    /// <summary>
    /// 药物ID
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 药物名称
    /// </summary>
    [Required]
    [MaxLength(200)]
    public abstract string DrugName { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    [MaxLength(200)]
    public abstract string? Specification { get; set; }

    /// <summary>
    /// 生产厂家/企业
    /// </summary>
    [MaxLength(200)]
    public abstract string? Manufacturer { get; set; }

    /// <summary>
    /// 批准文号
    /// </summary>
    [MaxLength(100)]
    public abstract string? ApprovalNumber { get; set; }

    /// <summary>
    /// 适应症
    /// </summary>
    public abstract string? Indications { get; set; }

    /// <summary>
    /// 用法用量
    /// </summary>
    public abstract string? Dosage { get; set; }

    /// <summary>
    /// 不良反应
    /// </summary>
    public abstract string? AdverseReactions { get; set; }

    /// <summary>
    /// 注意事项
    /// </summary>
    public abstract string? Precautions { get; set; }

    /// <summary>
    /// 数据来源
    /// </summary>
    public abstract DataSource DataSource { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 获取显示名称（用于UI显示）
    /// </summary>
    public virtual string DisplayName => DrugName;

    /// <summary>
    /// 获取完整描述（用于搜索匹配）
    /// </summary>
    public virtual string GetFullDescription()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(DrugName)) parts.Add(DrugName);
        if (!string.IsNullOrEmpty(Specification)) parts.Add(Specification);
        if (!string.IsNullOrEmpty(Manufacturer)) parts.Add(Manufacturer);
        if (!string.IsNullOrEmpty(ApprovalNumber)) parts.Add(ApprovalNumber);

        return string.Join(" ", parts);
    }

    /// <summary>
    /// 获取数据源显示文本
    /// </summary>
    public virtual string GetDataSourceText() => DataSource switch
    {
        DataSource.LocalDatabase => "本地数据库",
        DataSource.OnlineSearch => "在线数据",
        DataSource.CachedDocuments => "缓存数据",
        _ => "未知来源"
    };

    /// <summary>
    /// 获取数据源显示文本
    /// </summary>
    public virtual string DataSourceText => DataSource switch
    {
        DataSource.LocalDatabase => "本地数据库",
        DataSource.OnlineSearch => "在线数据",
        DataSource.CachedDocuments => "缓存数据",
        _ => "未知来源"
    };

    /// <summary>
    /// 获取数据源颜色
    /// </summary>
    public virtual string DataSourceColor => DataSource switch
    {
        DataSource.LocalDatabase => "#4CAF50",
        DataSource.OnlineSearch => "#2196F3",
        DataSource.CachedDocuments => "#FF9800",
        _ => "#9E9E9E"
    };
}