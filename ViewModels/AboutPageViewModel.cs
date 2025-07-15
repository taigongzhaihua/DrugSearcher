using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace DrugSearcher.ViewModels
{
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
        private VersionInfo? currentVersion;

        /// <summary>
        /// 更新检查结果
        /// </summary>
        [ObservableProperty]
        private UpdateCheckResult? updateCheckResult;

        /// <summary>
        /// 是否正在检查更新
        /// </summary>
        [ObservableProperty]
        private bool isCheckingUpdate;

        /// <summary>
        /// 状态消息
        /// </summary>
        [ObservableProperty]
        private string statusMessage = string.Empty;

        #endregion

        #region 只读属性

        /// <summary>
        /// 版本历史
        /// </summary>
        public ObservableCollection<VersionInfo> VersionHistory { get; }

        /// <summary>
        /// 应用程序名称
        /// </summary>
        public string AppName => "DrugSearcher";

        /// <summary>
        /// 应用程序描述
        /// </summary>
        public string AppDescription => "现代化的药物搜索应用程序，提供智能搜索、数据管理和多主题支持";

        /// <summary>
        /// 版权信息
        /// </summary>
        public string Copyright => $"© {DateTime.Now.Year} DrugSearcher. All rights reserved.";

        /// <summary>
        /// 开发者信息
        /// </summary>
        public string Developer => "DrugSearcher Team";

        /// <summary>
        /// 是否有可用更新
        /// </summary>
        public bool HasAvailableUpdate => UpdateCheckResult?.HasUpdate == true;

        /// <summary>
        /// 当前版本是否为预发布版本
        /// </summary>
        public bool IsPreRelease => CurrentVersion?.IsPreRelease == true;

        #endregion

        #region 命令

        /// <summary>
        /// 检查更新命令
        /// </summary>
        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            if (IsCheckingUpdate) return;

            try
            {
                IsCheckingUpdate = true;
                StatusMessage = "正在检查更新...";

                UpdateCheckResult = await _versionService.CheckForUpdatesAsync();
                StatusMessage = UpdateCheckResult.Message;

                // 通知相关属性变更
                OnPropertyChanged(nameof(HasAvailableUpdate));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查更新失败: {ex.Message}");
                StatusMessage = "检查更新失败";
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        /// <summary>
        /// 下载更新命令
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDownloadUpdate))]
        private void DownloadUpdate()
        {
            if (UpdateCheckResult?.LatestVersion?.DownloadUrl != null)
            {
                OpenUrl(UpdateCheckResult.LatestVersion.DownloadUrl);
            }
        }

        /// <summary>
        /// 是否可以下载更新
        /// </summary>
        private bool CanDownloadUpdate => HasAvailableUpdate;

        /// <summary>
        /// 打开GitHub命令
        /// </summary>
        [RelayCommand]
        private void OpenGitHub()
        {
            OpenUrl("https://github.com/taigongzhaihua/drugsearcher");
        }

        /// <summary>
        /// 打开许可证命令
        /// </summary>
        [RelayCommand]
        private void OpenLicense()
        {
            OpenUrl("https://github.com/taigongzhaihua/drugsearcher/blob/main/LICENSE");
        }

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
                CurrentVersion = await _versionService.GetCurrentVersionAsync();

                // 重新加载版本历史
                await LoadVersionHistoryAsync();

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
                        """;

                    System.Windows.Clipboard.SetText(versionInfo);
                    StatusMessage = "版本信息已复制到剪贴板";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"复制版本信息失败: {ex.Message}");
                StatusMessage = "复制版本信息失败";
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
            DownloadUpdateCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(HasAvailableUpdate));
        }

        /// <summary>
        /// 当CurrentVersion变更时
        /// </summary>
        partial void OnCurrentVersionChanged(VersionInfo? value)
        {
            OnPropertyChanged(nameof(IsPreRelease));
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
}