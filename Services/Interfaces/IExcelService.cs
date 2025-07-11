using DrugSearcher.Models;

namespace DrugSearcher.Services;

/// <summary>
/// Excel处理服务接口
/// </summary>
public interface IExcelService
{
    /// <summary>
    /// 从Excel文件读取药物数据
    /// </summary>
    Task<List<ExcelImportDto>> ReadFromExcelAsync(string filePath);

    /// <summary>
    /// 导出药物数据到Excel
    /// </summary>
    Task<bool> ExportToExcelAsync(List<DrugInfo> drugInfos, string filePath);

    /// <summary>
    /// 验证Excel文件格式
    /// </summary>
    Task<bool> ValidateExcelFormatAsync(string filePath);

    /// <summary>
    /// 获取Excel文件的详细验证信息
    /// </summary>
    Task<ExcelValidationResult> ValidateExcelDetailAsync(string filePath);
}