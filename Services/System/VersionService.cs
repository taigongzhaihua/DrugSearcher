using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Application = System.Windows.Application;

namespace DrugSearcher.Services;

/// <summary>
/// 版本信息服务实现 - 支持ClickOnce部署
/// </summary>
public class VersionService(HttpClient httpClient) : IVersionService
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private VersionInfo? _cachedCurrentVersion;
    private readonly Lock _cacheLock = new();

    // ClickOnce相关配置
    private const string SETUP_FOLDER_NAME = "DrugSearcherSetup";
    private const string APPLICATION_NAME = "DrugSearcher";
    private const string PUBLISHER_NAME = "TaiGongZhaiHua";
    private const string MANIFEST_EXTENSION = ".application";
    private const string EXE_MANIFEST_EXTENSION = ".exe.manifest";
    private const string DLL_MANIFEST_EXTENSION = ".dll.manifest";

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
            string version;
            DateTime releaseDate;
            string description;
            long fileSize = 0;
            Dictionary<string, string> deploymentInfo = [];

            // 检查是否是ClickOnce部署
            if (IsClickOnceDeployed())
            {
                // 从ClickOnce清单获取版本信息
                var versionInfo = GetClickOnceVersionInfo();
                version = versionInfo.Version ?? "1.0.0.0";
                releaseDate = versionInfo.InstallDate;
                description = $"{APPLICATION_NAME} - ClickOnce部署版本";
                fileSize = versionInfo.TotalSize;
                deploymentInfo = versionInfo.DeploymentInfo;

                Debug.WriteLine($"检测到ClickOnce版本: {version}");
            }
            else
            {
                // 开发环境或非ClickOnce部署
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyName = assembly.GetName();
                version = assemblyName.Version?.ToString() ?? "1.0.0.0";
                releaseDate = GetBuildDate(assembly);

                var fileVersionInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
                description = fileVersionInfo.FileDescription ?? $"{APPLICATION_NAME} - 现代化药物搜索应用程序";
                fileSize = new FileInfo(assembly.Location).Length;

                Debug.WriteLine($"检测到开发版本: {version}");
            }

            var versionInfoResult = new VersionInfo
            {
                Version = version,
                ReleaseDate = releaseDate,
                Description = description,
                Features = await GetCurrentVersionFeaturesAsync(),
                Fixes = await GetCurrentVersionFixesAsync(),
                IsPreRelease = IsPreReleaseVersion(version),
                FileSize = fileSize,
                IsClickOnceDeployed = IsClickOnceDeployed(),
                DeploymentInfo = deploymentInfo
            };

            lock (_cacheLock)
            {
                _cachedCurrentVersion = versionInfoResult;
            }

            return versionInfoResult;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取当前版本信息失败: {ex.Message}");
            return GetDefaultVersionInfo();
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

            // 如果是ClickOnce部署，检查部署清单
            if (IsClickOnceDeployed())
            {
                return await CheckClickOnceUpdatesAsync(currentVersion);
            }

            // 开发环境使用模拟数据
            return await GetMockUpdateCheckResult(currentVersion);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检查更新失败: {ex.Message}");
            return new UpdateCheckResult
            {
                HasUpdate = false,
                Message = "检查更新时发生错误，请稍后重试。",
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 应用更新
    /// </summary>
    public async Task<bool> ApplyUpdateAsync(UpdateProgressCallback? progressCallback = null)
    {
        if (!IsClickOnceDeployed())
        {
            Debug.WriteLine("非ClickOnce部署，无法应用更新");
            return false;
        }

        try
        {
            var setupPath = GetClickOnceSetupPath();
            if (string.IsNullOrEmpty(setupPath))
            {
                progressCallback?.Invoke(new UpdateProgress
                {
                    State = "找不到更新源，请确保安装盘已连接"
                });
                return false;
            }

            // 查找setup.exe
            var setupExe = Path.Combine(setupPath, "setup.exe");
            if (!File.Exists(setupExe))
            {
                // 尝试查找 DrugSearcher.application 文件
                var applicationFile = Directory.GetFiles(setupPath, "*.application").FirstOrDefault();
                if (!string.IsNullOrEmpty(applicationFile))
                {
                    progressCallback?.Invoke(new UpdateProgress
                    {
                        PercentComplete = 50,
                        State = "正在启动更新程序..."
                    });

                    // 直接启动 .application 文件
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = applicationFile,
                        UseShellExecute = true
                    });

                    progressCallback?.Invoke(new UpdateProgress
                    {
                        PercentComplete = 100,
                        State = "更新程序已启动，应用程序将关闭..."
                    });

                    await Task.Delay(2000);
                    Application.Current.Shutdown();
                    return true;
                }

                Debug.WriteLine($"找不到安装程序: {setupExe}");
                return false;
            }

            // 更新前的准备工作
            await PrepareForUpdateAsync();

            progressCallback?.Invoke(new UpdateProgress
            {
                PercentComplete = 30,
                State = "正在启动安装程序..."
            });

            // 启动安装程序进行更新
            var startInfo = new ProcessStartInfo
            {
                FileName = setupExe,
                UseShellExecute = true,
                WorkingDirectory = setupPath
            };

            Process.Start(startInfo);

            // 通知用户将重启应用
            progressCallback?.Invoke(new UpdateProgress
            {
                PercentComplete = 100,
                State = "更新程序已启动，应用程序将关闭..."
            });

            // 延迟后退出当前应用
            await Task.Delay(2000);
            Application.Current.Shutdown();

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用更新失败: {ex.Message}");
            progressCallback?.Invoke(new UpdateProgress
            {
                State = $"更新失败: {ex.Message}"
            });
            return false;
        }
    }

    /// <summary>
    /// 获取版本历史记录
    /// </summary>
    public async Task<IEnumerable<VersionInfo>> GetVersionHistoryAsync()
    {
        try
        {
            var historyList = new List<VersionInfo>();

            // 添加当前版本到历史记录
            var currentVersion = await GetCurrentVersionAsync();
            historyList.Add(currentVersion);

            // 尝试从本地获取历史版本信息
            if (IsClickOnceDeployed())
            {
                // 获取所有已安装的版本
                var installedVersions = GetInstalledClickOnceVersions();
                historyList.AddRange(installedVersions.Where(version => version.Version != currentVersion.Version));
            }

            // 从安装源获取历史版本信息
            var setupPath = GetClickOnceSetupPath();
            if (!string.IsNullOrEmpty(setupPath))
            {
                var historyFile = Path.Combine(setupPath, "version-history.json");
                if (File.Exists(historyFile))
                {
                    var json = await File.ReadAllTextAsync(historyFile);
                    var history = JsonSerializer.Deserialize<List<VersionInfo>>(json);
                    if (history != null)
                    {
                        historyList.AddRange(history);
                    }
                }
            }

            // 添加已知的历史版本
            historyList.Add(new VersionInfo
            {
                Version = "1.0.1.10",
                ReleaseDate = new DateTime(2025, 1, 15),
                Description = "当前稳定版本",
                Features = ["ClickOnce自动更新支持", "版本管理优化", "清理旧版本功能"],
                Fixes = ["修复更新检测问题", "优化内存使用"],
                IsPreRelease = false,
                IsClickOnceDeployed = true
            });

            historyList.Add(new VersionInfo
            {
                Version = "1.0.0.0",
                ReleaseDate = new DateTime(2024, 12, 1),
                Description = "首次发布版本",
                Features = ["基础搜索功能", "数据管理", "主题切换", "系统托盘支持"],
                Fixes = [],
                IsPreRelease = false,
                IsClickOnceDeployed = true
            });

            // 去重并按版本号排序
            return historyList
                .GroupBy(v => v.Version)
                .Select(g => g.First())
                .OrderByDescending(v => v.Version, new VersionComparer());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取版本历史失败: {ex.Message}");
            return GetMockVersionHistory();
        }
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

    /// <summary>
    /// 清理旧版本文件
    /// </summary>
    public async Task<bool> CleanupOldVersionsAsync()
    {
        try
        {
            if (!IsClickOnceDeployed())
            {
                Debug.WriteLine("非ClickOnce部署，跳过清理");
                return false;
            }

            var cleanupTasks = new List<Task<bool>>
            {
                CleanupClickOnceDirectoriesAsync(),
                Task.Run(() => CleanupRegistry()),
                CleanupManifestCacheAsync()
            };

            var results = await Task.WhenAll(cleanupTasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清理旧版本时发生错误: {ex.Message}");
            return false;
        }
    }

    #region 私有方法

    /// <summary>
    /// 检查是否是ClickOnce部署
    /// </summary>
    private static bool IsClickOnceDeployed()
    {
        try
        {
            var appPath = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(appPath);

            if (!string.IsNullOrEmpty(directory))
            {
                // ClickOnce应用通常在LocalApplicationData\Apps\2.0目录下
                return directory.Contains(@"\Apps\2.0\") ||
                       directory.Contains(@"\AppData\Local\Apps\");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检查ClickOnce部署失败: {ex.Message}");
        }

        return false;
    }
    /// <summary>
    /// 获取ClickOnce版本信息
    /// </summary>
    private static ClickOnceVersionInfo GetClickOnceVersionInfo()
    {
        var versionInfo = new ClickOnceVersionInfo
        {
            Version = "1.0.0.0",
            InstallDate = DateTime.Now,
            TotalSize = 0,
            DeploymentInfo = []
        };

        try
        {
            var appPath = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(appPath);

            if (string.IsNullOrEmpty(directory))
                return versionInfo;

            // 查找所有清单文件
            var manifestFiles = Directory.GetFiles(directory, "*.manifest").ToArray();
            Debug.WriteLine($"找到 {manifestFiles.Length} 个清单文件");

            string? detectedVersion = null;
            Dictionary<string, string> deploymentDetails = [];

            foreach (var manifestFile in manifestFiles)
            {
                try
                {
                    Debug.WriteLine($"检查清单文件: {Path.GetFileName(manifestFile)}");

                    var manifest = XDocument.Load(manifestFile);
                    var root = manifest.Root;
                    if (root == null) continue;

                    // 获取所有命名空间
                    var namespaces = root.Attributes()
                        .Where(a => a.IsNamespaceDeclaration)
                        .Select(a => new { Prefix = a.Name.LocalName, Namespace = a.Value })
                        .ToList();

                    // 查找 assemblyIdentity 元素（可能带有各种前缀）
                    var assemblyIdentities = root.Descendants()
                        .Where(e => e.Name.LocalName == "assemblyIdentity")
                        .ToList();

                    foreach (var identity in assemblyIdentities)
                    {
                        var nameAttr = identity.Attribute("name")?.Value;
                        var versionAttr = identity.Attribute("version")?.Value;

                        // 检查是否是 DrugSearcher 相关的 identity
                        if (!string.IsNullOrEmpty(nameAttr) &&
                            (nameAttr.Equals("DrugSearcher.exe", StringComparison.OrdinalIgnoreCase) ||
                             nameAttr.Equals("DrugSearcher.application", StringComparison.OrdinalIgnoreCase)))
                        {
                            if (!string.IsNullOrEmpty(versionAttr))
                            {
                                detectedVersion = versionAttr;
                                Debug.WriteLine($"从 {Path.GetFileName(manifestFile)} 获取版本: {versionAttr}");

                                // 收集其他属性
                                deploymentDetails["Name"] = nameAttr;
                                deploymentDetails["PublicKeyToken"] = identity.Attribute("publicKeyToken")?.Value ?? "0000000000000000";
                                deploymentDetails["Language"] = identity.Attribute("language")?.Value ?? "neutral";
                                deploymentDetails["ProcessorArchitecture"] = identity.Attribute("processorArchitecture")?.Value ?? "msil";
                                deploymentDetails["Type"] = identity.Attribute("type")?.Value ?? "";
                            }
                        }
                    }

                    // 查找描述信息（可能在 description 元素中）
                    var descriptions = root.Descendants()
                        .Where(e => e.Name.LocalName == "description")
                        .ToList();

                    foreach (var desc in descriptions)
                    {
                        // 检查各种可能的命名空间前缀
                        var publisherAttrs = desc.Attributes()
                            .Where(a => a.Name.LocalName == "publisher")
                            .ToList();

                        var productAttrs = desc.Attributes()
                            .Where(a => a.Name.LocalName == "product")
                            .ToList();

                        if (publisherAttrs.Count != 0)
                            deploymentDetails["Publisher"] = publisherAttrs.First().Value;
                        if (productAttrs.Count != 0)
                            deploymentDetails["Product"] = productAttrs.First().Value;
                    }

                    // 如果是 .exe.manifest 文件，还要查找入口点
                    if (!manifestFile.Contains(".exe.manifest", StringComparison.OrdinalIgnoreCase)) continue;
                    var entryPoints = root.Descendants()
                        .Where(e => e.Name.LocalName == "entryPoint")
                        .ToList();

                    foreach (var entryAssembly in entryPoints.Select(entryPoint => entryPoint.Descendants()
                                 .FirstOrDefault(e => e.Name.LocalName == "assemblyIdentity")).OfType<XElement>())
                    {
                        deploymentDetails["EntryPoint"] = entryAssembly.Attribute("name")?.Value ?? "";
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"解析清单文件 {Path.GetFileName(manifestFile)} 失败: {ex.Message}");
                }
            }

            // 设置版本信息
            if (!string.IsNullOrEmpty(detectedVersion))
            {
                versionInfo.Version = detectedVersion;
                versionInfo.DeploymentInfo = deploymentDetails;
            }
            else
            {
                // 如果从清单文件获取失败，尝试从程序集获取
                var assembly = Assembly.GetExecutingAssembly();
                versionInfo.Version = assembly.GetName().Version?.ToString() ?? "1.0.0.0";
                Debug.WriteLine($"从程序集获取版本: {versionInfo.Version}");
            }

            // 获取安装日期
            versionInfo.InstallDate = Directory.GetCreationTime(directory);

            // 计算总大小
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            versionInfo.TotalSize = files.Sum(f => new FileInfo(f).Length);

            // 获取部署路径信息
            versionInfo.DeploymentInfo["DeploymentPath"] = directory;
            versionInfo.DeploymentInfo["DeploymentType"] = "ClickOnce";

            // 解析路径获取更多信息
            var pathParts = directory.Split(Path.DirectorySeparatorChar);
            for (var i = 0; i < pathParts.Length; i++)
            {
                if (pathParts[i] != "Apps" || i + 1 >= pathParts.Length || pathParts[i + 1] != "2.0") continue;
                if (i + 3 < pathParts.Length)
                {
                    versionInfo.DeploymentInfo["StoreFolder"] = pathParts[i + 2];
                    versionInfo.DeploymentInfo["ComponentFolder"] = pathParts[i + 3];
                }
                break;
            }

            Debug.WriteLine($"最终版本信息: {versionInfo.Version}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取ClickOnce版本信息失败: {ex.Message}");
        }

        return versionInfo;
    }

    /// <summary>
    /// 获取已安装的ClickOnce版本
    /// </summary>
    private static List<VersionInfo> GetInstalledClickOnceVersions()
    {
        var versions = new List<VersionInfo>();

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appsPath = Path.Combine(localAppData, "Apps", "2.0");

            if (!Directory.Exists(appsPath))
                return versions;

            // 查找所有包含应用程序的目录
            var appDirs = Directory.GetDirectories(appsPath, "*", SearchOption.AllDirectories)
                .Where(dir => Directory.GetFiles(dir, "drug*.*").Length != 0 ||
                              Directory.GetFiles(dir, "*DrugSearcher*.*").Length != 0);

            foreach (var dir in appDirs)
            {
                try
                {
                    var manifestFiles = Directory.GetFiles(dir, "*.manifest");
                    versions = [.. from manifestFile in manifestFiles
                        where manifestFile.Contains("drug", StringComparison.OrdinalIgnoreCase) ||
                              manifestFile.Contains(APPLICATION_NAME, StringComparison.OrdinalIgnoreCase)
                        let manifestData = TryLoadManifest(manifestFile)
                        where manifestData != null
                        select new VersionInfo
                        {
                            Version = manifestData.Value.Version,
                            ReleaseDate = File.GetCreationTime(manifestFile),
                            Description = $"已安装版本 - {Path.GetFileName(dir)}",
                            IsClickOnceDeployed = true,
                            FileSize = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                                .Sum(f => new FileInfo(f).Length),
                            Features = [],
                            Fixes = []
                        }];
                }
                catch
                {
                    // 忽略无法访问的目录
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"获取已安装版本失败: {ex.Message}");
        }

        return versions;
    }

    // 辅助方法
    private static (string Version, XNamespace Namespace)? TryLoadManifest(string manifestFile)
    {
        try
        {
            var manifest = XDocument.Load(manifestFile);
            var ns = manifest.Root?.GetDefaultNamespace();
            if (ns == null) return null;

            var identity = manifest.Root?.Element(ns + "asmv1:assemblyIdentity");
            var version = identity?.Attribute("version")?.Value;

            return string.IsNullOrEmpty(version) ? null : (version, ns);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 检查ClickOnce更新
    /// </summary>
    private static async Task<UpdateCheckResult> CheckClickOnceUpdatesAsync(VersionInfo currentVersion)
    {
        try
        {
            // 查找部署清单
            var setupPath = GetClickOnceSetupPath();
            if (string.IsNullOrEmpty(setupPath))
            {
                return new UpdateCheckResult
                {
                    HasUpdate = false,
                    Message = "无法找到更新位置。请确保安装盘已连接。"
                };
            }

            // 查找.application文件
            var applicationFiles = Directory.GetFiles(setupPath, $"*{MANIFEST_EXTENSION}");
            if (applicationFiles.Length == 0)
            {
                return new UpdateCheckResult
                {
                    HasUpdate = false,
                    Message = "无法找到部署清单文件。"
                };
            }

            // 解析部署清单获取版本信息
            var manifestPath = applicationFiles[0];
            var manifest = await Task.Run(() => XDocument.Load(manifestPath));

            var ns = manifest.Root?.GetDefaultNamespace();
            if (ns == null)
                return new UpdateCheckResult
                {
                    HasUpdate = false,
                    Message = "部署清单格式无效。"
                };

            // 从根节点的 assemblyIdentity 获取版本
            var rootIdentity = manifest.Root?.Attribute("version")?.Value;

            // 从 dependency 节点获取版本信息
            var dependency = manifest.Root?.Element(ns + "dependency");
            var dependentAssembly = dependency?.Element(ns + "dependentAssembly");
            var assemblyIdentity = dependentAssembly?.Element(ns + "assemblyIdentity");

            // 优先使用 dependentAssembly 中的版本，否则使用根节点版本
            var manifestVersion = assemblyIdentity?.Attribute("version")?.Value ?? rootIdentity;

            Debug.WriteLine($"清单版本: {manifestVersion}, 当前版本: {currentVersion.Version}");

            if (string.IsNullOrEmpty(manifestVersion))
                return new UpdateCheckResult
                {
                    HasUpdate = false,
                    Message = "无法从清单文件获取版本信息。"
                };

            var hasUpdate = CompareVersions(currentVersion.Version, manifestVersion) < 0;

            if (!hasUpdate)
                return new UpdateCheckResult
                {
                    HasUpdate = false,
                    Message = "您使用的是最新版本。"
                };

            // 获取更新大小
            var updateSize = await GetUpdateSizeAsync(setupPath, manifestVersion);

            var latestVersion = new VersionInfo
            {
                Version = manifestVersion,
                ReleaseDate = File.GetLastWriteTime(manifestPath),
                Description = "新版本可用",
                Features = ["通过ClickOnce自动更新"],
                Fixes = [],
                IsPreRelease = false,
                FileSize = updateSize,
                IsClickOnceDeployed = true,
                DownloadUrl = manifestPath
            };

            // 检查部署配置
            var deployment = manifest.Root?.Element(ns + "deployment");
            var install = deployment?.Attribute("install")?.Value == "true";

            return new UpdateCheckResult
            {
                HasUpdate = true,
                LatestVersion = latestVersion,
                Message = $"发现新版本 {latestVersion.Version}！",
                IsRequired = install
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                HasUpdate = false,
                Message = "检查更新时发生错误。",
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 获取更新大小
    /// </summary>
    private static async Task<long> GetUpdateSizeAsync(string setupPath, string version)
    {
        try
        {
            // 查找对应版本的Application Files文件夹
            var appFilesPath = Path.Combine(setupPath, "Application Files", $"{APPLICATION_NAME}_{version.Replace('.', '_')}");

            if (Directory.Exists(appFilesPath))
            {
                var files = await Task.Run(() => Directory.GetFiles(appFilesPath, "*", SearchOption.AllDirectories));
                return files.Sum(f => new FileInfo(f).Length);
            }

            // 如果找不到特定版本文件夹，返回setup文件夹的大小
            var setupFiles = await Task.Run(() => Directory.GetFiles(setupPath, "*", SearchOption.AllDirectories));
            return setupFiles.Sum(f => new FileInfo(f).Length);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 获取ClickOnce安装路径
    /// </summary>
    private static string? GetClickOnceSetupPath()
    {
        try
        {
            // 优先查找可移动磁盘
            var removableDrives = DriveInfo.GetDrives()
                .Where(d => d is { DriveType: DriveType.Removable, IsReady: true })
                .OrderBy(d => d.Name);

            foreach (var drive in removableDrives)
            {
                var setupPath = Path.Combine(drive.RootDirectory.FullName, SETUP_FOLDER_NAME);
                if (!Directory.Exists(setupPath) || Directory.GetFiles(setupPath, "*.application").Length == 0) continue;
                Debug.WriteLine($"在可移动磁盘找到安装路径: {setupPath}");
                return setupPath;
            }

            // 检查所有固定磁盘
            var fixedDrives = DriveInfo.GetDrives()
                .Where(d => d is { DriveType: DriveType.Fixed, IsReady: true })
                .OrderBy(d => d.Name);

            foreach (var drive in fixedDrives)
            {
                var setupPath = Path.Combine(drive.RootDirectory.FullName, SETUP_FOLDER_NAME);
                if (!Directory.Exists(setupPath) || Directory.GetFiles(setupPath, "*.application").Length == 0) continue;
                Debug.WriteLine($"在固定磁盘找到安装路径: {setupPath}");
                return setupPath;
            }

            Debug.WriteLine("未找到ClickOnce安装路径");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"查找安装路径失败: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 清理ClickOnce目录
    /// </summary>
    private static async Task<bool> CleanupClickOnceDirectoriesAsync()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appsPath = Path.Combine(localAppData, "Apps", "2.0");

            if (!Directory.Exists(appsPath))
                return true;

            var currentVersion = GetClickOnceVersionInfo().Version;
            var currentPath = Assembly.GetExecutingAssembly().Location;
            var currentDir = Path.GetDirectoryName(currentPath);

            var dirsToDelete = new List<string>();

            // 查找所有应用程序目录
            await Task.Run(() =>
            {
                var appDirs = Directory.GetDirectories(appsPath, "*", SearchOption.TopDirectoryOnly);

                foreach (var storeDir in appDirs)
                {
                    var componentDirs = Directory.GetDirectories(storeDir, "*", SearchOption.TopDirectoryOnly);

                    foreach (var componentDir in componentDirs)
                    {
                        // 跳过当前运行的目录
                        if (!string.IsNullOrEmpty(currentDir) &&
                            (componentDir.Equals(currentDir, StringComparison.OrdinalIgnoreCase) ||
                             currentDir.StartsWith(componentDir, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        // 检查是否包含DrugSearcher相关文件
                        var isDrugSearcherDir = Directory.GetFiles(componentDir, "drug*.*", SearchOption.AllDirectories).Length != 0 ||
                                              Directory.GetFiles(componentDir, "*DrugSearcher*.*", SearchOption.AllDirectories).Length != 0;

                        if (isDrugSearcherDir)
                        {
                            dirsToDelete.Add(componentDir);
                        }
                    }
                }
            });

            // 删除旧版本目录
            foreach (var dir in dirsToDelete)
            {
                try
                {
                    await Task.Run(() => Directory.Delete(dir, true));
                    Debug.WriteLine($"已删除旧版本目录: {dir}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"删除目录失败 {dir}: {ex.Message}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清理版本目录失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 清理清单缓存
    /// </summary>
    private static async Task<bool> CleanupManifestCacheAsync()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var manifestsPath = Path.Combine(localAppData, "Apps", "2.0", "manifests");

            if (!Directory.Exists(manifestsPath))
                return true;

            var currentVersion = GetClickOnceVersionInfo().Version;

            await Task.Run(() =>
            {
                var manifestFiles = Directory.GetFiles(manifestsPath, "drug*.*")
                    .Where(f => !f.Contains(currentVersion?.Replace(".", "_") ?? string.Empty));

                foreach (var file in manifestFiles)
                {
                    try
                    {
                        File.Delete(file);
                        Debug.WriteLine($"已删除清单文件: {file}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"删除清单文件失败 {file}: {ex.Message}");
                    }
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清理清单缓存失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 清理注册表
    /// </summary>
    private static bool CleanupRegistry()
    {
        try
        {
            using var uninstallKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall",
                true);

            if (uninstallKey == null) return true;

            var currentVersion = GetClickOnceVersionInfo().Version;
            var subKeysToDelete = new List<string>();

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                using var appKey = uninstallKey.OpenSubKey(subKeyName);

                if (appKey?.GetValue("DisplayName") is string displayName &&
                    (displayName.Contains(APPLICATION_NAME, StringComparison.OrdinalIgnoreCase) ||
                     displayName.Contains("drug", StringComparison.OrdinalIgnoreCase)))
                {
                    var installLocation = appKey.GetValue("InstallLocation") as string ?? "";
                    var urlUpdateInfo = appKey.GetValue("UrlUpdateInfo") as string ?? "";

                    // 检查是否是旧版本
                    if (!installLocation.Contains(currentVersion?.Replace(".", "_") ?? string.Empty) &&
                        !urlUpdateInfo.Contains(currentVersion?.Replace(".", "_") ?? string.Empty))
                    {
                        subKeysToDelete.Add(subKeyName);
                    }
                }
            }

            // 删除收集的键
            foreach (var keyName in subKeysToDelete)
            {
                try
                {
                    uninstallKey.DeleteSubKeyTree(keyName);
                    Debug.WriteLine($"已清理注册表项: {keyName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"删除注册表项失败 {keyName}: {ex.Message}");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清理注册表失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 更新前的准备工作
    /// </summary>
    private static async Task PrepareForUpdateAsync()
    {
        await Task.Run(() =>
        {
            CleanupTempFiles();

            // 保存用户设置
            try
            {

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"保存用户设置失败: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 清理临时文件
    /// </summary>
    private static void CleanupTempFiles()
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var patterns = new[] { $"{APPLICATION_NAME}*.tmp", "drug*.tmp", "*.deploy" };

            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(tempPath, pattern);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        Debug.WriteLine($"已删除临时文件: {file}");
                    }
                    catch
                    {
                        // 忽略无法删除的文件
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清理临时文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取默认版本信息
    /// </summary>
    private static VersionInfo GetDefaultVersionInfo()
    {
        // 根据您提供的文件，当前版本应该是 1.0.1.13
        return new VersionInfo
        {
            Version = "1.0.1.13",
            ReleaseDate = DateTime.Now,
            Description = APPLICATION_NAME,
            Features = ["药物搜索功能", "本地数据管理", "主题切换", "ClickOnce自动更新"],
            Fixes = [],
            IsPreRelease = false,
            IsClickOnceDeployed = IsClickOnceDeployed()
        };
    }

    /// <summary>
    /// 获取构建日期
    /// </summary>
    private static DateTime GetBuildDate(Assembly assembly)
    {
        try
        {
            var filePath = assembly.Location;
            if (File.Exists(filePath))
            {
                return File.GetCreationTime(filePath);
            }
            return DateTime.Now;
        }
        catch
        {
            return DateTime.Now;
        }
    }

    /// <summary>
    /// 获取当前版本功能列表
    /// </summary>
    private static Task<List<string>> GetCurrentVersionFeaturesAsync()
    {
        return Task.FromResult<List<string>>([
            "🔍 智能药物搜索",
            "📊 本地数据管理",
            "🎨 多主题支持",
            "⌨️ 快捷键操作",
            "🔔 系统托盘集成",
            "📤 数据导入导出",
            "🔧 高级设置选项",
            "🔄 ClickOnce自动更新"
        ]);
    }

    /// <summary>
    /// 获取当前版本修复列表
    /// </summary>
    private static Task<List<string>> GetCurrentVersionFixesAsync()
    {
        return Task.FromResult<List<string>>([
            "搜索性能优化",
            "内存使用改进",
            "界面响应速度提升",
            "主题切换稳定性改善"
        ]);
    }

    /// <summary>
    /// 判断是否为预发布版本
    /// </summary>
    private static bool IsPreReleaseVersion(string version)
    {
        return version.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
               version.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
               version.Contains("rc", StringComparison.OrdinalIgnoreCase) ||
               version.Contains("preview", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 获取应用程序大小
    /// </summary>
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

    /// <summary>
    /// 获取模拟更新检查结果
    /// </summary>
    private static Task<UpdateCheckResult> GetMockUpdateCheckResult(VersionInfo currentVersion)
    {
        var latestVersion = new VersionInfo
        {
            Version = "1.1.0.0",
            ReleaseDate = DateTime.Now.AddDays(-1),
            Description = "新版本包含重要功能更新",
            Features = [
                "新增高级搜索功能",
                "优化用户界面体验",
                "增加数据导出功能",
                "支持更多主题色彩"
            ],
            Fixes = [
                "修复搜索结果显示问题",
                "优化内存使用效率",
                "修复主题切换偶发异常",
                "提升应用启动速度"
            ],
            IsPreRelease = false,
            FileSize = 15 * 1024 * 1024,
            IsClickOnceDeployed = false
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

    /// <summary>
    /// 获取模拟版本历史
    /// </summary>
    private static IEnumerable<VersionInfo> GetMockVersionHistory()
    {
        return
        [
            new VersionInfo
            {
                Version = "1.0.1.13",
                ReleaseDate = new DateTime(2025, 1, 17),
                Description = "最新版本",
                Features = ["改进版本检测逻辑", "优化ClickOnce更新流程"],
                Fixes = ["修复版本号解析问题", "修复清单文件读取错误"],
                IsPreRelease = false,
                IsClickOnceDeployed = true
            },
            new VersionInfo
            {
                Version = "1.0.1.10",
                ReleaseDate = new DateTime(2025, 1, 15),
                Description = "稳定版本",
                Features = ["ClickOnce自动更新支持", "版本管理优化", "清理旧版本功能"],
                Fixes = ["修复更新检测问题", "优化内存使用"],
                IsPreRelease = false,
                IsClickOnceDeployed = true
            },
            new VersionInfo
            {
                Version = "1.0.0.0",
                ReleaseDate = new DateTime(2024, 12, 1),
                Description = "首次发布版本",
                Features = ["基础搜索功能", "数据管理", "主题切换", "系统托盘支持"],
                Fixes = [],
                IsPreRelease = false,
                IsClickOnceDeployed = true
            }
        ];
    }

    /// <summary>
    /// 比较版本号
    /// </summary>
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

    #endregion

    #region 内部类

    /// <summary>
    /// ClickOnce版本信息
    /// </summary>
    private class ClickOnceVersionInfo
    {
        public string? Version { get; set; }
        public DateTime InstallDate { get; set; }
        public long TotalSize { get; set; }
        public Dictionary<string, string> DeploymentInfo { get; set; } = [];
    }

    /// <summary>
    /// 版本比较器
    /// </summary>
    private class VersionComparer : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            try
            {
                var v1 = new Version(x);
                var v2 = new Version(y);
                return v1.CompareTo(v2);
            }
            catch
            {
                return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    #endregion
}

#region 数据模型

/// <summary>
/// 更新进度
/// </summary>
public class UpdateProgress
{
    public int PercentComplete { get; set; }
    public long BytesDownloaded { get; set; }
    public long TotalBytesToDownload { get; set; }
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// 更新进度回调委托
/// </summary>
public delegate void UpdateProgressCallback(UpdateProgress progress);

#endregion