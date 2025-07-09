using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DrugSearcher.Models;

/// <summary>
/// 药物信息模型
/// </summary>
public class DrugInfo
{
    /// <summary>
    /// 主键ID
    /// </summary>
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// 药物名称
    /// </summary>
    [Required]
    [StringLength(200)]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>
    /// 通用名称
    /// </summary>
    [StringLength(200)]
    public string? GenericName { get; set; }

    /// <summary>
    /// 药物规格
    /// </summary>
    [StringLength(100)]
    public string? Specification { get; set; }

    /// <summary>
    /// 生产厂家
    /// </summary>
    [StringLength(200)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// 批准文号
    /// </summary>
    [StringLength(100)]
    public string? ApprovalNumber { get; set; }

    /// <summary>
    /// 适应症（包含中医证型）
    /// </summary>
    [Column(TypeName = "text")]
    public string? Indications { get; set; }

    /// <summary>
    /// 用法用量
    /// </summary>
    [Column(TypeName = "text")]
    public string? Dosage { get; set; }

    /// <summary>
    /// 药物说明
    /// </summary>
    [Column(TypeName = "text")]
    public string? Description { get; set; }

    /// <summary>
    /// 不良反应
    /// </summary>
    [Column(TypeName = "text")]
    public string? SideEffects { get; set; }

    /// <summary>
    /// 注意事项
    /// </summary>
    [Column(TypeName = "text")]
    public string? Precautions { get; set; }

    /// <summary>
    /// 数据来源
    /// </summary>
    public DataSource DataSource { get; set; } = DataSource.LocalDatabase;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 数据源显示颜色（用于UI显示）
    /// </summary>
    [NotMapped]
    public string DataSourceColor => DataSource switch
    {
        DataSource.LocalDatabase => "#4CAF50",
        DataSource.OnlineSearch => "#2196F3",
        DataSource.CachedDocuments => "#FF9800",
        _ => "#757575"
    };
}