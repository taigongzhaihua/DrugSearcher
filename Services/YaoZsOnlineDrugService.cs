using DrugSearcher.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.RegularExpressions;

namespace DrugSearcher.Services;

/// <summary>
/// 药智数据网站爬虫服务实现
/// </summary>
public class YaoZsOnlineDrugService : IOnlineDrugService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<YaoZsOnlineDrugService> _logger;
    private readonly DrugCrawlerConfiguration _config;
    private readonly SemaphoreSlim _semaphore;
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly object _lockObject = new();

    public YaoZsOnlineDrugService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<YaoZsOnlineDrugService> logger,
        DrugCrawlerConfiguration config)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        _config = config;
        _semaphore = new SemaphoreSlim(_config.MaxConcurrentRequests);

        // 配置HttpClient
        _httpClient.Timeout = TimeSpan.FromMilliseconds(_config.RequestTimeoutMs);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", _config.UserAgent);
    }

    /// <summary>
    /// 在线搜索药物
    /// </summary>
    public async Task<List<DrugInfo>> SearchOnlineDrugsAsync(string keyword)
    {
        try
        {
            _logger.LogInformation("开始搜索药物: {Keyword}", keyword);
            
            // 对于yaozs.com，我们需要通过ID范围进行搜索
            // 这里实现一个简单的匹配逻辑
            var results = new List<DrugInfo>();
            
            // 从缓存中搜索或进行部分范围搜索
            for (int i = _config.StartId; i <= Math.Min(_config.StartId + 100, _config.EndId); i++)
            {
                var drug = await GetDrugDetailByExternalIdAsync($"sms{i:D6}");
                if (drug != null && 
                    (drug.DrugName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                     drug.GenericName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true))
                {
                    results.Add(drug);
                }
                
                if (results.Count >= 20) break; // 限制搜索结果数量
            }

            _logger.LogInformation("搜索完成，找到 {Count} 个结果", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "搜索药物时发生错误: {Keyword}", keyword);
            return new List<DrugInfo>();
        }
    }

    /// <summary>
    /// 根据ID获取在线药物详情
    /// </summary>
    public async Task<DrugInfo?> GetDrugDetailByIdAsync(int id)
    {
        return await GetDrugDetailByExternalIdAsync($"sms{id:D6}");
    }

    /// <summary>
    /// 根据外部ID获取药物详情
    /// </summary>
    public async Task<DrugInfo?> GetDrugDetailByExternalIdAsync(string externalId)
    {
        try
        {
            // 检查缓存
            if (_config.EnableCache && _cache.TryGetValue($"drug_{externalId}", out DrugInfo? cachedDrug))
            {
                _logger.LogDebug("从缓存获取药物信息: {ExternalId}", externalId);
                return cachedDrug;
            }

            await _semaphore.WaitAsync();
            try
            {
                // 控制请求频率
                await ThrottleRequest();

                var url = $"{_config.BaseUrl}{externalId}/";
                _logger.LogDebug("请求药物详情: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogDebug("药物不存在: {ExternalId}", externalId);
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("获取到空内容: {ExternalId}", externalId);
                    return null;
                }

                var drug = ParseDrugInfo(content, externalId, url);
                
                // 缓存结果
                if (_config.EnableCache && drug != null)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_config.CacheExpirationHours)
                    };
                    _cache.Set($"drug_{externalId}", drug, cacheOptions);
                }

                _logger.LogDebug("成功获取药物信息: {DrugName}", drug?.DrugName);
                return drug;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "网络请求失败: {ExternalId}", externalId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取药物详情时发生错误: {ExternalId}", externalId);
            return null;
        }
    }

    /// <summary>
    /// 批量获取药物信息
    /// </summary>
    public async Task<List<DrugInfo>> GetDrugsBatchAsync(int startId, int endId, CancellationToken cancellationToken = default)
    {
        var results = new List<DrugInfo>();
        var tasks = new List<Task<DrugInfo?>>();

        _logger.LogInformation("开始批量获取药物信息: {StartId} - {EndId}", startId, endId);

        for (int i = startId; i <= endId; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            tasks.Add(GetDrugDetailByIdAsync(i));

            // 控制并发数量
            if (tasks.Count >= _config.MaxConcurrentRequests)
            {
                var completedTasks = await Task.WhenAll(tasks);
                results.AddRange(completedTasks.Where(d => d != null)!);
                tasks.Clear();

                _logger.LogInformation("已处理到ID: {CurrentId}, 成功获取: {Count} 个药物", i, results.Count);
            }
        }

        // 处理剩余任务
        if (tasks.Count > 0)
        {
            var completedTasks = await Task.WhenAll(tasks);
            results.AddRange(completedTasks.Where(d => d != null)!);
        }

        _logger.LogInformation("批量获取完成，总计: {Count} 个药物", results.Count);
        return results;
    }

    /// <summary>
    /// 获取可用的药物总数
    /// </summary>
    public async Task<int> GetAvailableDrugCountAsync()
    {
        // 根据配置返回预期的总数
        return await Task.FromResult(_config.EndId - _config.StartId + 1);
    }

    /// <summary>
    /// 解析药物信息
    /// </summary>
    private DrugInfo? ParseDrugInfo(string html, string externalId, string url)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 检查是否存在药物信息
            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            if (titleNode?.InnerText.Contains("未找到") == true || titleNode?.InnerText.Contains("404") == true)
            {
                return null;
            }

            var drug = new DrugInfo
            {
                ExternalId = externalId,
                ExternalUrl = url,
                DataSource = DataSource.OnlineSearch,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // 解析药物名称
            var nameNode = doc.DocumentNode.SelectSingleNode("//h1[@class='drug-name']") 
                          ?? doc.DocumentNode.SelectSingleNode("//h1") 
                          ?? doc.DocumentNode.SelectSingleNode("//title");
            
            if (nameNode != null)
            {
                drug.DrugName = CleanText(nameNode.InnerText);
            }

            // 解析药物详细信息
            ParseDrugDetails(doc, drug);

            // 如果没有药物名称，尝试从其他地方获取
            if (string.IsNullOrWhiteSpace(drug.DrugName))
            {
                drug.DrugName = drug.GenericName ?? drug.TradeName ?? externalId;
            }

            // 验证是否有有效的药物信息
            if (string.IsNullOrWhiteSpace(drug.DrugName) || drug.DrugName.Length < 2)
            {
                return null;
            }

            return drug;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析药物信息失败: {ExternalId}", externalId);
            return null;
        }
    }

    /// <summary>
    /// 解析药物详细信息
    /// </summary>
    private void ParseDrugDetails(HtmlDocument doc, DrugInfo drug)
    {
        // 常见的标签和对应的属性映射
        var fieldMappings = new Dictionary<string, Action<string>>
        {
            { "通用名称", value => drug.GenericName = value },
            { "商品名称", value => drug.TradeName = value },
            { "汉语拼音", value => drug.Pinyin = value },
            { "批准文号", value => drug.ApprovalNumber = value },
            { "药品分类", value => drug.DrugCategory = value },
            { "生产企业", value => drug.Manufacturer = value },
            { "药品性质", value => drug.DrugNature = value },
            { "相关疾病", value => drug.RelatedDiseases = value },
            { "性状", value => drug.Characteristics = value },
            { "主要成份", value => drug.MainIngredients = value },
            { "适应症", value => drug.Indications = value },
            { "规格", value => drug.Specification = value },
            { "不良反应", value => drug.SideEffects = value },
            { "用法用量", value => drug.Dosage = value },
            { "禁忌", value => drug.Contraindications = value },
            { "注意事项", value => drug.Precautions = value },
            { "药物相互作用", value => drug.DrugInteractions = value },
            { "药理作用", value => drug.PharmacologicalAction = value },
            { "贮藏", value => drug.Storage = value },
            { "包装", value => drug.Packaging = value },
            { "有效期", value => drug.ValidityPeriod = value },
            { "执行标准", value => drug.ExecutionStandard = value }
        };

        // 尝试多种解析方式
        ParseByTableStructure(doc, drug, fieldMappings);
        ParseByDefinitionList(doc, drug, fieldMappings);
        ParseByDivStructure(doc, drug, fieldMappings);
    }

    /// <summary>
    /// 通过表格结构解析
    /// </summary>
    private void ParseByTableStructure(HtmlDocument doc, DrugInfo drug, Dictionary<string, Action<string>> fieldMappings)
    {
        var rows = doc.DocumentNode.SelectNodes("//table//tr");
        if (rows == null) return;

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("td");
            if (cells?.Count >= 2)
            {
                var label = CleanText(cells[0].InnerText);
                var value = CleanText(cells[1].InnerText);
                
                if (fieldMappings.TryGetValue(label, out var setter))
                {
                    setter(value);
                }
            }
        }
    }

    /// <summary>
    /// 通过定义列表解析
    /// </summary>
    private void ParseByDefinitionList(HtmlDocument doc, DrugInfo drug, Dictionary<string, Action<string>> fieldMappings)
    {
        var dtNodes = doc.DocumentNode.SelectNodes("//dt");
        if (dtNodes == null) return;

        foreach (var dt in dtNodes)
        {
            var label = CleanText(dt.InnerText);
            var dd = dt.NextSibling;
            
            while (dd != null && dd.Name != "dd")
            {
                dd = dd.NextSibling;
            }

            if (dd != null)
            {
                var value = CleanText(dd.InnerText);
                if (fieldMappings.TryGetValue(label, out var setter))
                {
                    setter(value);
                }
            }
        }
    }

    /// <summary>
    /// 通过Div结构解析
    /// </summary>
    private void ParseByDivStructure(HtmlDocument doc, DrugInfo drug, Dictionary<string, Action<string>> fieldMappings)
    {
        var divs = doc.DocumentNode.SelectNodes("//div[contains(@class, 'field') or contains(@class, 'info')]");
        if (divs == null) return;

        foreach (var div in divs)
        {
            var labelNode = div.SelectSingleNode(".//span[@class='label'] | .//strong | .//b");
            var valueNode = div.SelectSingleNode(".//span[@class='value'] | .//p");

            if (labelNode != null && valueNode != null)
            {
                var label = CleanText(labelNode.InnerText);
                var value = CleanText(valueNode.InnerText);
                
                if (fieldMappings.TryGetValue(label, out var setter))
                {
                    setter(value);
                }
            }
        }
    }

    /// <summary>
    /// 清理文本内容
    /// </summary>
    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // 移除HTML标签
        text = Regex.Replace(text, @"<[^>]*>", "");
        
        // 移除多余的空白字符
        text = Regex.Replace(text, @"\s+", " ");
        
        // 移除首尾空白和常见的分隔符
        text = text.Trim().Trim(':', '：', '。', '、');
        
        return text;
    }

    /// <summary>
    /// 控制请求频率
    /// </summary>
    private async Task ThrottleRequest()
    {
        lock (_lockObject)
        {
            var elapsed = DateTime.Now - _lastRequestTime;
            var requiredInterval = TimeSpan.FromMilliseconds(_config.RequestIntervalMs);
            
            if (elapsed < requiredInterval)
            {
                var delay = requiredInterval - elapsed;
                Thread.Sleep(delay);
            }
            
            _lastRequestTime = DateTime.Now;
        }
    }
}