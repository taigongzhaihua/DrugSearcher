using DrugSearcher.Models;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;
using System.IO;

namespace DrugSearcher.Services;

/// <summary>
/// Excel处理服务实现
/// </summary>
public class ExcelService : IExcelService
{
    /// <summary>
    /// 从Excel文件读取药物数据
    /// </summary>
    public async Task<List<ExcelImportDto>> ReadFromExcelAsync(string filePath)
    {
        var result = new List<ExcelImportDto>();

        await Task.Run(() =>
        {
            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            IWorkbook workbook = Path.GetExtension(filePath).ToLower() == ".xlsx"
                ? new XSSFWorkbook(fileStream)
                : new HSSFWorkbook(fileStream);

            var sheet = workbook.GetSheetAt(0);
            if (sheet == null) return;

            // 读取表头，确定列的位置
            var headerRow = sheet.GetRow(0);
            if (headerRow == null) return;

            var columnMapping = GetColumnMapping(headerRow);

            // 从第二行开始读取数据
            for (int rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
            {
                var row = sheet.GetRow(rowIndex);
                if (row == null) continue;

                var importDto = new ExcelImportDto
                {
                    DrugName = GetCellValue(row, columnMapping.GetValueOrDefault("药物名称", -1)),
                    Specification = GetCellValue(row, columnMapping.GetValueOrDefault("规格", -1)),
                    Manufacturer = GetCellValue(row, columnMapping.GetValueOrDefault("生产厂家", -1)),
                    Indications = GetCellValue(row, columnMapping.GetValueOrDefault("适应症", -1)),
                    Dosage = GetCellValue(row, columnMapping.GetValueOrDefault("用法用量", -1)),
                    TcmSyndrome = GetCellValue(row, columnMapping.GetValueOrDefault("中医证型", -1))
                };

                // 验证必填字段
                if (!string.IsNullOrWhiteSpace(importDto.DrugName))
                {
                    result.Add(importDto);
                }
            }
        });

        return result;
    }

    /// <summary>
    /// 导出药物数据到Excel
    /// </summary>
    public async Task<bool> ExportToExcelAsync(List<DrugInfo> drugInfos, string filePath)
    {
        try
        {
            await Task.Run(() =>
            {
                var workbook = new XSSFWorkbook();
                var sheet = workbook.CreateSheet("药物数据");

                // 创建表头
                var headerRow = sheet.CreateRow(0);
                var headers = new[] { "药物名称", "通用名称", "规格", "生产厂家", "批准文号", "适应症", "用法用量", "创建时间", "更新时间" };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = headerRow.CreateCell(i);
                    cell.SetCellValue(headers[i]);
                }

                // 填充数据
                for (int i = 0; i < drugInfos.Count; i++)
                {
                    var row = sheet.CreateRow(i + 1);
                    var drug = drugInfos[i];

                    row.CreateCell(0).SetCellValue(drug.DrugName);
                    row.CreateCell(1).SetCellValue(drug.GenericName ?? "");
                    row.CreateCell(2).SetCellValue(drug.Specification ?? "");
                    row.CreateCell(3).SetCellValue(drug.Manufacturer ?? "");
                    row.CreateCell(4).SetCellValue(drug.ApprovalNumber ?? "");
                    row.CreateCell(5).SetCellValue(drug.Indications ?? "");
                    row.CreateCell(6).SetCellValue(drug.Dosage ?? "");
                    row.CreateCell(7).SetCellValue(drug.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    row.CreateCell(8).SetCellValue(drug.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                }

                // 自动调整列宽
                for (int i = 0; i < headers.Length; i++)
                {
                    sheet.AutoSizeColumn(i);
                }

                // 保存文件
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                workbook.Write(fileStream);
            });

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 验证Excel文件格式
    /// </summary>
    public async Task<bool> ValidateExcelFormatAsync(string filePath)
    {
        try
        {
            return await Task.Run(() =>
            {
                if (!File.Exists(filePath))
                    return false;

                var extension = Path.GetExtension(filePath).ToLower();
                if (extension != ".xlsx" && extension != ".xls")
                    return false;

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                IWorkbook workbook = extension == ".xlsx"
                    ? new XSSFWorkbook(fileStream)
                    : new HSSFWorkbook(fileStream);

                var sheet = workbook.GetSheetAt(0);
                if (sheet == null || sheet.LastRowNum < 1)
                    return false;

                // 验证表头
                var headerRow = sheet.GetRow(0);
                if (headerRow == null)
                    return false;

                var requiredColumns = new[] { "药物名称" };
                var headerText = new List<string>();

                for (int i = 0; i < headerRow.LastCellNum; i++)
                {
                    var cell = headerRow.GetCell(i);
                    if (cell != null)
                    {
                        headerText.Add(cell.StringCellValue?.Trim() ?? "");
                    }
                }

                return requiredColumns.All(col => headerText.Contains(col));
            });
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取列映射
    /// </summary>
    private Dictionary<string, int> GetColumnMapping(IRow headerRow)
    {
        var mapping = new Dictionary<string, int>();

        for (int i = 0; i < headerRow.LastCellNum; i++)
        {
            var cell = headerRow.GetCell(i);
            if (cell != null)
            {
                var cellValue = cell.StringCellValue?.Trim() ?? "";
                if (!string.IsNullOrEmpty(cellValue))
                {
                    mapping[cellValue] = i;
                }
            }
        }

        return mapping;
    }

    /// <summary>
    /// 获取单元格值
    /// </summary>
    private string GetCellValue(IRow row, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= row.LastCellNum)
            return string.Empty;

        var cell = row.GetCell(columnIndex);
        if (cell == null)
            return string.Empty;

        return cell.CellType switch
        {
            CellType.String => cell.StringCellValue?.Trim() ?? "",
            CellType.Numeric => cell.NumericCellValue.ToString(CultureInfo.CurrentCulture),
            CellType.Boolean => cell.BooleanCellValue.ToString(),
            CellType.Formula => cell.StringCellValue?.Trim() ?? "",
            _ => string.Empty
        };
    }
}