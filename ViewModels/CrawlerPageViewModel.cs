using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Models;
using DrugSearcher.Services;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.ViewModels;

public partial class CrawlerPageViewModel : ObservableObject
{
    private readonly IOnlineDrugService _onlineDrugService;
    private readonly ILogger<CrawlerPageViewModel> _logger;
    private CancellationTokenSource? _crawlCancellationTokenSource;

    public CrawlerPageViewModel(
        IOnlineDrugService onlineDrugService,
        ILogger<CrawlerPageViewModel> logger)
    {
        _onlineDrugService = onlineDrugService;
        _logger = logger;

        CrawlLogs = [];

        // 设置默认值
        StartId = 1;
        EndId = 124051;
        BatchSize = 10;
        DelayMs = 1000;

        Task.Run(UpdateStatistics);
    }

    #region 属性

    [ObservableProperty] public partial int StartId { get; set; }

    [ObservableProperty] public partial int EndId { get; set; }

    [ObservableProperty] public partial int BatchSize { get; set; }

    [ObservableProperty] public partial int DelayMs { get; set; }

    [ObservableProperty] public partial bool IsCrawling { get; set; }

    [ObservableProperty] public partial bool CanStartCrawl { get; set; } = true;

    [ObservableProperty] public partial string CrawlButtonText { get; set; } = "开始爬取";

    [ObservableProperty] public partial double ProgressPercentage { get; set; }

    [ObservableProperty] public partial string ProgressText { get; set; } = "准备就绪";

    [ObservableProperty] public partial int TotalProcessed { get; set; }

    [ObservableProperty] public partial int SuccessCount { get; set; }

    [ObservableProperty] public partial int FailedCount { get; set; }

    [ObservableProperty] public partial int CurrentId { get; set; }

    [ObservableProperty] public partial string EstimatedTimeRemaining { get; set; } = "--";

    [ObservableProperty] public partial string CrawlSpeed { get; set; } = "0 条/分钟";

    [ObservableProperty] public partial DateTime StartTime { get; set; }

    [ObservableProperty] public partial DateTime EndTime { get; set; }

    [ObservableProperty] public partial string ElapsedTime { get; set; } = "00:00:00";

    // 统计信息
    [ObservableProperty] public partial int TotalDrugsInDatabase { get; set; }

    [ObservableProperty] public partial int SuccessfulDrugs { get; set; }

    [ObservableProperty] public partial int FailedDrugs { get; set; }

    [ObservableProperty] public partial int NotFoundDrugs { get; set; }

    [ObservableProperty] public partial int ParseErrorDrugs { get; set; }

    [ObservableProperty] public partial string CompletionRate { get; set; } = "0%";

    public ObservableCollection<CrawlLogEntry> CrawlLogs { get; }

    #endregion

    #region 命令

    [RelayCommand]
    private async Task StartCrawl()
    {
        if (IsCrawling)
        {
            await StopCrawl();
            return;
        }

        if (StartId <= 0 || EndId <= 0 || StartId > EndId)
        {
            MessageBox.Show("请设置正确的起始和结束ID", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (BatchSize is <= 0 or > 50)
        {
            MessageBox.Show("批次大小应在1-50之间", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (DelayMs < 100)
        {
            MessageBox.Show("请求间隔不能少于100毫秒", "参数错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await StartCrawling();
    }

    [RelayCommand]
    private async Task StopCrawl()
    {
        if (_crawlCancellationTokenSource != null)
        {
            await _crawlCancellationTokenSource.CancelAsync();
            IsCrawling = false;
            CrawlButtonText = "开始爬取";
            CanStartCrawl = true;

            AddLog("用户取消了爬取任务", LogLevel.Warning);
        }
    }

    [RelayCommand]
    private async Task RetryFailedDrugs()
    {
        try
        {
            AddLog("开始重新爬取失败的药物...", LogLevel.Information);

            var failedIds = await _onlineDrugService.GetFailedDrugIdsAsync();
            if (failedIds.Count == 0)
            {
                MessageBox.Show("没有需要重试的失败记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"发现 {failedIds.Count} 条失败记录，是否重新爬取？",
                "确认重试",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                IsCrawling = true;
                CrawlButtonText = "停止爬取";
                CanStartCrawl = false;
                StartTime = DateTime.Now;

                var progress = new Progress<CrawlProgress>(OnCrawlProgress);
                var crawlResult = await _onlineDrugService.RetryCrawlFailedDrugsAsync(failedIds, progress);

                EndTime = DateTime.Now;
                IsCrawling = false;
                CrawlButtonText = "开始爬取";
                CanStartCrawl = true;

                AddLog($"重试完成，成功: {crawlResult.SuccessCount}，失败: {crawlResult.FailedCount}", LogLevel.Information);
                await UpdateStatistics();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重试失败药物时发生错误");
            AddLog($"重试失败：{ex.Message}", LogLevel.Error);
        }
    }

    [RelayCommand]
    private async Task CleanupOldRecords()
    {
        try
        {
            var result = MessageBox.Show(
                "确定要清理30天前的失败记录吗？这个操作无法撤销。",
                "确认清理",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                AddLog("开始清理旧的失败记录...", LogLevel.Information);

                var cleanedCount = await _onlineDrugService.CleanupOldFailedRecordsAsync(30);

                AddLog($"清理完成，删除了 {cleanedCount} 条旧记录", LogLevel.Information);
                await UpdateStatistics();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理旧记录时发生错误");
            AddLog($"清理失败：{ex.Message}", LogLevel.Error);
        }
    }

    [RelayCommand]
    private async Task ViewRecentCrawled()
    {
        try
        {
            var recentDrugs = await _onlineDrugService.GetRecentCrawledDrugsAsync(20);

            var recentInfo = string.Join("\n", recentDrugs.Select(d =>
                $"[{d.CrawledAt:yyyy-MM-dd HH:mm}] {d.DrugName} (ID: {d.Id})"));

            if (string.IsNullOrEmpty(recentInfo))
            {
                AddLog("暂无最近爬取的药物记录", LogLevel.Information);
            }
            else
            {
                AddLog($"最近爬取的药物：\n{recentInfo}", LogLevel.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取最近爬取药物时发生错误");
            AddLog($"获取失败：{ex.Message}", LogLevel.Error);
        }
    }

    [RelayCommand]
    private async Task RefreshStatistics()
    {
        await UpdateStatistics();
        AddLog("统计信息已更新", LogLevel.Information);
    }

    [RelayCommand]
    private void ClearLogs()
    {
        CrawlLogs.Clear();
        AddLog("日志已清空", LogLevel.Information);
    }

    [RelayCommand]
    private void SetQuickRange(string range)
    {
        switch (range)
        {
            case "First1000":
                StartId = 1;
                EndId = 1000;
                break;
            case "First10000":
                StartId = 1;
                EndId = 10000;
                break;
            case "All":
                StartId = 1;
                EndId = 124051;
                break;
            case "Test":
                StartId = 1;
                EndId = 100;
                break;
        }

        AddLog($"已设置爬取范围：{StartId}-{EndId}", LogLevel.Information);
    }

    #endregion

    #region 私有方法

    private async Task StartCrawling()
    {
        try
        {
            _crawlCancellationTokenSource = new CancellationTokenSource();
            IsCrawling = true;
            CrawlButtonText = "停止爬取";
            CanStartCrawl = false;
            StartTime = DateTime.Now;

            ResetProgress();
            AddLog($"开始爬取，范围：{StartId}-{EndId}，批次大小：{BatchSize}，延迟：{DelayMs}ms", LogLevel.Information);

            var progress = new Progress<CrawlProgress>(OnCrawlProgress);
            var result = await _onlineDrugService.CrawlDrugInfosAsync(StartId, EndId, BatchSize, DelayMs, progress);

            EndTime = DateTime.Now;
            IsCrawling = false;
            CrawlButtonText = "开始爬取";
            CanStartCrawl = true;

            AddLog(
                $@"爬取完成！总计：{result.TotalProcessed}，成功：{result.SuccessCount}，失败：{result.FailedCount}，耗时：{result.Duration:hh\:mm\:ss}",
                LogLevel.Information);

            await UpdateStatistics();
        }
        catch (OperationCanceledException)
        {
            AddLog("爬取被用户取消", LogLevel.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "爬取过程中发生错误");
            AddLog($"爬取出错：{ex.Message}", LogLevel.Error);
        }
        finally
        {
            IsCrawling = false;
            CrawlButtonText = "开始爬取";
            CanStartCrawl = true;
        }
    }

    private void OnCrawlProgress(CrawlProgress progress) => Application.Current.Dispatcher.Invoke(() =>
    {
        TotalProcessed = progress.TotalProcessed;
        SuccessCount = progress.SuccessCount;
        FailedCount = progress.FailedCount;
        CurrentId = progress.CurrentId;
        ProgressPercentage = progress.ProgressPercentage;
        ProgressText = $"正在处理 ID: {CurrentId} ({TotalProcessed}/{EndId - StartId + 1})";

        // 计算速度和预估时间
        var elapsed = DateTime.Now - StartTime;
        if (elapsed.TotalMinutes > 0)
        {
            var speed = TotalProcessed / elapsed.TotalMinutes;
            CrawlSpeed = $"{speed:F1} 条/分钟";

            var remaining = EndId - StartId + 1 - TotalProcessed;
            if (speed > 0)
            {
                var estimatedMinutes = remaining / speed;
                EstimatedTimeRemaining = TimeSpan.FromMinutes(estimatedMinutes).ToString(@"hh\:mm\:ss");
            }
        }

        ElapsedTime = elapsed.ToString(@"hh\:mm\:ss");
    });

    private void ResetProgress()
    {
        TotalProcessed = 0;
        SuccessCount = 0;
        FailedCount = 0;
        CurrentId = 0;
        ProgressPercentage = 0;
        ProgressText = "准备开始...";
        EstimatedTimeRemaining = "--";
        CrawlSpeed = "0 条/分钟";
        ElapsedTime = "00:00:00";
    }

    private async Task UpdateStatistics()
    {
        try
        {
            var statistics = await _onlineDrugService.GetCrawlStatisticsAsync();

            TotalDrugsInDatabase = statistics.TotalRecords;
            SuccessfulDrugs = statistics.SuccessCount;
            FailedDrugs = statistics.FailedCount + statistics.ParseErrorCount + statistics.NotFoundCount;

            const int totalPossible = 124051;
            var completionPercentage = (double)statistics.SuccessCount / totalPossible * 100;
            CompletionRate = $"{completionPercentage:F2}%";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新统计信息时发生错误");
            AddLog($"更新统计失败：{ex.Message}", LogLevel.Error);
        }
    }

    private void AddLog(string message, LogLevel level) => Application.Current.Dispatcher.Invoke(() =>
    {
        var logEntry = new CrawlLogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message
        };

        CrawlLogs.Insert(0, logEntry);

        // 限制日志数量，避免内存占用过大
        while (CrawlLogs.Count > 1000)
        {
            CrawlLogs.RemoveAt(CrawlLogs.Count - 1);
        }
    });

    #endregion
}

/// <summary>
/// 爬取日志条目
/// </summary>
public class CrawlLogEntry
{
    public DateTime Timestamp { get; set; }

    public LogLevel Level { get; set; }

    public string Message { get; set; } = string.Empty;

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss");

    public string LevelText => Level switch
    {
        LogLevel.Information => "信息",
        LogLevel.Warning => "警告",
        LogLevel.Error => "错误",
        LogLevel.Debug => "调试",
        _ => "未知"
    };

    public string LevelColor => Level switch
    {
        LogLevel.Information => "#2196F3",
        LogLevel.Warning => "#FF9800",
        LogLevel.Error => "#F44336",
        LogLevel.Debug => "#9E9E9E",
        _ => "#000000"
    };
}