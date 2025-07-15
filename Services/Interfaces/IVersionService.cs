namespace DrugSearcher.Services
{
    /// <summary>
    /// 版本信息服务接口
    /// </summary>
    public interface IVersionService
    {
        /// <summary>
        /// 获取当前版本信息
        /// </summary>
        /// <returns>版本信息</returns>
        Task<VersionInfo> GetCurrentVersionAsync();

        /// <summary>
        /// 检查是否有新版本
        /// </summary>
        /// <returns>更新检查结果</returns>
        Task<UpdateCheckResult> CheckForUpdatesAsync();

        /// <summary>
        /// 获取版本历史记录
        /// </summary>
        /// <returns>版本历史</returns>
        Task<IEnumerable<VersionInfo>> GetVersionHistoryAsync();

        /// <summary>
        /// 清除版本缓存
        /// </summary>
        void ClearCache();
    }

    /// <summary>
    /// 版本信息
    /// </summary>
    public class VersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> Features { get; set; } = [];
        public List<string> Fixes { get; set; } = [];
        public bool IsPreRelease { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
    }

    /// <summary>
    /// 更新检查结果
    /// </summary>
    public class UpdateCheckResult
    {
        public bool HasUpdate { get; set; }
        public VersionInfo? LatestVersion { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsRequired { get; set; }
    }
}