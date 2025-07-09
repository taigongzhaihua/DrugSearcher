namespace DrugSearcher.Models;

/// <summary>
/// Excel导入数据传输对象
/// </summary>
public class ExcelImportDto
{
    /// <summary>
    /// 药物名称
    /// </summary>
    public string DrugName { get; set; } = string.Empty;

    /// <summary>
    /// 规格
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 生产厂家
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// 适应症
    /// </summary>
    public string? Indications { get; set; }

    /// <summary>
    /// 用法用量
    /// </summary>
    public string? Dosage { get; set; }

    /// <summary>
    /// 中医证型
    /// </summary>
    public string? TcmSyndrome { get; set; }

    /// <summary>
    /// 获取合并后的适应症（包含中医证型）
    /// </summary>
    public string GetMergedIndications()
    {
        var result = Indications ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(TcmSyndrome))
        {
            result += string.IsNullOrWhiteSpace(result) ?
                $"中医证型：{TcmSyndrome}" :
                $"\n\n中医证型：{TcmSyndrome}";
        }
        return result;
    }
}