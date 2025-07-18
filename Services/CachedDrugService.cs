using DrugSearcher.Models;

namespace DrugSearcher.Services;

public class CachedDrugService : ICachedDrugService
{
    public Task<List<BaseDrugInfo>> SearchCachedDrugsAsync(string keyword) => throw new NotImplementedException();

    public Task<BaseDrugInfo?> GetCachedDrugDetailAsync(int id) => throw new NotImplementedException();

    public Task UpdateCachedDrugAsync(BaseDrugInfo drugInfo) => throw new NotImplementedException();

    public Task<List<string>> GetCachedDrugNameSuggestionsAsync(string keyword) => throw new NotImplementedException();
}