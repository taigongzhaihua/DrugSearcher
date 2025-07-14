using DrugSearcher.Models;
using System.Collections.ObjectModel;

namespace DrugSearcher.Services;

/// <summary>
/// 动态设置服务接口
/// </summary>
public interface IDynamicSettingsService
{
    /// <summary>
    /// 设置分组集合
    /// </summary>
    ObservableCollection<DynamicSettingGroup> SettingGroups { get; }

    /// <summary>
    /// 注册设置项
    /// </summary>
    /// <param name="item">设置项</param>
    void RegisterSetting(DynamicSettingItem item);

    /// <summary>
    /// 注册设置分组
    /// </summary>
    /// <param name="group">设置分组</param>
    void RegisterSettingGroup(DynamicSettingGroup group);

    /// <summary>
    /// 获取设置项
    /// </summary>
    /// <param name="key">设置键</param>
    /// <returns>设置项</returns>
    DynamicSettingItem? GetSetting(string key);

    /// <summary>
    /// 更新设置值
    /// </summary>
    /// <param name="key">设置键</param>
    /// <param name="value">新值</param>
    Task UpdateSettingAsync(string key, object? value);

    /// <summary>
    /// 加载设置值
    /// </summary>
    Task LoadSettingsAsync();

    /// <summary>
    /// 重置分组设置
    /// </summary>
    /// <param name="groupName">分组名称</param>
    Task ResetGroupAsync(string groupName);

    /// <summary>
    /// 重置所有设置
    /// </summary>
    Task ResetAllSettingsAsync();

    /// <summary>
    /// 搜索设置
    /// </summary>
    /// <param name="searchText">搜索文本</param>
    /// <returns>匹配的设置项</returns>
    List<DynamicSettingItem> SearchSettings(string searchText);

    /// <summary>
    /// 获取分组设置
    /// </summary>
    /// <param name="groupName">分组名称</param>
    /// <returns>分组中的所有设置</returns>
    List<DynamicSettingItem> GetGroupSettings(string groupName);

    Task<Dictionary<string, object?>> GetAllSettingsAsync();
}