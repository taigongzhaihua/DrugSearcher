namespace DrugSearcher.Data;

public interface IApplicationDbContextFactory
{
    ApplicationDbContext CreateDbContext();
    Task EnsureDatabaseCreatedAsync();
}