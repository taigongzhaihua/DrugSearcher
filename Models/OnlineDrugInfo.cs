using DrugSearcher.Enums;
using System.ComponentModel.DataAnnotations;

namespace DrugSearcher.Models;

/// <summary>
/// 在线药物信息模型（从yaozs.com爬取的数据）
/// </summary>
public class OnlineDrugInfo : BaseDrugInfo
{
    /// <summary>
    /// 通用名称
    /// </summary>
    [Required]
    [MaxLength(200)]
    public override string DrugName { get; set; } = string.Empty;

    /// <summary>
    /// 商品名称
    /// </summary>
    [MaxLength(200)]
    public string? TradeName { get; set; }

    /// <summary>
    /// 汉语拼音
    /// </summary>
    [MaxLength(200)]
    public string? PinyinName { get; set; }

    /// <summary>
    /// 批准文号
    /// </summary>
    [MaxLength(100)]
    public override string? ApprovalNumber { get; set; }

    /// <summary>
    /// 药品分类
    /// </summary>
    [MaxLength(100)]
    public string? DrugCategory { get; set; }

    /// <summary>
    /// 生产企业
    /// </summary>
    [MaxLength(200)]
    public override string? Manufacturer { get; set; }

    /// <summary>
    /// 药品性质（处方药/非处方药）
    /// </summary>
    [MaxLength(50)]
    public string? DrugType { get; set; }

    /// <summary>
    /// 相关疾病
    /// </summary>
    [MaxLength(1000)]
    public string? RelatedDiseases { get; set; }

    /// <summary>
    /// 性状
    /// </summary>
    [MaxLength(500)]
    public string? Appearance { get; set; }

    /// <summary>
    /// 主要成份
    /// </summary>
    [MaxLength(500)]
    public string? MainIngredients { get; set; }

    /// <summary>
    /// 适应症
    /// </summary>
    [MaxLength(2000)]
    public override string? Indications { get; set; }

    /// <summary>
    /// 规格
    /// </summary>
    [MaxLength(200)]
    public override string? Specification { get; set; }

    /// <summary>
    /// 不良反应
    /// </summary>
    [MaxLength(2000)]
    public override string? AdverseReactions { get; set; }

    /// <summary>
    /// 用法用量
    /// </summary>
    [MaxLength(2000)]
    public override string? Dosage { get; set; }

    /// <summary>
    /// 禁忌
    /// </summary>
    [MaxLength(1000)]
    public string? Contraindications { get; set; }

    /// <summary>
    /// 注意事项
    /// </summary>
    [MaxLength(5000)]
    public override string? Precautions { get; set; }

    /// <summary>
    /// 孕妇及哺乳期妇女用药
    /// </summary>
    [MaxLength(2000)]
    public string? PregnancyAndLactation { get; set; }

    /// <summary>
    /// 儿童用药
    /// </summary>
    [MaxLength(1000)]
    public string? PediatricUse { get; set; }

    /// <summary>
    /// 老人用药
    /// </summary>
    [MaxLength(1000)]
    public string? GeriatricUse { get; set; }

    /// <summary>
    /// 药物相互作用
    /// </summary>
    [MaxLength(2000)]
    public string? DrugInteractions { get; set; }

    /// <summary>
    /// 药理毒理
    /// </summary>
    [MaxLength(2000)]
    public string? PharmacologyToxicology { get; set; }

    /// <summary>
    /// 药代动力学
    /// </summary>
    [MaxLength(2000)]
    public string? Pharmacokinetics { get; set; }

    /// <summary>
    /// 贮藏
    /// </summary>
    [MaxLength(200)]
    public string? Storage { get; set; }

    /// <summary>
    /// 有效期
    /// </summary>
    [MaxLength(100)]
    public string? ShelfLife { get; set; }

    /// <summary>
    /// 原始URL
    /// </summary>
    [MaxLength(500)]
    public string? SourceUrl { get; set; }

    /// <summary>
    /// 爬取状态
    /// </summary>
    public CrawlStatus CrawlStatus { get; set; } = CrawlStatus.Success;

    /// <summary>
    /// 爬取时间
    /// </summary>
    public DateTime CrawledAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 数据来源
    /// </summary>
    public override DataSource DataSource { get; set; } = DataSource.OnlineSearch;

    /// <summary>
    /// 获取显示名称（优先显示通用名称，其次商品名称）
    /// </summary>
    public override string DisplayName =>
        !string.IsNullOrEmpty(DrugName) ? DrugName :
        !string.IsNullOrEmpty(TradeName) ? TradeName :
        "未知药物";

    /// <summary>
    /// 获取完整描述（包含在线特有字段）
    /// </summary>
    public override string GetFullDescription()
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(DrugName)) parts.Add(DrugName);
        if (!string.IsNullOrEmpty(TradeName)) parts.Add(TradeName);
        if (!string.IsNullOrEmpty(PinyinName)) parts.Add(PinyinName);
        if (!string.IsNullOrEmpty(Specification)) parts.Add(Specification);
        if (!string.IsNullOrEmpty(Manufacturer)) parts.Add(Manufacturer);
        if (!string.IsNullOrEmpty(ApprovalNumber)) parts.Add(ApprovalNumber);
        if (!string.IsNullOrEmpty(DrugCategory)) parts.Add(DrugCategory);
        if (!string.IsNullOrEmpty(MainIngredients)) parts.Add(MainIngredients);

        return string.Join(" ", parts);
    }
}