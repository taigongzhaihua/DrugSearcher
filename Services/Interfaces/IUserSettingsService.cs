using DrugSearcher.Models;

namespace DrugSearcher.Services;

public interface IUserSettingsService
{
    // 通用设置操作
    Task<T?> GetSettingAsync<T>(string key, T? defaultValue = default!);
    Task SetSettingAsync<T>(string key, T value);
    Task<bool> HasSettingAsync(string key);
    Task DeleteSettingAsync(string key);

    // 批量操作
    Task<Dictionary<string, object?>> GetSettingsByCategoryAsync(string category);
    Task<Dictionary<string, object?>> GetAllSettingsAsync();
    Task SetSettingsAsync(Dictionary<string, object?> settings);

    // 设置定义管理
    Task RegisterSettingDefinitionAsync(SettingDefinition definition);
    Task<SettingDefinition?> GetSettingDefinitionAsync(string key);
    Task<List<SettingDefinition>> GetSettingDefinitionsAsync();

    // 重置和导入导出
    Task ResetToDefaultsAsync();
    Task ResetCategoryToDefaultsAsync(string category);
    Task<string> ExportSettingsAsync();
    Task ImportSettingsAsync(string json);

    // 事件
    event EventHandler<SettingChangedEventArgs>? SettingChanged;
    event EventHandler? SettingsReloaded;
}