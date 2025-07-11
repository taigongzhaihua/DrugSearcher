using System.ComponentModel.DataAnnotations;

namespace DrugSearcher.Models;

/// <summary>
/// Excel导入数据传输对象
/// </summary>
public class ExcelImportDto
{
    /// <summary>
    /// 药品名称
    /// </summary>
    [Required]
    public string DrugName { get; set; } = string.Empty;

    /// <summary>
    /// 规格
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 用法用量
    /// </summary>
    public string? Dosage { get; set; }

    /// <summary>
    /// 适应症
    /// </summary>
    public string? Indications { get; set; }

    /// <summary>
    /// 中医病名
    /// </summary>
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
    /// 生产厂家（Excel中没有此字段，设为空）
    /// </summary>
    public string? Manufacturer { get; set; } = string.Empty;

    /// <summary>
    /// 获取合并的适应症信息（包含中医相关信息）
    /// </summary>
    public string GetMergedIndications()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(Indications))
            parts.Add($"适应症：{Indications}");

        if (!string.IsNullOrWhiteSpace(TcmDisease))
            parts.Add($"中医病名：{TcmDisease}");

        if (!string.IsNullOrWhiteSpace(TcmSyndrome))
            parts.Add($"中医辨病辨证：{TcmSyndrome}");

        if (!string.IsNullOrWhiteSpace(Remarks))
            parts.Add($"备注：{Remarks}");

        return string.Join("；", parts);
    }
}