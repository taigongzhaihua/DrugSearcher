using DrugSearcher.Models;
using DrugSearcher.Repositories;

namespace DrugSearcher.Services
{
    /// <summary>
    /// 本地药物数据服务实现
    /// </summary>
    public class LocalDrugService(IDrugRepository drugRepository, IExcelService excelService) : ILocalDrugService
    {
        /// <summary>
        /// 搜索药物
        /// </summary>
        public async Task<List<DrugInfo>> SearchDrugsAsync(string keyword)
        {
            return await drugRepository.SearchAsync(keyword);
        }

        /// <summary>
        /// 获取药物详情
        /// </summary>
        public async Task<DrugInfo?> GetDrugDetailAsync(int id)
        {
            return await drugRepository.GetByIdAsync(id);
        }

        /// <summary>
        /// 获取药物名称建议
        /// </summary>
        public async Task<List<string>> GetDrugNameSuggestionsAsync(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<string>();

            var drugs = await drugRepository.SearchAsync(keyword);
            return drugs.Select(d => d.DrugName)
                       .Distinct()
                       .Take(10)
                       .ToList();
        }

        /// <summary>
        /// 添加药物
        /// </summary>
        public async Task<DrugInfo> AddDrugAsync(DrugInfo drugInfo)
        {
            // 检查是否已存在
            var exists = await drugRepository.ExistsAsync(
                drugInfo.DrugName,
                drugInfo.Specification,
                drugInfo.Manufacturer);

            if (exists)
            {
                throw new InvalidOperationException("药物已存在（名称、规格、厂家均相同）");
            }

            drugInfo.DataSource = DataSource.LocalDatabase;
            return await drugRepository.AddAsync(drugInfo);
        }

        /// <summary>
        /// 更新药物
        /// </summary>
        public async Task<DrugInfo> UpdateDrugAsync(DrugInfo drugInfo)
        {
            var existing = await drugRepository.GetByIdAsync(drugInfo.Id);
            if (existing == null)
            {
                throw new InvalidOperationException("药物不存在");
            }

            return await drugRepository.UpdateAsync(drugInfo);
        }

        /// <summary>
        /// 删除药物
        /// </summary>
        public async Task<bool> DeleteDrugAsync(int id)
        {
            return await drugRepository.DeleteAsync(id);
        }

        /// <summary>
        /// 批量删除药物
        /// </summary>
        public async Task<bool> DeleteDrugsAsync(List<int> ids)
        {
            return await drugRepository.DeleteRangeAsync(ids);
        }

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
                    result.Message = "Excel文件格式不正确";
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
                var drugInfos = new List<DrugInfo>();
                var processedItems = new HashSet<string>(); // 用于跟踪已处理的项目

                foreach (var item in importData)
                {
                    try
                    {
                        // 创建唯一键用于去重
                        var uniqueKey = $"{item.DrugName}|{item.Specification}|{item.Manufacturer}";

                        // 检查是否已在当前批次中处理过
                        if (processedItems.Contains(uniqueKey))
                        {
                            result.DuplicateRecords++;
                            continue;
                        }

                        // 检查数据库中是否已存在
                        var exists = await drugRepository.ExistsAsync(
                            item.DrugName,
                            item.Specification,
                            item.Manufacturer);

                        if (exists)
                        {
                            result.DuplicateRecords++;
                            continue;
                        }

                        // 创建新的药物信息
                        var drugInfo = new DrugInfo
                        {
                            DrugName = item.DrugName,
                            Specification = item.Specification,
                            Manufacturer = item.Manufacturer,
                            Indications = item.GetMergedIndications(),
                            Dosage = item.Dosage,
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
        public async Task<(List<DrugInfo> Items, int TotalCount)> GetDrugsPagedAsync(int pageIndex, int pageSize)
        {
            return await drugRepository.GetPagedAsync(pageIndex, pageSize);
        }

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
}