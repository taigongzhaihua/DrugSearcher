using System.ComponentModel.DataAnnotations;

namespace DrugSearcher.Models;

/// <summary>
/// 本地药物信息模型
/// </summary>
public class LocalDrugInfo : BaseDrugInfo
{
    /// <summary>
    /// 药物名称
    /// </summary>
    [Required]
    [StringLength(200)]
    public override string DrugName { get; set; } = string.Empty;

    /// <summary>
    /// 通用名称
    /// </summary>
    [StringLength(200)]
    public string? GenericName { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    [StringLength(100)]
    public override string? Specification { get; set; }

    /// <summary>
    /// 生产厂家
    /// </summary>
    [StringLength(200)]
    public override string? Manufacturer { get; set; }

    /// <summary>
    /// 批准文号
    /// </summary>
    [StringLength(100)]
    public override string? ApprovalNumber { get; set; }

    /// <summary>
    /// 适应症
    /// </summary>
    public override string? Indications { get; set; }

    /// <summary>
    /// 用法用量
    /// </summary>
    public override string? Dosage { get; set; }

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
    /// 不良反应（来自原始模型的SideEffects字段）
    /// </summary>
    public override string? AdverseReactions { get; set; }

    /// <summary>
    /// 注意事项
    /// </summary>
    public override string? Precautions { get; set; }

    /// <summary>
    /// 数据来源
    /// </summary>
    public override DataSource DataSource { get; set; } = DataSource.LocalDatabase;

    /// <summary>
    /// 获取完整描述（包含本地特有字段）
    /// </summary>
    public override string GetFullDescription()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(DrugName)) parts.Add(DrugName);
        if (!string.IsNullOrEmpty(GenericName)) parts.Add(GenericName);
        if (!string.IsNullOrEmpty(Specification)) parts.Add(Specification);
        if (!string.IsNullOrEmpty(Manufacturer)) parts.Add(Manufacturer);
        if (!string.IsNullOrEmpty(ApprovalNumber)) parts.Add(ApprovalNumber);
        if (!string.IsNullOrEmpty(TcmDisease)) parts.Add(TcmDisease);
        if (!string.IsNullOrEmpty(Description)) parts.Add(Description);

        return string.Join(" ", parts);
    }
}