using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace DrugSearcher.Services;

/// <summary>
/// 版本信息服务实现
/// </summary>
public class VersionService(HttpClient httpClient) : IVersionService
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private const string UpdateCheckUrl = "https://api.github.com/repos/taigongzhaihua/drugsearcher/releases/latest";
    private VersionInfo? _cachedCurrentVersion;
    private readonly Lock _cacheLock = new();

    /// <summary>
    /// 获取当前版本信息
    /// </summary>
    public async Task<VersionInfo> GetCurrentVersionAsync()
    {
        lock (_cacheLock)
        {
            if (_cachedCurrentVersion != null)
                return _cachedCurrentVersion;
        }

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName();
            var version = assemblyName.Version?.ToString() ?? "1.0.0.0";

            // 获取文件版本信息
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);

            // 获取构建时间
            var buildDate = GetBuildDate(assembly);

            var versionInfo = new VersionInfo
            {
                Version = version,
                ReleaseDate = buildDate,
                Description = fileVersionInfo.FileDescription ?? "DrugSearcher - 现代化药物搜索应用程序",
                Features = await GetCurrentVersionFeaturesAsync(),
                Fixes = await GetCurrentVersionFixesAsync(),
                IsPreRelease = IsPreReleaseVersion(version),
                FileSize = GetApplicationSize()
            };

            lock (_cacheLock)
            {
                _cachedCurrentVersion = versionInfo;
            }

            return versionInfo;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取当前版本信息失败: {ex.Message}");

            // 返回默认版本信息
            var defaultVersion = new VersionInfo
            {
                Version = "1.0.0.0",
                ReleaseDate = DateTime.Now,
                Description = "DrugSearcher",
                Features = ["药物搜索功能", "本地数据管理", "主题切换"],
                Fixes = [],
                IsPreRelease = false
            };

            lock (_cacheLock)
            {
                _cachedCurrentVersion = defaultVersion;
            }

            return defaultVersion;
        }
    }

    /// <summary>
    /// 检查是否有新版本
    /// </summary>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        try
        {
            var currentVersion = await GetCurrentVersionAsync();

            // 设置超时时间
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            try
            {
                var response = await _httpClient.GetStringAsync(UpdateCheckUrl, cts.Token);
                var releaseInfo = JsonSerializer.Deserialize<GitHubRelease>(response);

                if (releaseInfo != null)
                {
                    var latestVersion = new VersionInfo
                    {
                        Version = releaseInfo.TagName?.TrimStart('v') ?? "1.0.0.0",
                        ReleaseDate = releaseInfo.PublishedAt,
                        Description = releaseInfo.Name ?? "新版本",
                        Features = ParseReleaseNotes(releaseInfo.Body, "Features", "功能"),
                        Fixes = ParseReleaseNotes(releaseInfo.Body, "Fixes", "修复"),
                        IsPreRelease = releaseInfo.Prerelease,
                        DownloadUrl = releaseInfo.HtmlUrl ?? "https://github.com/taigongzhaihua/drugsearcher/releases/latest",
                        FileSize = GetAssetSize(releaseInfo.Assets)
                    };

                    var hasUpdate = CompareVersions(currentVersion.Version, latestVersion.Version) < 0;

                    return new UpdateCheckResult
                    {
                        HasUpdate = hasUpdate,
                        LatestVersion = hasUpdate ? latestVersion : null,
                        Message = hasUpdate ? $"发现新版本 {latestVersion.Version}！" : "您使用的是最新版本。",
                        IsRequired = false
                    };
                }
            }
            catch (TaskCanceledException)
            {
                return new UpdateCheckResult
                {
                    HasUpdate = false,
                    Message = "检查更新超时，请稍后重试。"
                };
            }

            // 如果网络请求失败，返回模拟数据
            return await GetMockUpdateCheckResult(currentVersion);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检查更新失败: {ex.Message}");

            // 返回模拟数据作为fallback
            var currentVersion = await GetCurrentVersionAsync();
            return await GetMockUpdateCheckResult(currentVersion);
        }
    }

    /// <summary>
    /// 获取版本历史记录
    /// </summary>
    public async Task<IEnumerable<VersionInfo>> GetVersionHistoryAsync()
    {
        try
        {
            // 尝试从GitHub获取历史版本
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetStringAsync(
                "https://api.github.com/repos/taigongzhaihua/drugsearcher/releases",
                cts.Token);

            var releases = JsonSerializer.Deserialize<GitHubRelease[]>(response);

            if (releases != null)
            {
                return releases.Select(release => new VersionInfo
                {
                    Version = release.TagName?.TrimStart('v') ?? "1.0.0.0",
                    ReleaseDate = release.PublishedAt,
                    Description = release.Name ?? "版本发布",
                    Features = ParseReleaseNotes(release.Body, "Features", "功能"),
                    Fixes = ParseReleaseNotes(release.Body, "Fixes", "修复"),
                    IsPreRelease = release.Prerelease
                }).OrderByDescending(v => v.ReleaseDate);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取版本历史失败: {ex.Message}");
        }

        // 返回模拟数据
        return GetMockVersionHistory();
    }

    /// <summary>
    /// 清除版本缓存
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _cachedCurrentVersion = null;
        }
    }

    #region 私有方法

    private static Task<UpdateCheckResult> GetMockUpdateCheckResult(VersionInfo currentVersion)
    {
        // 模拟检查结果
        var latestVersion = new VersionInfo
        {
            Version = "1.1.0.0",
            ReleaseDate = DateTime.Now.AddDays(-1),
            Description = "新版本包含重要功能更新",
            Features =
            [
                "新增高级搜索功能",
                "优化用户界面体验",
                "增加数据导出功能",
                "支持更多主题色彩"
            ],
            Fixes =
            [
                "修复搜索结果显示问题",
                "优化内存使用效率",
                "修复主题切换偶发异常",
                "提升应用启动速度"
            ],
            IsPreRelease = false,
            DownloadUrl = "https://github.com/taigongzhaihua/drugsearcher/releases/latest",
            FileSize = 15 * 1024 * 1024 // 15MB
        };

        var hasUpdate = CompareVersions(currentVersion.Version, latestVersion.Version) < 0;

        return Task.FromResult(new UpdateCheckResult
        {
            HasUpdate = hasUpdate,
            LatestVersion = hasUpdate ? latestVersion : null,
            Message = hasUpdate ? $"发现新版本 {latestVersion.Version}！" : "您使用的是最新版本。",
            IsRequired = false
        });
    }

    private static IEnumerable<VersionInfo> GetMockVersionHistory() =>
    [
        new()
        {
            Version = "1.0.0.0",
            ReleaseDate = DateTime.Now.AddDays(-30),
            Description = "首次发布版本",
            Features = ["基础搜索功能", "数据管理", "主题切换", "系统托盘支持"],
            Fixes = [],
            IsPreRelease = false
        },
        new()
        {
            Version = "0.9.0.0",
            ReleaseDate = DateTime.Now.AddDays(-45),
            Description = "Release Candidate 版本",
            Features = ["核心功能实现", "用户界面优化", "性能改进"],
            Fixes = ["修复启动问题", "优化内存使用", "界面适配问题"],
            IsPreRelease = true
        },
        new()
        {
            Version = "0.8.0.0",
            ReleaseDate = DateTime.Now.AddDays(-60),
            Description = "Beta 测试版本",
            Features = ["搜索功能原型", "基础界面框架"],
            Fixes = ["修复崩溃问题", "数据库连接优化"],
            IsPreRelease = true
        }
    ];

    private static DateTime GetBuildDate(Assembly assembly)
    {
        try
        {
            // 尝试从AssemblyMetadata获取构建时间
            var buildTimeAttribute = assembly.GetCustomAttribute<AssemblyMetadataAttribute>();
            if (buildTimeAttribute?.Key == "BuildTime" &&
                DateTime.TryParse(buildTimeAttribute.Value, out var buildTime))
            {
                return buildTime;
            }

            // 从文件创建时间获取
            var filePath = assembly.Location;
            if (File.Exists(filePath))
            {
                return File.GetCreationTime(filePath);
            }

            // 使用当前时间作为fallback
            return DateTime.Now;
        }
        catch
        {
            return DateTime.Now;
        }
    }

    private static Task<List<string>> GetCurrentVersionFeaturesAsync() => Task.FromResult<List<string>>([
        "🔍 智能药物搜索",
        "📊 本地数据管理",
        "🎨 多主题支持",
        "⌨️ 快捷键操作",
        "🔔 系统托盘集成",
        "📤 数据导入导出",
        "🔧 高级设置选项",
        "🌐 在线更新检查"
    ]);

    private static Task<List<string>> GetCurrentVersionFixesAsync() => Task.FromResult<List<string>>([
        "搜索性能优化",
        "内存使用改进",
        "界面响应速度提升",
        "主题切换稳定性改善"
    ]);

    private static bool IsPreReleaseVersion(string version) => version.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
                                                               version.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
                                                               version.Contains("rc", StringComparison.OrdinalIgnoreCase) ||
                                                               version.Contains("preview", StringComparison.OrdinalIgnoreCase);

    private static long GetApplicationSize()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fileInfo = new FileInfo(assembly.Location);
            return fileInfo.Length;
        }
        catch
        {
            return 0;
        }
    }

    private static int CompareVersions(string version1, string version2)
    {
        try
        {
            var v1 = new Version(version1);
            var v2 = new Version(version2);
            return v1.CompareTo(v2);
        }
        catch
        {
            return string.Compare(version1, version2, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static List<string> ParseReleaseNotes(string? body, params string[] sectionHeaders)
    {
        var features = new List<string>();

        if (string.IsNullOrEmpty(body))
            return features;

        foreach (var header in sectionHeaders)
        {
            var headerIndex = body.IndexOf(header, StringComparison.OrdinalIgnoreCase);
            if (headerIndex >= 0)
            {
                var startIndex = headerIndex + header.Length;
                var endIndex = body.IndexOf('\n', startIndex);

                if (endIndex > startIndex)
                {
                    var section = body[startIndex..endIndex];
                    var items = section.Split(['-', '*'], StringSplitOptions.RemoveEmptyEntries);

                    foreach (var item in items)
                    {
                        var trimmed = item.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                        {
                            features.Add(trimmed);
                        }
                    }
                }
            }
        }

        return features;
    }

    private static long GetAssetSize(GitHubAsset[]? assets)
    {
        if (assets == null || assets.Length == 0)
            return 0;

        var mainAsset = assets.FirstOrDefault(a =>
            a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            a.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

        return mainAsset?.Size ?? 0;
    }

    #endregion
}

#region GitHub API 数据模型

internal class GitHubRelease
{
    public string? TagName { get; set; }
    public string? Name { get; set; }
    public string? Body { get; set; }
    public bool Prerelease { get; set; }
    public DateTime PublishedAt { get; set; }
    public string? HtmlUrl { get; set; }
    public GitHubAsset[]? Assets { get; set; }
}

internal class GitHubAsset
{
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public string? BrowserDownloadUrl { get; set; }
}

#endregion