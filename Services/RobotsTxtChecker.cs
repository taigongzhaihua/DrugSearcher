using System.Text.RegularExpressions;

namespace DrugSearcher.Services;

/// <summary>
/// Robots.txt 检查器
/// </summary>
public class RobotsTxtChecker
{
    private readonly HttpClient _httpClient;
    private readonly string _userAgent;
    private readonly Dictionary<string, RobotsTxtRules> _cache;

    public RobotsTxtChecker(HttpClient httpClient, string userAgent)
    {
        _httpClient = httpClient;
        _userAgent = userAgent;
        _cache = new Dictionary<string, RobotsTxtRules>();
    }

    /// <summary>
    /// 检查URL是否被robots.txt禁止访问
    /// </summary>
    /// <param name="url">要检查的URL</param>
    /// <returns>true表示被禁止，false表示允许</returns>
    public async Task<bool> IsDisallowedAsync(string url)
    {
        try
        {
            var uri = new Uri(url);
            var baseUrl = $"{uri.Scheme}://{uri.Host}";
            var path = uri.AbsolutePath;

            // 获取robots.txt规则
            var rules = await GetRobotsTxtRulesAsync(baseUrl);
            
            // 检查路径是否被禁止
            return rules.IsDisallowed(path);
        }
        catch (Exception)
        {
            // 如果无法获取robots.txt，默认允许访问
            return false;
        }
    }

    /// <summary>
    /// 获取robots.txt规则
    /// </summary>
    /// <param name="baseUrl">基础URL</param>
    /// <returns>robots.txt规则</returns>
    private async Task<RobotsTxtRules> GetRobotsTxtRulesAsync(string baseUrl)
    {
        // 检查缓存
        if (_cache.TryGetValue(baseUrl, out var cachedRules))
        {
            return cachedRules;
        }

        var robotsTxtUrl = $"{baseUrl}/robots.txt";
        var rules = new RobotsTxtRules();

        try
        {
            var response = await _httpClient.GetAsync(robotsTxtUrl);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                rules.ParseContent(content, _userAgent);
            }
        }
        catch (Exception)
        {
            // 如果无法获取robots.txt，使用默认规则（允许所有）
        }

        // 缓存规则
        _cache[baseUrl] = rules;
        return rules;
    }
}

/// <summary>
/// Robots.txt 规则类
/// </summary>
public class RobotsTxtRules
{
    private readonly List<string> _disallowedPaths;
    private readonly List<string> _allowedPaths;
    private int _crawlDelay;

    public RobotsTxtRules()
    {
        _disallowedPaths = new List<string>();
        _allowedPaths = new List<string>();
        _crawlDelay = 0;
    }

    /// <summary>
    /// 获取爬取延迟（秒）
    /// </summary>
    public int CrawlDelay => _crawlDelay;

    /// <summary>
    /// 解析robots.txt内容
    /// </summary>
    /// <param name="content">robots.txt内容</param>
    /// <param name="userAgent">用户代理</param>
    public void ParseContent(string content, string userAgent)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        bool isRelevantSection = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // 跳过注释行
            if (trimmedLine.StartsWith("#") || string.IsNullOrWhiteSpace(trimmedLine))
                continue;

            // 处理User-agent行
            if (trimmedLine.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
            {
                var agent = trimmedLine.Substring("User-agent:".Length).Trim();
                isRelevantSection = agent == "*" || 
                                   agent.Equals(userAgent, StringComparison.OrdinalIgnoreCase) ||
                                   userAgent.Contains(agent, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            // 只处理相关的User-agent段
            if (!isRelevantSection)
                continue;

            // 处理Disallow行
            if (trimmedLine.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
            {
                var path = trimmedLine.Substring("Disallow:".Length).Trim();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    _disallowedPaths.Add(path);
                }
            }
            // 处理Allow行
            else if (trimmedLine.StartsWith("Allow:", StringComparison.OrdinalIgnoreCase))
            {
                var path = trimmedLine.Substring("Allow:".Length).Trim();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    _allowedPaths.Add(path);
                }
            }
            // 处理Crawl-delay行
            else if (trimmedLine.StartsWith("Crawl-delay:", StringComparison.OrdinalIgnoreCase))
            {
                var delay = trimmedLine.Substring("Crawl-delay:".Length).Trim();
                if (int.TryParse(delay, out var delaySeconds))
                {
                    _crawlDelay = delaySeconds;
                }
            }
        }
    }

    /// <summary>
    /// 检查路径是否被禁止
    /// </summary>
    /// <param name="path">要检查的路径</param>
    /// <returns>true表示被禁止，false表示允许</returns>
    public bool IsDisallowed(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // 首先检查Allow规则（优先级更高）
        foreach (var allowedPath in _allowedPaths)
        {
            if (IsPathMatched(path, allowedPath))
                return false;
        }

        // 然后检查Disallow规则
        foreach (var disallowedPath in _disallowedPaths)
        {
            if (IsPathMatched(path, disallowedPath))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 检查路径是否匹配模式
    /// </summary>
    /// <param name="path">实际路径</param>
    /// <param name="pattern">匹配模式</param>
    /// <returns>是否匹配</returns>
    private static bool IsPathMatched(string path, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        // 如果模式以*结尾，进行前缀匹配
        if (pattern.EndsWith('*'))
        {
            var prefix = pattern.Substring(0, pattern.Length - 1);
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        // 精确匹配或前缀匹配
        return path.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(pattern, StringComparison.OrdinalIgnoreCase);
    }
}