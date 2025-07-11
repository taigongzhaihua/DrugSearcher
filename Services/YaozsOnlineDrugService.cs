using DrugSearcher.Enums;
using DrugSearcher.Models;
using DrugSearcher.Repositories;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Net.Http;

namespace DrugSearcher.Services;

/// <summary>
/// 药智数在线药物服务实现（使用仓储模式）
/// </summary>
public class YaozsOnlineDrugService : IOnlineDrugService
{
    private readonly HttpClient _httpClient;
    private readonly IOnlineDrugRepository _onlineDrugRepository;
    private readonly ILogger<YaozsOnlineDrugService> _logger;
    private const string BaseUrl = "https://www.yaozs.com/sms{0:D6}/";

    public YaozsOnlineDrugService(
        HttpClient httpClient,
        IOnlineDrugRepository onlineDrugRepository,
        ILogger<YaozsOnlineDrugService> logger)
    {
        _httpClient = httpClient;
        _onlineDrugRepository = onlineDrugRepository;
        _logger = logger;

        // 设置HTTP客户端
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<List<OnlineDrugInfo>> SearchOnlineDrugsAsync(string keyword)
    {
        try
        {
            return await _onlineDrugRepository.SearchAsync(keyword);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"搜索在线药物信息时发生错误，关键词: {keyword}");
            return [];
        }
    }

    public async Task<OnlineDrugInfo?> GetDrugDetailByIdAsync(int id)
    {
        try
        {
            // 先从数据库查找
            var existingDrug = await _onlineDrugRepository.GetByIdAsync(id);

            if (existingDrug is { CrawlStatus: CrawlStatus.Success })
            {
                return existingDrug;
            }

            // 如果数据库中没有或爬取失败，则重新爬取
            return await CrawlSingleDrugInfoAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"获取药物详情时发生错误，ID: {id}");
            return null;
        }
    }

    public async Task<OnlineDrugInfo?> CrawlSingleDrugInfoAsync(int id)
    {
        try
        {
            var url = string.Format(BaseUrl, id);
            _logger.LogInformation($"开始爬取药物信息，ID: {id}, URL: {url}");

            using var response = await _httpClient.GetAsync(url);

            var drugInfo = new OnlineDrugInfo
            {
                Id = id,
                SourceUrl = url,
                CrawledAt = DateTime.Now
            };

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"HTTP请求失败，状态码: {response.StatusCode}, URL: {url}");
                drugInfo.CrawlStatus = response.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? CrawlStatus.NotFound
                    : CrawlStatus.Failed;

                await _onlineDrugRepository.AddOrUpdateAsync(drugInfo);
                return drugInfo;
            }

            var html = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(html))
            {
                _logger.LogWarning($"页面内容为空，URL: {url}");
                drugInfo.CrawlStatus = CrawlStatus.Failed;
                await _onlineDrugRepository.AddOrUpdateAsync(drugInfo);
                return drugInfo;
            }

            // 解析HTML并提取数据
            var parsedDrug = ParseDrugInfoWithHtmlAgilityPack(html, id, url);
            if (parsedDrug.CrawlStatus == CrawlStatus.Success)
            {
                _logger.LogInformation($"成功解析药物信息: {parsedDrug.DrugName}");
            }
            else
            {
                _logger.LogWarning($"解析药物信息失败，ID: {id}, 状态: {parsedDrug.CrawlStatus}");
            }

            await _onlineDrugRepository.AddOrUpdateAsync(parsedDrug);
            return parsedDrug;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"爬取药物信息时发生错误，ID: {id}");

            // 保存失败记录
            var failedDrug = new OnlineDrugInfo
            {
                Id = id,
                SourceUrl = string.Format(BaseUrl, id),
                CrawlStatus = CrawlStatus.Failed,
                CrawledAt = DateTime.Now
            };
            await _onlineDrugRepository.AddOrUpdateAsync(failedDrug);
            return failedDrug;
        }
    }

    /// <summary>
    /// 使用HtmlAgilityPack解析药物信息
    /// </summary>
    private OnlineDrugInfo ParseDrugInfoWithHtmlAgilityPack(string html, int id, string url)
    {
        try
        {
            var drugInfo = new OnlineDrugInfo
            {
                Id = id,
                SourceUrl = url,
                CrawledAt = DateTime.Now,
                CrawlStatus = CrawlStatus.ParseError
            };

            // 加载HTML文档
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // 查找所有class为"row"的div元素
            var rowNodes = doc.DocumentNode.SelectNodes("//div[@class='row']");

            if (rowNodes == null || rowNodes.Count == 0)
            {
                _logger.LogWarning($"未找到任何 class='row' 的元素，ID: {id}");
                return drugInfo;
            }

            _logger.LogDebug($"找到 {rowNodes.Count} 个 row 元素，ID: {id}");

            // 提取所有key-value对
            var dataDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rowNode in rowNodes)
            {
                var keyValuePair = ExtractKeyValueFromRow(rowNode);
                if (keyValuePair.HasValue)
                {
                    var (key, value) = keyValuePair.Value;
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
                    {
                        dataDict[key] = value;
                        _logger.LogDebug($"提取到数据 - {key}: {value.Substring(0, Math.Min(50, value.Length))}...");
                    }
                }
            }

            if (dataDict.Count == 0)
            {
                _logger.LogWarning($"未能从row元素中提取到任何有效数据，ID: {id}");
                return drugInfo;
            }

            // 将提取的数据映射到药物信息对象
            MapDataToDrugInfo(dataDict, drugInfo);

            // 检查是否成功解析到基本信息
            if (!string.IsNullOrEmpty(drugInfo.DrugName) && drugInfo.DrugName != "未知药物")
            {
                drugInfo.CrawlStatus = CrawlStatus.Success;
                _logger.LogInformation($"成功解析药物: {drugInfo.DrugName}, ID: {id}");
            }
            else
            {
                drugInfo.CrawlStatus = CrawlStatus.ParseError;
                _logger.LogWarning($"未能解析出有效药物名称，ID: {id}");

                // 记录可用的键以便调试
                var availableKeys = string.Join(", ", dataDict.Keys);
                _logger.LogDebug($"可用的数据键 (ID: {id}): {availableKeys}");
            }

            return drugInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"解析药物信息时发生错误，ID: {id}");
            return new OnlineDrugInfo
            {
                Id = id,
                SourceUrl = url,
                CrawlStatus = CrawlStatus.ParseError,
                CrawledAt = DateTime.Now,
                DrugName = "解析失败"
            };
        }
    }

    /// <summary>
    /// 从单个row元素中提取key-value对
    /// </summary>
    private (string Key, string Value)? ExtractKeyValueFromRow(HtmlNode rowNode)
    {
        try
        {
            // 查找label和span元素
            var labelNode = rowNode.SelectSingleNode(".//label");
            var spanNode = rowNode.SelectSingleNode(".//span");

            if (labelNode == null || spanNode == null)
            {
                _logger.LogDebug($"Row元素缺少label或span: {rowNode.OuterHtml.Substring(0, Math.Min(100, rowNode.OuterHtml.Length))}");
                return null;
            }

            // 提取key（label的文本内容）
            var key = CleanText(labelNode.InnerText);

            // 提取value（span的HTML内容，需要处理<br>标签）
            var value = ProcessSpanContent(spanNode);

            // 清理key，移除末尾的冒号等
            key = key?.TrimEnd(':', '：', ' ', '\t', '\r', '\n');

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
            {
                return null;
            }

            return (key, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取key-value对时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 处理span内容，将<br>标签替换为换行符
    /// </summary>
    private string ProcessSpanContent(HtmlNode spanNode)
    {
        try
        {
            // 获取span的innerHTML
            var innerHTML = spanNode.InnerHtml;

            // 将<br>、<br/>、<br />标签替换为换行符
            innerHTML = System.Text.RegularExpressions.Regex.Replace(
                innerHTML,
                @"<br\s*/?>\s*",
                "\n",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            // 创建临时HTML节点来解码HTML实体
            var tempDoc = new HtmlAgilityPack.HtmlDocument();
            tempDoc.LoadHtml($"<div>{innerHTML}</div>");

            // 获取纯文本内容（会自动解码HTML实体）
            var text = tempDoc.DocumentNode.SelectSingleNode("//div")?.InnerText ?? "";

            // 清理多余的空白字符
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n\s*\n", "\n");

            return text.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理span内容时发生错误");
            // 降级到简单文本提取
            return CleanText(spanNode.InnerText);
        }
    }

    /// <summary>
    /// 清理文本内容
    /// </summary>
    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // 标准化空白字符
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

        return text.Trim();
    }

    /// <summary>
    /// 将提取的数据映射到药物信息对象
    /// </summary>
    private void MapDataToDrugInfo(Dictionary<string, string> dataDict, OnlineDrugInfo drugInfo)
    {
        // 药物名称的多种可能键名
        drugInfo.DrugName = GetValueByKeys(dataDict, "通用名称", "药品名称", "品名", "名称") ?? "未知药物";

        // 商品名称
        drugInfo.TradeName = GetValueByKeys(dataDict, "商品名称", "商品名");

        // 汉语拼音
        drugInfo.PinyinName = GetValueByKeys(dataDict, "汉语拼音", "拼音");

        // 批准文号
        drugInfo.ApprovalNumber = GetValueByKeys(dataDict, "批准文号", "文号", "国药准字");

        // 药品分类
        drugInfo.DrugCategory = GetValueByKeys(dataDict, "药品分类", "分类");

        // 生产企业
        drugInfo.Manufacturer = GetValueByKeys(dataDict, "生产企业", "生产厂家", "厂家", "生产单位");

        // 药品性质
        drugInfo.DrugType = GetValueByKeys(dataDict, "药品性质", "性质");

        // 相关疾病
        drugInfo.RelatedDiseases = GetValueByKeys(dataDict, "相关疾病", "疾病");

        // 性状
        drugInfo.Appearance = GetValueByKeys(dataDict, "性状");

        // 主要成份
        drugInfo.MainIngredients = GetValueByKeys(dataDict, "主要成份", "成份", "主要成分", "成分");

        // 规格
        drugInfo.Specification = GetValueByKeys(dataDict, "规格");

        // 适应症
        drugInfo.Indications = GetValueByKeys(dataDict, "适应症", "功能主治", "主治");

        // 不良反应
        drugInfo.AdverseReactions = GetValueByKeys(dataDict, "不良反应", "副作用");

        // 用法用量
        drugInfo.Dosage = GetValueByKeys(dataDict, "用法用量", "用法", "用量");

        // 禁忌
        drugInfo.Contraindications = GetValueByKeys(dataDict, "禁忌");

        // 注意事项
        drugInfo.Precautions = GetValueByKeys(dataDict, "注意事项", "注意");

        // 孕妇及哺乳期妇女用药
        drugInfo.PregnancyAndLactation = GetValueByKeys(dataDict, "孕妇及哺乳期妇女用药", "孕妇用药", "哺乳期用药");

        // 儿童用药
        drugInfo.PediatricUse = GetValueByKeys(dataDict, "儿童用药");

        // 老人用药
        drugInfo.GeriatricUse = GetValueByKeys(dataDict, "老人用药", "老年用药");

        // 药物相互作用
        drugInfo.DrugInteractions = GetValueByKeys(dataDict, "药物相互作用", "相互作用");

        // 药理毒理
        drugInfo.PharmacologyToxicology = GetValueByKeys(dataDict, "药理毒理", "药理作用", "毒理");

        // 药代动力学
        drugInfo.Pharmacokinetics = GetValueByKeys(dataDict, "药代动力学");

        // 贮藏
        drugInfo.Storage = GetValueByKeys(dataDict, "贮藏", "储藏", "保存");

        // 有效期
        drugInfo.ShelfLife = GetValueByKeys(dataDict, "有效期");
    }

    /// <summary>
    /// 根据多个可能的键名获取值
    /// </summary>
    private string? GetValueByKeys(Dictionary<string, string> dataDict, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (dataDict.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return null;
    }

    public async Task<CrawlResult> CrawlDrugInfosAsync(int startId, int endId, int batchSize = 10, int delayMs = 500, IProgress<CrawlProgress>? progress = null)
    {
        var result = new CrawlResult();
        var semaphore = new SemaphoreSlim(batchSize, batchSize);
        var tasks = new List<Task>();

        _logger.LogInformation($"开始批量爬取药物信息，范围: {startId}-{endId}, 批次大小: {batchSize}, 延迟: {delayMs}ms");

        for (int id = startId; id <= endId; id++)
        {
            var currentId = id;
            var task = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    // 检查数据库中是否已存在
                    var existing = await _onlineDrugRepository.GetByIdAsync(currentId);

                    OnlineDrugInfo? drugInfo;

                    if (existing == null)
                    {
                        // 如果不存在，则爬取
                        drugInfo = await CrawlSingleDrugInfoAsync(currentId);
                    }
                    else if (existing.CrawlStatus != CrawlStatus.Success)
                    {
                        // 如果之前爬取失败，重新尝试
                        drugInfo = await CrawlSingleDrugInfoAsync(currentId);
                    }
                    else
                    {
                        // 已存在且成功，跳过
                        drugInfo = existing;
                    }

                    lock (result)
                    {
                        result.TotalProcessed++;

                        if (drugInfo?.CrawlStatus == CrawlStatus.Success)
                        {
                            result.SuccessCount++;
                            result.CrawledDrugs.Add(drugInfo);
                        }
                        else
                        {
                            result.FailedCount++;
                            result.FailedIds.Add(currentId);
                        }

                        // 报告进度
                        var progressInfo = new CrawlProgress
                        {
                            TotalProcessed = result.TotalProcessed,
                            SuccessCount = result.SuccessCount,
                            FailedCount = result.FailedCount,
                            CurrentId = currentId,
                            ProgressPercentage = (double)result.TotalProcessed / (endId - startId + 1) * 100
                        };

                        progress?.Report(progressInfo);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            tasks.Add(task);

            // 控制请求频率
            if (tasks.Count >= batchSize)
            {
                await Task.WhenAll(tasks);
                tasks.Clear();
                await Task.Delay(delayMs); // 使用与测试程序相同的延迟
            }
        }

        // 等待剩余任务完成
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }

        result.EndTime = DateTime.Now;
        _logger.LogInformation($"爬取完成，总计: {result.TotalProcessed}, 成功: {result.SuccessCount}, 失败: {result.FailedCount}");

        return result;
    }

    // ... 其他方法保持不变
    public async Task<int> GetCrawledDrugCountAsync()
    {
        try
        {
            return await _onlineDrugRepository.GetSuccessCountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取已爬取药物数量时发生错误");
            return 0;
        }
    }

    public async Task<List<int>> GetFailedDrugIdsAsync()
    {
        try
        {
            return await _onlineDrugRepository.GetFailedDrugIdsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取失败药物ID列表时发生错误");
            return [];
        }
    }

    public async Task<CrawlResult> RetryCrawlFailedDrugsAsync(List<int> failedIds, IProgress<CrawlProgress>? progress = null)
    {
        _logger.LogInformation($"开始重新爬取失败的药物，数量: {failedIds.Count}");

        var result = new CrawlResult();

        for (int i = 0; i < failedIds.Count; i++)
        {
            var id = failedIds[i];
            var drugInfo = await CrawlSingleDrugInfoAsync(id);

            result.TotalProcessed++;

            if (drugInfo?.CrawlStatus == CrawlStatus.Success)
            {
                result.SuccessCount++;
                result.CrawledDrugs.Add(drugInfo);
            }
            else
            {
                result.FailedCount++;
                result.FailedIds.Add(id);
            }

            // 报告进度
            var progressInfo = new CrawlProgress
            {
                TotalProcessed = result.TotalProcessed,
                SuccessCount = result.SuccessCount,
                FailedCount = result.FailedCount,
                CurrentId = id,
                ProgressPercentage = (double)(i + 1) / failedIds.Count * 100
            };

            progress?.Report(progressInfo);

            // 使用相同的延迟
            await Task.Delay(500);
        }

        result.EndTime = DateTime.Now;
        _logger.LogInformation($"重新爬取完成，总计: {result.TotalProcessed}, 成功: {result.SuccessCount}, 失败: {result.FailedCount}");

        return result;
    }

    public async Task<CrawlStatistics> GetCrawlStatisticsAsync()
    {
        try
        {
            return await _onlineDrugRepository.GetCrawlStatisticsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取爬取统计信息时发生错误");
            return new CrawlStatistics();
        }
    }

    public async Task<int> CleanupOldFailedRecordsAsync(int olderThanDays = 30)
    {
        try
        {
            var cutoffDate = DateTime.Now.AddDays(-olderThanDays);

            // 清理失败记录
            var failedCleaned = await _onlineDrugRepository.CleanupOldRecordsAsync(CrawlStatus.Failed, cutoffDate);

            // 清理解析错误记录
            var parseErrorCleaned = await _onlineDrugRepository.CleanupOldRecordsAsync(CrawlStatus.ParseError, cutoffDate);

            var totalCleaned = failedCleaned + parseErrorCleaned;

            _logger.LogInformation($"清理完成，删除了 {totalCleaned} 条旧记录（失败: {failedCleaned}, 解析错误: {parseErrorCleaned}）");

            return totalCleaned;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理旧的失败记录时发生错误");
            return 0;
        }
    }

    public async Task<List<OnlineDrugInfo>> GetRecentCrawledDrugsAsync(int count = 10)
    {
        try
        {
            return await _onlineDrugRepository.GetRecentCrawledAsync(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最近爬取的药物时发生错误");
            return [];
        }
    }
}