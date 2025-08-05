using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Helpers;
using DrugSearcher.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using System.Xml.Linq;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.ViewModels;

/// <summary>
/// 关于页面视图模型
/// </summary>
public partial class AboutPageViewModel : ObservableObject
{
    private readonly IVersionService _versionService;

    public AboutPageViewModel(IVersionService versionService)
    {
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));

        // 初始化版本历史
        VersionHistory = [];

        // 异步初始化
        _ = InitializeAsync();
    }

    #region 可观察属性

    /// <summary>
    /// 当前版本信息
    /// </summary>
    [ObservableProperty]
    public partial VersionInfo? CurrentVersion { get; set; }

    /// <summary>
    /// 更新检查结果
    /// </summary>
    [ObservableProperty]
    public partial UpdateCheckResult? UpdateCheckResult { get; set; }

    /// <summary>
    /// 是否正在检查更新
    /// </summary>
    [ObservableProperty]
    public partial bool IsCheckingUpdate { get; set; }

    /// <summary>
    /// 是否正在更新
    /// </summary>
    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    /// <summary>
    /// 更新进度
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdateProgressText))]
    public partial int UpdateProgress { get; set; }

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    [field: MaybeNull, AllowNull]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// ClickOnce 部署信息
    /// </summary>
    [ObservableProperty]
    [field: MaybeNull, AllowNull]
    public partial string DeploymentInfo { get; set; } = string.Empty;

    #endregion

    #region 只读属性

    /// <summary>
    /// 版本历史
    /// </summary>
    public ObservableCollection<VersionInfo> VersionHistory { get; }

    /// <summary>
    /// 应用程序名称
    /// </summary>
    public static string AppName => "DrugSearcher";

    /// <summary>
    /// 应用程序描述
    /// </summary>
    public static string AppDescription => "现代化的药物搜索应用程序，提供智能搜索、数据管理和多主题支持";

    /// <summary>
    /// 版权信息
    /// </summary>
    public static string Copyright => $"© {DateTime.Now.Year} DrugSearcher. All rights reserved.";

    /// <summary>
    /// 开发者信息
    /// </summary>
    public static string Developer => "DrugSearcher Team";

    /// <summary>
    /// 是否有可用更新
    /// </summary>
    public bool HasAvailableUpdate => UpdateCheckResult?.HasUpdate == true;

    /// <summary>
    /// 当前版本是否为预发布版本
    /// </summary>
    public bool IsPreRelease => CurrentVersion?.IsPreRelease == true;

    /// <summary>
    /// 是否是 ClickOnce 部署
    /// </summary>
    public bool IsClickOnceDeployment => CurrentVersion?.IsClickOnceDeployed == true;

    /// <summary>
    /// 更新进度文本
    /// </summary>
    public string UpdateProgressText => IsUpdating ? $"更新进度: {UpdateProgress}%" : string.Empty;

    #endregion

    #region 命令

    /// <summary>
    /// 检查更新命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingUpdate || IsUpdating) return;

        try
        {
            IsCheckingUpdate = true;
            StatusMessage = "正在检查更新...";

            UpdateCheckResult = await _versionService.CheckForUpdatesAsync();
            StatusMessage = UpdateCheckResult.Message;

            // 如果有更新且是必需的，自动开始更新
            if (UpdateCheckResult.IsRequired && UpdateCheckResult.HasUpdate)
            {
                StatusMessage = "检测到必需更新，准备开始更新...";
                await Task.Delay(2000);
                await ApplyUpdateAsync();
            }

            // 通知相关属性变更
            OnPropertyChanged(nameof(HasAvailableUpdate));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检查更新失败: {ex.Message}");
            StatusMessage = $"检查更新失败: {ex.Message}";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    /// <summary>
    /// 是否可以检查更新
    /// </summary>
    private bool CanCheckForUpdates() => !IsCheckingUpdate && !IsUpdating;

    /// <summary>
    /// 应用更新命令（ClickOnce）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApplyUpdate))]
    private async Task ApplyUpdateAsync()
    {
        if (!IsClickOnceDeployment)
        {
            // 非 ClickOnce 部署，打开下载链接
            if (UpdateCheckResult?.LatestVersion?.DownloadUrl != null)
            {
                OpenUrl(UpdateCheckResult.LatestVersion.DownloadUrl);
            }
            return;
        }

        try
        {
            IsUpdating = true;
            UpdateProgress = 0;
            StatusMessage = "正在准备更新...";

            var success = await _versionService.ApplyUpdateAsync(progress =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateProgress = progress.PercentComplete;
                    StatusMessage = $"正在下载更新: {progress.BytesDownloaded / 1024 / 1024:F2}MB / {progress.TotalBytesToDownload / 1024 / 1024:F2}MB";
                });
            });

            if (success)
            {
                StatusMessage = "更新下载完成，准备重启应用程序...";

                var result = MessageBox.Show(
                    "更新已准备就绪，需要重启应用程序以完成更新。\n\n是否立即重启？",
                    "更新完成",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    // 清理旧版本
                    await _versionService.CleanupOldVersionsAsync();

                    // 重启应用
                    ClickOnceHelper.RestartApplication();
                }
            }
            else
            {
                StatusMessage = "更新失败，请稍后重试";
                MessageBox.Show("更新失败，请检查网络连接后重试。", "更新失败",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用更新失败: {ex.Message}");
            StatusMessage = $"更新失败: {ex.Message}";
            MessageBox.Show($"更新过程中发生错误:\n{ex.Message}", "更新错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsUpdating = false;
            UpdateProgress = 0;
        }
    }

    /// <summary>
    /// 是否可以应用更新
    /// </summary>
    private bool CanApplyUpdate() => HasAvailableUpdate && !IsUpdating && !IsCheckingUpdate;

    /// <summary>
    /// 打开GitHub命令
    /// </summary>
    [RelayCommand]
    private void OpenGitHub() => OpenUrl("https://github.com/taigongzhaihua/drugsearcher");

    /// <summary>
    /// 打开许可证命令
    /// </summary>
    [RelayCommand]
    private void OpenLicense() => OpenUrl("https://github.com/taigongzhaihua/drugsearcher/blob/main/LICENSE");

    /// <summary>
    /// 刷新版本信息命令
    /// </summary>
    [RelayCommand]
    private async Task RefreshVersionInfoAsync()
    {
        try
        {
            StatusMessage = "正在刷新版本信息...";

            // 清除缓存并重新获取版本信息
            _versionService.ClearCache();
            CurrentVersion = await _versionService.GetCurrentVersionAsync();

            // 重新加载版本历史
            await LoadVersionHistoryAsync();

            // 加载部署信息
            LoadDeploymentInfo();

            StatusMessage = "版本信息已刷新";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"刷新版本信息失败: {ex.Message}");
            StatusMessage = "刷新版本信息失败";
        }
    }

    /// <summary>
    /// 复制版本信息命令
    /// </summary>
    [RelayCommand]
    private void CopyVersionInfo()
    {
        try
        {
            if (CurrentVersion != null)
            {
                var versionInfo = $"""
                                   {AppName} {CurrentVersion.Version}
                                   发布日期: {CurrentVersion.ReleaseDate:yyyy-MM-dd}
                                   构建时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
                                   开发者: {Developer}
                                   部署类型: {(IsClickOnceDeployment ? "ClickOnce" : "独立安装")}
                                   {(IsClickOnceDeployment ? $"\n部署信息:\n{DeploymentInfo}" : "")}
                                   """;

                Clipboard.SetText(versionInfo);
                StatusMessage = "版本信息已复制到剪贴板";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"复制版本信息失败: {ex.Message}");
            StatusMessage = "复制版本信息失败";
        }
    }

    /// <summary>
    /// 清理旧版本命令
    /// </summary>
    [RelayCommand]
    private async Task CleanupOldVersionsAsync()
    {
        try
        {
            StatusMessage = "正在清理旧版本...";

            var result = MessageBox.Show(
                "此操作将删除所有旧版本的程序文件，释放磁盘空间。\n\n是否继续？",
                "清理旧版本",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var success = await _versionService.CleanupOldVersionsAsync();

                if (success)
                {
                    StatusMessage = "旧版本清理完成";
                    MessageBox.Show("旧版本已成功清理。", "清理完成",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    StatusMessage = "旧版本清理失败";
                    MessageBox.Show("清理旧版本时发生错误。", "清理失败",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                StatusMessage = "已取消清理";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清理旧版本失败: {ex.Message}");
            StatusMessage = $"清理失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 打开应用程序文件夹命令
    /// </summary>
    [RelayCommand]
    private void OpenApplicationFolder()
    {
        try
        {
            ClickOnceHelper.OpenApplicationFolder();
            StatusMessage = "已打开应用程序文件夹";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"打开文件夹失败: {ex.Message}");
            StatusMessage = "打开文件夹失败";
        }
    }

    #endregion

    #region 属性变更通知

    /// <summary>
    /// 当UpdateCheckResult变更时
    /// </summary>
    partial void OnUpdateCheckResultChanged(UpdateCheckResult? value)
    {
        // 通知CanExecute状态变更
        ApplyUpdateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasAvailableUpdate));
    }

    /// <summary>
    /// 当CurrentVersion变更时
    /// </summary>
    partial void OnCurrentVersionChanged(VersionInfo? value)
    {
        OnPropertyChanged(nameof(IsPreRelease));
        OnPropertyChanged(nameof(IsClickOnceDeployment));
    }

    /// <summary>
    /// 当IsUpdating变更时
    /// </summary>
    partial void OnIsUpdatingChanged(bool value)
    {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        ApplyUpdateCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// 当IsCheckingUpdate变更时
    /// </summary>
    partial void OnIsCheckingUpdateChanged(bool value)
    {
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        ApplyUpdateCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 异步初始化
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            StatusMessage = "正在加载版本信息...";

            // 加载当前版本信息
            CurrentVersion = await _versionService.GetCurrentVersionAsync();

            // 加载版本历史
            await LoadVersionHistoryAsync();

            // 加载部署信息
            LoadDeploymentInfo();

            StatusMessage = "就绪";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"初始化关于页面失败: {ex.Message}");
            StatusMessage = "初始化失败";
        }
    }

    /// <summary>
    /// 加载版本历史
    /// </summary>
    private async Task LoadVersionHistoryAsync()
    {
        try
        {
            var history = await _versionService.GetVersionHistoryAsync();

            VersionHistory.Clear();
            foreach (var version in history)
            {
                VersionHistory.Add(version);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载版本历史失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 加载部署信息
    /// </summary>
    private void LoadDeploymentInfo()
    {
        try
        {
            if (!IsClickOnceDeployment)
            {
                DeploymentInfo = "独立安装版本";
                return;
            }

            // 获取 ClickOnce 部署路径  
            var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var appDir = Path.GetDirectoryName(appPath);

            if (string.IsNullOrEmpty(appDir))
            {
                DeploymentInfo = "无法获取部署信息";
                return;
            }

            // 从路径中提取部署信息  
            var deploymentInfo = new List<string>
           {  
               // 添加部署路径  
               $"部署路径: {appDir}"
           };

            // 尝试从清单文件获取更多信息  
            var manifestFiles = Directory.GetFiles(appDir, "*.manifest");
            if (manifestFiles.Length > 0)
            {
                try
                {
                    var manifest = XDocument.Load(manifestFiles[0]);
                    var ns = manifest.Root?.GetDefaultNamespace();

                    if (ns != null) // 确保 ns 不为 null  
                    {
                        // 获取发布者信息  
                        var publisher = manifest.Descendants(ns + "description")
                            .FirstOrDefault()?.Attribute(ns + "publisher")?.Value;
                        if (!string.IsNullOrEmpty(publisher))
                        {
                            deploymentInfo.Add($"发布者: {publisher}");
                        }

                        // 获取产品名称  
                        var product = manifest.Descendants(ns + "description")
                            .FirstOrDefault()?.Attribute(ns + "product")?.Value;
                        if (!string.IsNullOrEmpty(product))
                        {
                            deploymentInfo.Add($"产品: {product}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"解析清单文件失败: {ex.Message}");
                }
            }

            // 获取应用程序数据大小  
            try
            {
                var dirInfo = new DirectoryInfo(appDir);
                var totalSize = dirInfo.GetFiles("*", SearchOption.AllDirectories)
                    .Sum(file => file.Length);
                deploymentInfo.Add($"应用程序大小: {totalSize / 1024 / 1024:F2} MB");
            }
            catch
            {
                // 忽略错误  
            }

            // 检查是否从可移动磁盘运行  
            if (ClickOnceHelper.IsRunningFromRemovableDrive())
            {
                deploymentInfo.Add("部署源: 可移动磁盘");
            }

            DeploymentInfo = string.Join("\n", deploymentInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载部署信息失败: {ex.Message}");
            DeploymentInfo = "无法获取部署信息";
        }
    }

    /// <summary>
    /// 打开URL
    /// </summary>
    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"打开URL失败: {ex.Message}");
            StatusMessage = "打开链接失败";
        }
    }

    #endregion
}