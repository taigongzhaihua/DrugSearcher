using DrugSearcher.Models;
using DrugSearcher.Repositories;

namespace DrugSearcher.Services;

/// <summary>
/// 本地药物数据服务实现
/// </summary>
public class LocalDrugService(IDrugRepository drugRepository, IExcelService excelService) : ILocalDrugService
{
    /// <summary>
    /// 搜索药物
    /// </summary>
    public async Task<List<LocalDrugInfo>> SearchDrugsAsync(string keyword) => await drugRepository.SearchAsync(keyword);

    /// <summary>
    /// 获取药物详情
    /// </summary>
    public async Task<LocalDrugInfo?> GetDrugDetailAsync(int id) => await drugRepository.GetByIdAsync(id);

    /// <summary>
    /// 获取药物名称建议
    /// </summary>
    public async Task<List<string>> GetDrugNameSuggestionsAsync(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return [];

        var drugs = await drugRepository.SearchAsync(keyword);
        return [.. drugs.Select(d => d.DrugName)
            .Distinct()
            .Take(10)];
    }

    /// <summary>
    /// 添加药物
    /// </summary>
    public async Task<LocalDrugInfo> AddDrugAsync(LocalDrugInfo localDrugInfo)
    {
        // 检查是否已存在
        var exists = await drugRepository.ExistsAsync(
            localDrugInfo.DrugName,
            localDrugInfo.Specification,
            localDrugInfo.Manufacturer);

        if (exists)
        {
            throw new InvalidOperationException("药物已存在（名称、规格、厂家均相同）");
        }

        localDrugInfo.DataSource = DataSource.LocalDatabase;
        return await drugRepository.AddAsync(localDrugInfo);
    }

    /// <summary>
    /// 更新药物
    /// </summary>
    public async Task<LocalDrugInfo> UpdateDrugAsync(LocalDrugInfo localDrugInfo)
    {
        var existing = await drugRepository.GetByIdAsync(localDrugInfo.Id);
        return existing == null ? throw new InvalidOperationException("药物不存在") : await drugRepository.UpdateAsync(localDrugInfo);
    }

    /// <summary>
    /// 删除药物
    /// </summary>
    public async Task<bool> DeleteDrugAsync(int id) => await drugRepository.DeleteAsync(id);

    /// <summary>
    /// 批量删除药物
    /// </summary>
    public async Task<bool> DeleteDrugsAsync(List<int> ids) => await drugRepository.DeleteRangeAsync(ids);

    /// <summary>
    /// 从Excel导入药物数据
    /// </summary>
    /// <summary>
    /// 从Excel导入药物数据
    /// </summary>
    public async Task<ImportResult> ImportFromExcelAsync(string filePath)
    {
        var result = new ImportResult();

        try
        {
            // 验证文件格式
            if (!await excelService.ValidateExcelFormatAsync(filePath))
            {
                result.Success = false;
                result.Message = "Excel文件格式不正确，请确保包含\"药品名称\"列";
                return result;
            }

            // 读取Excel数据
            var importData = await excelService.ReadFromExcelAsync(filePath);
            result.TotalRecords = importData.Count;

            if (importData.Count == 0)
            {
                result.Success = false;
                result.Message = "Excel文件中没有有效数据";
                return result;
            }

            // 转换为DrugInfo对象并处理重复数据
            var drugInfos = new List<LocalDrugInfo>();
            var processedItems = new HashSet<string>(); // 用于跟踪已处理的项目

            foreach (var item in importData)
            {
                try
                {
                    // 创建唯一键用于去重（由于Excel中没有生产厂家，只用名称和规格）
                    var uniqueKey = $"{item.DrugName}|{item.Specification}";

                    // 检查是否已在当前批次中处理过
                    if (processedItems.Contains(uniqueKey))
                    {
                        result.DuplicateRecords++;
                        continue;
                    }

                    // 检查数据库中是否已存在（按名称和规格）
                    var exists = await drugRepository.ExistsAsync(
                        item.DrugName,
                        item.Specification,
                        null); // 生产厂家为null

                    if (exists)
                    {
                        result.DuplicateRecords++;
                        continue;
                    }

                    // 创建新的药物信息
                    var drugInfo = new LocalDrugInfo
                    {
                        DrugName = item.DrugName,
                        Specification = item.Specification,
                        Manufacturer = item.Manufacturer, // 会是空字符串
                        Indications = item.Indications,
                        Dosage = item.Dosage,
                        TcmDisease = item.TcmDisease,
                        TcmSyndrome = item.TcmSyndrome,
                        Remarks = item.Remarks,
                        DataSource = DataSource.LocalDatabase
                    };

                    drugInfos.Add(drugInfo);
                    processedItems.Add(uniqueKey);
                }
                catch (Exception ex)
                {
                    result.FailedRecords++;
                    result.ErrorDetails.Add($"处理记录 '{item.DrugName}' 时发生错误: {ex.Message}");
                }
            }

            // 批量保存到数据库
            if (drugInfos.Count > 0)
            {
                try
                {
                    await drugRepository.AddRangeAsync(drugInfos);
                    result.SuccessRecords = drugInfos.Count;
                }
                catch (Exception ex)
                {
                    result.FailedRecords += drugInfos.Count;
                    result.ErrorDetails.Add($"批量保存失败: {ex.Message}");
                }
            }

            // 设置结果
            result.Success = result.SuccessRecords > 0;
            result.Message = result.Success
                ? $"导入完成！成功：{result.SuccessRecords}条，重复：{result.DuplicateRecords}条，失败：{result.FailedRecords}条"
                : "导入失败，没有成功导入任何数据";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"导入过程中发生错误: {ex.Message}";
            result.ErrorDetails.Add(ex.ToString());
        }

        return result;
    }

    /// <summary>
    /// 获取Excel文件的详细验证信息
    /// </summary>
    public async Task<ExcelValidationResult> ValidateExcelDetailAsync(string filePath) => await excelService.ValidateExcelDetailAsync(filePath);

    /// <summary>
    /// 导出药物数据到Excel
    /// </summary>
    public async Task<bool> ExportToExcelAsync(string filePath)
    {
        try
        {
            var allDrugs = await drugRepository.GetAllAsync();
            return await excelService.ExportToExcelAsync(allDrugs, filePath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取所有药物（分页）
    /// </summary>
    public async Task<(List<LocalDrugInfo> Items, int TotalCount)> GetDrugsPagedAsync(int pageIndex, int pageSize) => await drugRepository.GetPagedAsync(pageIndex, pageSize);

    /// <summary>
    /// 获取药物统计信息
    /// </summary>
    public async Task<DrugStatistics> GetStatisticsAsync()
    {
        var allDrugs = await drugRepository.GetAllAsync();
        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        return new DrugStatistics
        {
            TotalDrugs = allDrugs.Count,
            TodayAdded = allDrugs.Count(d => d.CreatedAt.Date == today),
            WeekAdded = allDrugs.Count(d => d.CreatedAt.Date >= weekStart),
            MonthAdded = allDrugs.Count(d => d.CreatedAt.Date >= monthStart)
        };
    }
}