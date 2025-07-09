using DrugSearcher.Services;
using Microsoft.EntityFrameworkCore;

namespace DrugSearcher.Data;

public class ApplicationDbContextFactory(
    DbContextOptions<ApplicationDbContext> options,
    IDefaultSettingsProvider defaultSettingsProvider)
    : IApplicationDbContextFactory
{
    private bool _databaseInitialized;
    private readonly Lock _initLock = new();

    public ApplicationDbContext CreateDbContext()
    {
        return new ApplicationDbContext(options);
    }

    public async Task EnsureDatabaseCreatedAsync()
    {
        lock (_initLock)
        {
            if (_databaseInitialized)
                return;
        }

        await Task.Run(() =>
        {
            lock (_initLock)
            {
                if (_databaseInitialized)
                    return;

                using var context = CreateDbContext();
                context.Database.EnsureCreated();

                // 确保默认设置存在
                EnsureDefaultSettings(context);

                _databaseInitialized = true;
                Console.WriteLine("数据库初始化完成");
            }
        });
    }

    private void EnsureDefaultSettings(ApplicationDbContext context)
    {
        try
        {
            if (context.Settings.Any()) return;
            var defaultSettings = defaultSettingsProvider.GetDefaultSettingItems();
            context.Settings.AddRange(defaultSettings);
            context.SaveChanges();
            Console.WriteLine("已添加默认设置");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"确保默认设置时发生错误: {ex.Message}");
        }
    }
}