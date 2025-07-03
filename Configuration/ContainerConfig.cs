using Autofac;
using DrugSearcher.Data;
using DrugSearcher.Managers;
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

        // 注册 DbContext 工厂
        builder.RegisterType<ApplicationDbContextFactory>()
            .As<IApplicationDbContextFactory>()
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
}