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
            // 创建文件的副本到临时目录，避免文件被占用的问题
            var tempFilePath = CreateTempCopyOfFile(filePath);

            try
            {
                using var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                IWorkbook workbook = Path.GetExtension(tempFilePath).ToLower() == ".xlsx"
                    ? new XSSFWorkbook(fileStream)
                    : new HSSFWorkbook(fileStream);

                var sheet = workbook.GetSheetAt(0);

                // 读取表头，确定列的位置
                var headerRow = sheet?.GetRow(0);
                if (headerRow == null) return;

                var columnMapping = GetColumnMapping(headerRow);

                // 从第二行开始读取数据
                for (var rowIndex = 1; rowIndex <= sheet?.LastRowNum; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    if (row == null) continue;

                    var importDto = new ExcelImportDto
                    {
                        DrugName = GetCellValue(row, columnMapping.GetValueOrDefault("药品名称", -1)),
                        Specification = GetCellValue(row, columnMapping.GetValueOrDefault("规格", -1)),
                        Dosage = GetCellValue(row, columnMapping.GetValueOrDefault("用法用量", -1)),
                        Indications = GetCellValue(row, columnMapping.GetValueOrDefault("适应症", -1)),
                        TcmDisease = GetCellValue(row, columnMapping.GetValueOrDefault("中医病名", -1)),
                        TcmSyndrome = GetCellValue(row, columnMapping.GetValueOrDefault("中医辨病辨证", -1)),
                        Remarks = GetCellValue(row, columnMapping.GetValueOrDefault("备注", -1)),
                        // 生产厂家在Excel中没有，设为空
                        Manufacturer = string.Empty
                    };

                    // 验证必填字段
                    if (!string.IsNullOrWhiteSpace(importDto.DrugName))
                    {
                        result.Add(importDto);
                    }
                }

                // 显式关闭工作簿
                workbook.Close();
            }
            finally
            {
                // 删除临时文件
                if (File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                    }
                    catch
                    {
                        // 忽略删除临时文件的错误
                    }
                }
            }
        });

        return result;
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

                // 创建文件的副本到临时目录
                var tempFilePath = CreateTempCopyOfFile(filePath);

                try
                {
                    using var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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

                    var headerText = new List<string>();

                    for (var i = 0; i < headerRow.LastCellNum; i++)
                    {
                        var cell = headerRow.GetCell(i);
                        if (cell != null)
                        {
                            headerText.Add(cell.StringCellValue?.Trim() ?? "");
                        }
                    }

                    // 只验证必需的列是否存在
                    var requiredColumns = new[] { "药品名称" };
                    var hasRequiredColumns = requiredColumns.All(col => headerText.Contains(col));

                    // 显式关闭工作簿
                    workbook.Close();

                    return hasRequiredColumns;
                }
                finally
                {
                    // 删除临时文件
                    if (File.Exists(tempFilePath))
                    {
                        try
                        {
                            File.Delete(tempFilePath);
                        }
                        catch
                        {
                            // 忽略删除临时文件的错误
                        }
                    }
                }
            });
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取Excel文件的详细验证信息
    /// </summary>
    public async Task<ExcelValidationResult> ValidateExcelDetailAsync(string filePath)
    {
        var result = new ExcelValidationResult();

        try
        {
            await Task.Run(() =>
            {
                if (!File.Exists(filePath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "文件不存在";
                    return;
                }

                var extension = Path.GetExtension(filePath).ToLower();
                if (extension != ".xlsx" && extension != ".xls")
                {
                    result.IsValid = false;
                    result.ErrorMessage = "文件格式不正确，请选择Excel文件(.xlsx或.xls)";
                    return;
                }

                // 创建文件的副本到临时目录
                var tempFilePath = CreateTempCopyOfFile(filePath);

                try
                {
                    using var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    IWorkbook workbook = extension == ".xlsx"
                        ? new XSSFWorkbook(fileStream)
                        : new HSSFWorkbook(fileStream);

                    var sheet = workbook.GetSheetAt(0);
                    if (sheet == null)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Excel文件中没有工作表";
                        return;
                    }

                    if (sheet.LastRowNum < 1)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Excel文件中没有数据";
                        return;
                    }

                    // 验证表头
                    var headerRow = sheet.GetRow(0);
                    if (headerRow == null)
                    {
                        result.IsValid = false;
                        result.ErrorMessage = "Excel文件中没有表头";
                        return;
                    }

                    var headerText = new List<string>();
                    for (var i = 0; i < headerRow.LastCellNum; i++)
                    {
                        var cell = headerRow.GetCell(i);
                        if (cell != null)
                        {
                            var cellValue = cell.StringCellValue?.Trim() ?? "";
                            if (!string.IsNullOrEmpty(cellValue))
                            {
                                headerText.Add(cellValue);
                            }
                        }
                    }

                    result.DetectedColumns = headerText;

                    // 检查必需的列
                    var requiredColumns = new[] { "药品名称" };
                    var missingRequired = requiredColumns.Where(col => !headerText.Contains(col)).ToList();
                    if (missingRequired.Any())
                    {
                        result.IsValid = false;
                        result.ErrorMessage = $"缺少必需的列：{string.Join(", ", missingRequired)}";
                        result.MissingRequiredColumns = missingRequired;
                        return;
                    }

                    // 检查可选的列
                    var optionalColumns = new[] { "规格", "用法用量", "适应症", "中医病名", "中医辨病辨证", "备注" };
                    var missingOptional = optionalColumns.Where(col => !headerText.Contains(col)).ToList();
                    result.MissingOptionalColumns = missingOptional;

                    // 统计数据行数
                    var dataRowCount = 0;
                    var drugNameColumnIndex = headerText.IndexOf("药品名称");

                    for (var rowIndex = 1; rowIndex <= sheet.LastRowNum; rowIndex++)
                    {
                        var row = sheet.GetRow(rowIndex);
                        if (row != null && drugNameColumnIndex >= 0)
                        {
                            var drugNameCell = row.GetCell(drugNameColumnIndex);
                            if (drugNameCell != null && !string.IsNullOrWhiteSpace(GetCellValueAsString(drugNameCell)))
                            {
                                dataRowCount++;
                            }
                        }
                    }

                    result.DataRowCount = dataRowCount;
                    result.IsValid = true;

                    if (missingOptional.Any())
                    {
                        result.WarningMessage = $"以下可选列未找到：{string.Join(", ", missingOptional)}，这些数据将为空";
                    }

                    // 显式关闭工作簿
                    workbook.Close();
                }
                finally
                {
                    // 删除临时文件
                    if (File.Exists(tempFilePath))
                    {
                        try
                        {
                            File.Delete(tempFilePath);
                        }
                        catch
                        {
                            // 忽略删除临时文件的错误
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.ErrorMessage = $"验证Excel文件时发生错误：{ex.Message}";
        }

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
                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 如果文件已存在，先尝试删除
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (IOException)
                    {
                        // 如果文件被占用，生成新的文件名
                        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                        var extension = Path.GetExtension(filePath);
                        var directoryPath = Path.GetDirectoryName(filePath);
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        filePath = Path.Combine(directoryPath ?? "", $"{fileNameWithoutExt}_{timestamp}{extension}");
                    }
                }

                var workbook = new XSSFWorkbook();
                var sheet = workbook.CreateSheet("药物数据");

                // 创建表头
                var headerRow = sheet.CreateRow(0);
                var headers = new[] { "药品名称", "通用名称", "规格", "生产厂家", "批准文号", "适应症", "用法用量", "中医病名", "中医辨病辨证", "备注", "创建时间", "更新时间" };

                for (var i = 0; i < headers.Length; i++)
                {
                    var cell = headerRow.CreateCell(i);
                    cell.SetCellValue(headers[i]);
                }

                // 填充数据
                for (var i = 0; i < drugInfos.Count; i++)
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
                    row.CreateCell(7).SetCellValue(drug.TcmDisease ?? "");
                    row.CreateCell(8).SetCellValue(drug.TcmSyndrome ?? "");
                    row.CreateCell(9).SetCellValue(drug.Remarks ?? "");
                    row.CreateCell(10).SetCellValue(drug.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    row.CreateCell(11).SetCellValue(drug.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                }

                // 自动调整列宽
                for (var i = 0; i < headers.Length; i++)
                {
                    sheet.AutoSizeColumn(i);
                }

                // 保存文件
                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                workbook.Write(fileStream);
                fileStream.Flush();

                // 显式关闭工作簿
                workbook.Close();
            });

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"导出Excel失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 创建文件的临时副本
    /// </summary>
    private static string CreateTempCopyOfFile(string originalFilePath)
    {
        var tempPath = Path.GetTempPath();
        var fileName = Path.GetFileName(originalFilePath);
        var timestamp = DateTime.Now.Ticks;
        var tempFileName = $"{timestamp}_{fileName}";
        var tempFilePath = Path.Combine(tempPath, tempFileName);

        // 使用FileShare.ReadWrite来读取可能被其他进程占用的文件
        using (var sourceStream = new FileStream(originalFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var destStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
        {
            sourceStream.CopyTo(destStream);
        }

        return tempFilePath;
    }

    /// <summary>
    /// 获取列映射
    /// </summary>
    private static Dictionary<string, int> GetColumnMapping(IRow headerRow)
    {
        var mapping = new Dictionary<string, int>();

        for (var i = 0; i < headerRow.LastCellNum; i++)
        {
            var cell = headerRow.GetCell(i);
            if (cell == null) continue;
            var cellValue = cell.StringCellValue?.Trim() ?? "";
            if (!string.IsNullOrEmpty(cellValue))
            {
                mapping[cellValue] = i;
            }
        }

        return mapping;
    }

    /// <summary>
    /// 获取单元格值
    /// </summary>
    private static string GetCellValue(IRow row, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= row.LastCellNum)
            return string.Empty;

        var cell = row.GetCell(columnIndex);
        if (cell == null)
            return string.Empty;

        return GetCellValueAsString(cell);
    }

    /// <summary>
    /// 将单元格值转换为字符串
    /// </summary>
    private static string GetCellValueAsString(ICell cell)
    {
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