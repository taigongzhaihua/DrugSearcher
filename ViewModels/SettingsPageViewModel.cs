using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrugSearcher.Models;
using DrugSearcher.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Application = System.Windows.Application;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace DrugSearcher.ViewModels;

/// <summary>
/// 设置页面视图模型 - 纯UI逻辑
/// </summary>
public partial class SettingsPageViewModel : ObservableObject
{
    #region 私有字段

    private readonly IDynamicSettingsService _dynamicSettingsService;
    private readonly IUserSettingsService _userSettingsService;

    #endregion

    #region 构造函数

    public SettingsPageViewModel(
        IDynamicSettingsService dynamicSettingsService,
        IUserSettingsService userSettingsService)
    {
        _dynamicSettingsService = dynamicSettingsService ?? throw new ArgumentNullException(nameof(dynamicSettingsService));
        _userSettingsService = userSettingsService ?? throw new ArgumentNullException(nameof(userSettingsService));

        SettingGroups = _dynamicSettingsService.SettingGroups;
        FilteredGroups = [];

        _ = LoadSettingsAsync();
    }

    #endregion

    #region 可观察属性

    /// <summary>
    /// 设置分组集合
    /// </summary>
    public ObservableCollection<DynamicSettingGroup> SettingGroups { get; }

    /// <summary>
    /// 过滤后的分组集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DynamicSettingGroup> _filteredGroups;

    /// <summary>
    /// 是否正在加载
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// 状态消息
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// 搜索文本
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    #endregion

    #region 命令

    /// <summary>
    /// 更新设置值命令
    /// </summary>
    [RelayCommand]
    private async Task UpdateSettingAsync(DynamicSettingItem? settingItem)
    {
        if (settingItem == null)
            return;

        try
        {
            object? valueToSave;

            // 1. 处理不同类型的值
            if (settingItem.Value == null)
            {
                valueToSave = settingItem.DefaultValue;
            }
            else if (settingItem.Value is string ||
                     settingItem.Value is bool ||
                     settingItem.Value is int ||
                     settingItem.Value is double ||
                     settingItem.Value.GetType().IsEnum)
            {
                // 直接可用的基础类型
                valueToSave = settingItem.Value;
            }
            else if (settingItem.Value is HotKeySetting hotKey)
            {
                // 处理快捷键设置
                valueToSave = JsonSerializer.Serialize(hotKey);
            }
            else if (settingItem.AdditionalData.TryGetValue("SelectedValuePath", out var selectedValuePath))
            {
                // 处理复杂对象（如匿名对象）
                var valuePathKey = selectedValuePath.ToString();
                if (!string.IsNullOrEmpty(valuePathKey))
                {
                    var type = settingItem.Value.GetType();
                    var property = type.GetProperty(valuePathKey);
                    if (property != null)
                    {
                        valueToSave = property.GetValue(settingItem.Value);
                    }
                    else
                    {
                        Debug.WriteLine($"警告: 在对象 {type.Name} 中找不到属性 {valuePathKey}");
                        valueToSave = settingItem.Value?.ToString(); // 回退方案
                    }
                }
                else
                {
                    valueToSave = settingItem.Value?.ToString(); // 回退方案
                }
            }
            else
            {
                // 其他复杂对象的回退处理
                valueToSave = settingItem.Value?.ToString();
            }

            // 2. 记录日志以便调试
            Debug.WriteLine($"更新设置: {settingItem.Key} = {valueToSave} (原始值: {settingItem.Value})");

            // 3. 保存设置
            if (valueToSave != null)
            {
                await _dynamicSettingsService.UpdateSettingAsync(settingItem.Key, valueToSave);
            }
            else
            {
                Debug.WriteLine($"警告: 设置 {settingItem.Key} 的值为 null，跳过保存");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorMessageAsync($"更新设置失败: {ex.Message}", 3000);
            Debug.WriteLine($"更新设置失败: {settingItem.Key}, {ex.Message}");
        }
    }

    /// <summary>
    /// 更新快捷键设置命令
    /// </summary>
    [RelayCommand]
    private async Task UpdateHotKeySettingAsync(DynamicSettingItem? settingItem)
    {
        if (settingItem == null)
            return;

        try
        {
            // 先更新设置值（移除冲突检查，因为这可能导致问题）
            await UpdateSettingAsync(settingItem);

            // 检查快捷键冲突（异步执行，不阻塞设置保存）
            _ = Task.Run(async () =>
            {
                try
                {
                    if (settingItem.Value is HotKeySetting newHotKey)
                    {
                        var conflictResult = await CheckHotKeyConflictAsync(settingItem.Key, newHotKey);
                        if (conflictResult.HasConflict)
                        {
                            // 在UI线程上显示警告
                            await Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                await ShowWarningMessageAsync($"快捷键可能冲突: {conflictResult.ConflictDescription}", 2000);
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"检查快捷键冲突失败: {ex.Message}");
                }
            });

            // 显示成功消息
            await ShowSuccessMessageAsync("快捷键设置已更新", 1500);
        }
        catch (Exception ex)
        {
            await ShowErrorMessageAsync($"更新快捷键设置失败: {ex.Message}", 3000);
            Debug.WriteLine($"更新快捷键设置失败: {settingItem.Key}, {ex.Message}");
        }
    }


    /// <summary>
    /// 重置分组设置命令
    /// </summary>
    [RelayCommand]
    private async Task ResetGroupAsync(string? groupName)
    {
        if (string.IsNullOrEmpty(groupName))
            return;

        await ExecuteWithLoadingAsync($"正在重置{groupName}设置...", async () =>
        {
            await _dynamicSettingsService.ResetGroupAsync(groupName);
            await LoadSettingsAsync();
            await ShowSuccessMessageAsync($"{groupName}设置已重置", 2000);
        });
    }

    /// <summary>
    /// 重置所有设置命令
    /// </summary>
    [RelayCommand]
    private async Task ResetAllSettingsAsync()
    {
        await ExecuteWithLoadingAsync("正在重置所有设置...", async () =>
        {
            await _dynamicSettingsService.ResetAllSettingsAsync();
            await ShowSuccessMessageAsync("所有设置已重置", 2000);
        });
    }

    /// <summary>
    /// 导出设置命令
    /// </summary>
    [RelayCommand]
    private async Task ExportSettingsAsync()
    {
        await ExecuteWithLoadingAsync("正在导出设置...", async () =>
        {
            var json = await _userSettingsService.ExportSettingsAsync();
            var success = await SaveSettingsToFileAsync(json);

            if (success)
            {
                await ShowSuccessMessageAsync("设置导出成功", 2000);
            }
        });
    }

    /// <summary>
    /// 导入设置命令
    /// </summary>
    [RelayCommand]
    private async Task ImportSettingsAsync()
    {
        try
        {
            var json = await LoadSettingsFromFileAsync();
            if (string.IsNullOrEmpty(json))
                return;

            await ExecuteWithLoadingAsync("正在导入设置...", async () =>
            {
                await _userSettingsService.ImportSettingsAsync(json);
                await _dynamicSettingsService.LoadSettingsAsync();
                await ShowSuccessMessageAsync("设置导入成功", 2000);
            });
        }
        catch (Exception ex)
        {
            await ShowErrorMessageAsync($"导入设置失败: {ex.Message}", 3000);
        }
    }

    /// <summary>
    /// 清除搜索命令
    /// </summary>
    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    #endregion

    #region 快捷键相关方法


    /// <summary>
    /// 检查快捷键冲突
    /// </summary>
    private async Task<HotKeyConflictResult> CheckHotKeyConflictAsync(string settingKey, HotKeySetting newHotKey)
    {
        try
        {
            // 如果快捷键被禁用，不检查冲突
            if (!newHotKey.IsEnabled)
                return new HotKeyConflictResult { HasConflict = false };

            // 获取所有快捷键设置
            var allSettings = await _userSettingsService.GetAllSettingsAsync();

            foreach (var kvp in allSettings.Where(kvp => kvp.Key != settingKey))
            {
                // 检查是否是快捷键设置
                if (!kvp.Key.StartsWith("hotkey.") || kvp.Value is not string jsonValue ||
                    string.IsNullOrEmpty(jsonValue)) continue;
                try
                {
                    var existingHotKey = JsonSerializer.Deserialize<HotKeySetting>(jsonValue);
                    if (existingHotKey is not { IsEnabled: true } ||
                        existingHotKey.Key != newHotKey.Key ||
                        existingHotKey.Modifiers != newHotKey.Modifiers) continue;
                    var conflictSettingItem = _dynamicSettingsService.GetSetting(kvp.Key);
                    var conflictName = conflictSettingItem?.DisplayName ?? kvp.Key;

                    return new HotKeyConflictResult
                    {
                        HasConflict = true,
                        ConflictDescription = $"与 '{conflictName}' 冲突"
                    };
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"解析快捷键设置失败: {kvp.Key}, {ex.Message}");
                }
            }

            return new HotKeyConflictResult { HasConflict = false };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检查快捷键冲突失败: {ex.Message}");
            return new HotKeyConflictResult { HasConflict = false };
        }
    }

    #endregion

    #region 属性变更处理

    /// <summary>
    /// 搜索文本变更处理
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        FilterSettings(value);
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 加载设置
    /// </summary>
    private async Task LoadSettingsAsync()
    {
        await ExecuteWithLoadingAsync("正在加载设置...", async () =>
        {
            await _dynamicSettingsService.LoadSettingsAsync();
            FilterSettings(SearchText);
            await ShowSuccessMessageAsync("设置加载完成", 1000);
        });
    }

    /// <summary>
    /// 过滤设置
    /// </summary>
    /// <param name="searchText">搜索文本</param>
    private void FilterSettings(string searchText)
    {
        FilteredGroups.Clear();

        if (string.IsNullOrWhiteSpace(searchText))
        {
            // 显示所有分组
            foreach (var group in SettingGroups)
            {
                FilteredGroups.Add(group);
            }
        }
        else
        {
            // 根据搜索文本过滤
            var matchedItems = _dynamicSettingsService.SearchSettings(searchText);
            var groupedItems = matchedItems.GroupBy(i => i.Category);

            foreach (var grouping in groupedItems)
            {
                var originalGroup = SettingGroups.FirstOrDefault(g => g.Name == grouping.Key);
                if (originalGroup == null)
                    continue;

                var filteredGroup = new DynamicSettingGroup
                {
                    Name = originalGroup.Name,
                    DisplayName = originalGroup.DisplayName,
                    Description = originalGroup.Description,
                    Icon = originalGroup.Icon,
                    Order = originalGroup.Order
                };

                foreach (var item in grouping)
                {
                    filteredGroup.Items.Add(item);
                }

                FilteredGroups.Add(filteredGroup);
            }
        }
    }

    /// <summary>
    /// 在加载状态下执行操作
    /// </summary>
    /// <param name="loadingMessage">加载消息</param>
    /// <param name="action">要执行的操作</param>
    private async Task ExecuteWithLoadingAsync(string loadingMessage, Func<Task> action)
    {
        IsLoading = true;
        StatusMessage = loadingMessage;

        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await ShowErrorMessageAsync($"操作失败: {ex.Message}", 3000);
            Debug.WriteLine($"操作失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 显示成功消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="durationMs">显示时长</param>
    private async Task ShowSuccessMessageAsync(string message, int durationMs)
    {
        StatusMessage = message;
        await Task.Delay(durationMs);
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// 显示错误消息
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="durationMs">显示时长</param>
    private async Task ShowErrorMessageAsync(string message, int durationMs)
    {
        StatusMessage = message;
        await Task.Delay(durationMs);
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// 保存设置到文件
    /// </summary>
    /// <param name="json">设置JSON</param>
    /// <returns>是否成功</returns>
    private static async Task<bool> SaveSettingsToFileAsync(string json)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = "json",
            FileName = $"DrugSearcher_Settings_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (saveFileDialog.ShowDialog() != true)
            return false;

        await File.WriteAllTextAsync(saveFileDialog.FileName, json);
        return true;
    }

    /// <summary>
    /// 从文件加载设置
    /// </summary>
    /// <returns>设置JSON</returns>
    private static async Task<string?> LoadSettingsFromFileAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            DefaultExt = "json"
        };

        if (openFileDialog.ShowDialog() != true)
            return null;

        return await File.ReadAllTextAsync(openFileDialog.FileName);
    }

    /// <summary>
    /// 显示警告消息
    /// </summary>
    private async Task ShowWarningMessageAsync(string message, int durationMs)
    {
        StatusMessage = $"⚠️ {message}";
        await Task.Delay(durationMs);
        StatusMessage = string.Empty;
    }
    #endregion
}

/// <summary>
/// 快捷键冲突检查结果
/// </summary>
public class HotKeyConflictResult
{
    public bool HasConflict { get; set; }
    public string ConflictDescription { get; set; } = string.Empty;
}