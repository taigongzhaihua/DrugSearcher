using System.ComponentModel.DataAnnotations;

namespace DrugSearcher.Models;

/// <summary>
/// 药物信息模型
/// </summary>
public class DrugInfo
{
    /// <summary>
    /// 主键ID
    /// </summary>
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
    /// 商品名称
    /// </summary>
    [StringLength(200)]
    public string? TradeName { get; set; }

    /// <summary>
    /// 汉语拼音
    /// </summary>
    [StringLength(200)]
    public string? Pinyin { get; set; }

    /// <summary>
    /// 规格
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
    /// 药品分类
    /// </summary>
    [StringLength(100)]
    public string? DrugCategory { get; set; }

    /// <summary>
    /// 药品性质
    /// </summary>
    [StringLength(100)]
    public string? DrugNature { get; set; }

    /// <summary>
    /// 相关疾病
    /// </summary>
    public string? RelatedDiseases { get; set; }

    /// <summary>
    /// 性状
    /// </summary>
    public string? Characteristics { get; set; }

    /// <summary>
    /// 主要成份
    /// </summary>
    public string? MainIngredients { get; set; }

    /// <summary>
    /// 适应症
    /// </summary>
    public string? Indications { get; set; }

    /// <summary>
    /// 用法用量
    /// </summary>
    public string? Dosage { get; set; }

    /// <summary>
    /// 不良反应
    /// </summary>
    public string? SideEffects { get; set; }

    /// <summary>
    /// 禁忌
    /// </summary>
    public string? Contraindications { get; set; }

    /// <summary>
    /// 注意事项
    /// </summary>
    public string? Precautions { get; set; }

    /// <summary>
    /// 药物相互作用
    /// </summary>
    public string? DrugInteractions { get; set; }

    /// <summary>
    /// 药理作用
    /// </summary>
    public string? PharmacologicalAction { get; set; }

    /// <summary>
    /// 贮藏
    /// </summary>
    [StringLength(200)]
    public string? Storage { get; set; }

    /// <summary>
    /// 包装
    /// </summary>
    [StringLength(200)]
    public string? Packaging { get; set; }

    /// <summary>
    /// 有效期
    /// </summary>
    [StringLength(100)]
    public string? ValidityPeriod { get; set; }

    /// <summary>
    /// 执行标准
    /// </summary>
    [StringLength(200)]
    public string? ExecutionStandard { get; set; }

    /// <summary>
    /// 中医病名
    /// </summary>
    [StringLength(500)]
    public string? TcmDisease { get; set; }

    /// <summary>
    /// 中医辨病辨证
    /// </summary>
    public string? TcmSyndrome { get; set; }

    /// <summary>
    /// 备注
    /// </summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// 药物说明
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 外部ID（来源网站的ID）
    /// </summary>
    [StringLength(100)]
    public string? ExternalId { get; set; }

    /// <summary>
    /// 外部URL（来源网站的URL）
    /// </summary>
    [StringLength(500)]
    public string? ExternalUrl { get; set; }

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
    /// 数据源颜色（用于UI显示）
    /// </summary>
    public string DataSourceColor => DataSource switch
    {
        DataSource.LocalDatabase => "#4CAF50",
        DataSource.OnlineSearch => "#2196F3",
        DataSource.CachedDocuments => "#FF9800",
        _ => "#9E9E9E"
    };
}