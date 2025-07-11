namespace DrugSearcher.Services;

public interface IDatabaseInitializationService
{
    Task InitializeAsync();
    Task<bool> CheckDatabaseStatusAsync();
}