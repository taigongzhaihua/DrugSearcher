using Autofac;
using DrugSearcher.Data;
using DrugSearcher.Data.Interfaces;
using DrugSearcher.Managers;
using DrugSearcher.Repositories;
using DrugSearcher.Services;
using DrugSearcher.ViewModels;
using DrugSearcher.Views;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace DrugSearcher.Configuration;

public static class ContainerConfig
{
    public static IContainer Configure()
    {
        var builder = new ContainerBuilder();
        // 注册 DbContext 工厂而不是直接注册 DbContext
        builder.Register(_ =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            var dbPath = GetDatabasePath();
            var connectionString = $"Data Source={dbPath}";
            optionsBuilder.UseSqlite(connectionString);
            return optionsBuilder.Options;
        }).As<DbContextOptions<ApplicationDbContext>>().SingleInstance();

        // 注册 DrugSearcherDbContext（新增的）
        builder.Register(_ =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<DrugSearcherDbContext>();
            var dbPath = GetDrugDatabasePath(); // 可以使用不同的数据库文件
            var connectionString = $"Data Source={dbPath}";
            optionsBuilder.UseSqlite(connectionString);
            return optionsBuilder.Options;
        }).As<DbContextOptions<DrugSearcherDbContext>>().SingleInstance();

        // 注册 DbContext 工厂
        builder.RegisterType<ApplicationDbContextFactory>()
            .As<IApplicationDbContextFactory>()
            .SingleInstance();

        // 注册 DrugSearcherDbContext 工厂（新增的）
        builder.RegisterType<DrugSearcherDbContextFactory>()
            .As<IDrugSearcherDbContextFactory>()
            .SingleInstance();

        // 注册设置服务，使用 DbContext 工厂
        builder.RegisterType<UserSettingsService>()
            .As<IUserSettingsService>()
            .SingleInstance();


        // 注册默认设置提供者
        builder.RegisterType<DefaultSettingsProvider>()
            .As<IDefaultSettingsProvider>()
            .SingleInstance();

        // 注册设置服务
        builder.RegisterType<UserSettingsService>()
            .As<IUserSettingsService>()
            .SingleInstance();

        // 注册药物搜索服务
        builder.RegisterType<DrugSearchService>()
            .AsSelf()
            .SingleInstance();

        // 注册本地药物服务
        builder.RegisterType<LocalDrugService>()
            .As<ILocalDrugService>()
            .SingleInstance();

        // 注册Excel服务
        builder.RegisterType<ExcelService>()
            .As<IExcelService>()
            .SingleInstance();

        // 注册药物搜索服务
        builder.RegisterType<DrugSearchService>()
            .AsSelf()
            .SingleInstance();

        // 注册仓储类
        builder.RegisterType<DrugRepository>()
            .As<IDrugRepository>()
            .SingleInstance();

        //

        // 注册主题管理器
        builder.RegisterType<ThemeManager>()
            .AsSelf()
            .SingleInstance();

        // 注册ViewModels
        builder.RegisterType<MainWindowViewModel>()
            .AsSelf()
            .SingleInstance();
        builder.RegisterType<SettingsPageViewModel>()
            .AsSelf()
            .SingleInstance();
        builder.RegisterType<HomePageViewModel>()
            .AsSelf()
            .SingleInstance();

        // 注册窗口
        builder.RegisterType<MainWindow>()
            .AsSelf()
            .InstancePerDependency();

        // 注册页面
        builder.RegisterType<SettingsPage>()
            .AsSelf()
            .SingleInstance();
        builder.RegisterType<HomePage>()
            .AsSelf()
            .SingleInstance();


        return builder.Build();
    }

    private static string GetDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "DrugSearcher");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, "drugSearcher.db");
    }

    private static string GetDrugDatabasePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "DrugSearcher");
        Directory.CreateDirectory(appFolder);
        return Path.Combine(appFolder, "drugs.db"); // 使用不同的数据库文件
    }
}