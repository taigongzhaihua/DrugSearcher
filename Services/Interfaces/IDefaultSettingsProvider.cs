using DrugSearcher.Models;

namespace DrugSearcher.Services
{
    public interface IDefaultSettingsProvider
    {
        List<SettingDefinition> GetDefaultDefinitions();
        List<SettingItem> GetDefaultSettingItems();
    }
}