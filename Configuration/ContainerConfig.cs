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

/// <summary>
/// Autofac 依赖注入容器配置
/// </summary>
public static class ContainerConfig
{
    /// <summary>
    /// 配置依赖注入容器
    /// </summary>
    /// <returns>配置好的容器实例</returns>
    public static IContainer Configure()
    {
        var builder = new ContainerBuilder();

        try
        {
            // 按类别注册各种依赖
            RegisterDatabaseServices(builder);
            RegisterRepositories(builder);
            RegisterBusinessServices(builder);
            RegisterViewModels(builder);
            RegisterViews(builder);
            RegisterManagers(builder);

            return builder.Build();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"容器配置失败: {ex.Message}");
            throw new InvalidOperationException($"依赖注入容器配置失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 注册数据库相关服务
    /// </summary>
    private static void RegisterDatabaseServices(ContainerBuilder builder)
    {
        // 注册 ApplicationDbContext 配置
        builder.Register(_ =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            var dbPath = GetDatabasePath();
            var connectionString = $"Data Source={dbPath}";
            optionsBuilder.UseSqlite(connectionString);

            // 开发环境配置
#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.LogTo(message => System.Diagnostics.Debug.WriteLine(message), Microsoft.Extensions.Logging.LogLevel.Information);
#endif

            return optionsBuilder.Options;
        }).As<DbContextOptions<ApplicationDbContext>>().SingleInstance();

        // 注册 DrugSearcherDbContext 配置
        builder.Register(_ =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<DrugSearcherDbContext>();
            var dbPath = GetDrugDatabasePath();
            var connectionString = $"Data Source={dbPath}";
            optionsBuilder.UseSqlite(connectionString);

            // 开发环境配置
#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();

            // 修改以下代码以解决 CS1618 错误  
            optionsBuilder.LogTo(message => System.Diagnostics.Debug.WriteLine(message), Microsoft.Extensions.Logging.LogLevel.Information);
#endif

            return optionsBuilder.Options;
        }).As<DbContextOptions<DrugSearcherDbContext>>().SingleInstance();

        // 注册 DbContext 工厂
        builder.RegisterType<ApplicationDbContextFactory>()
            .As<IApplicationDbContextFactory>()
            .SingleInstance();

        builder.RegisterType<DrugSearcherDbContextFactory>()
            .As<IDrugSearcherDbContextFactory>()
            .SingleInstance();
    }

    /// <summary>
    /// 注册仓储层
    /// </summary>
    private static void RegisterRepositories(ContainerBuilder builder)
    {
        builder.RegisterType<DrugRepository>()
            .As<IDrugRepository>()
            .InstancePerLifetimeScope(); // 改为 InstancePerLifetimeScope，避免并发问题
    }

    /// <summary>
    /// 注册业务服务层
    /// </summary>
    private static void RegisterBusinessServices(ContainerBuilder builder)
    {
        // 设置相关服务 - 全局单例
        builder.RegisterType<DefaultSettingsProvider>()
            .As<IDefaultSettingsProvider>()
            .SingleInstance();

        builder.RegisterType<UserSettingsService>()
            .As<IUserSettingsService>()
            .SingleInstance();

        // 数据处理服务 - 根据使用场景选择生命周期
        builder.RegisterType<ExcelService>()
            .As<IExcelService>()
            .InstancePerLifetimeScope(); // Excel 处理可能消耗资源，使用作用域

        builder.RegisterType<LocalDrugService>()
            .As<ILocalDrugService>()
            .InstancePerLifetimeScope(); // 数据操作服务使用作用域

        // 在线服务和缓存服务（如果需要的话）
        builder.RegisterType<OnlineDrugService>()
            .As<IOnlineDrugService>()
            .InstancePerLifetimeScope();

        builder.RegisterType<CachedDrugService>()
            .As<ICachedDrugService>()
            .InstancePerLifetimeScope();

        // 药物搜索服务 - 聚合服务
        builder.RegisterType<DrugSearchService>()
            .AsSelf()
            .InstancePerLifetimeScope(); // 改为作用域，避免状态混乱
    }

    /// <summary>
    /// 注册视图模型
    /// </summary>
    private static void RegisterViewModels(ContainerBuilder builder)
    {
        // 主窗口 ViewModel - 全局单例
        builder.RegisterType<MainWindowViewModel>()
            .AsSelf()
            .SingleInstance();

        // 设置页面 ViewModel - 全局单例
        builder.RegisterType<SettingsPageViewModel>()
            .AsSelf()
            .SingleInstance();

        // 首页页面 ViewModels - 全局单例
        builder.RegisterType<HomePageViewModel>()
            .AsSelf()
            .SingleInstance();

        // 注册本地数据管理ViewModel
        builder.RegisterType<LocalDataManagementViewModel>()
            .AsSelf()
            .SingleInstance();

        // 注册药物编辑对话框ViewModel
        builder.RegisterType<DrugEditDialogViewModel>()
            .AsSelf()
            .InstancePerDependency();

    }

    /// <summary>
    /// 注册视图
    /// </summary>
    private static void RegisterViews(ContainerBuilder builder)
    {
        // 主窗口 - 全局单例
        builder.RegisterType<MainWindow>()
            .AsSelf()
            .SingleInstance();

        // 设置页面 - 全局单例（通常设置页面可以复用）
        builder.RegisterType<SettingsPage>()
            .AsSelf()
            .SingleInstance();

        // 首页页面 - 全局单例
        builder.RegisterType<HomePage>()
            .AsSelf()
            .SingleInstance();

        // 注册本地数据管理页面
        builder.RegisterType<LocalDataManagementPage>()
            .AsSelf()
            .SingleInstance();

        // 注册药物编辑对话框
        builder.RegisterType<DrugEditDialog>()
            .AsSelf()
            .InstancePerDependency(); // 对话框通常是临时的，使用 InstancePerDependency

    }

    /// <summary>
    /// 注册管理器和工具类
    /// </summary>
    private static void RegisterManagers(ContainerBuilder builder)
    {
        // 主题管理器 - 全局单例
        builder.RegisterType<ThemeManager>()
            .AsSelf()
            .SingleInstance();

        // 其他管理器可以在这里添加
        // builder.RegisterType<ConfigurationManager>()
        //     .AsSelf()
        //     .SingleInstance();
    }

    /// <summary>
    /// 获取应用程序数据库路径
    /// </summary>
    private static string GetDatabasePath()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "DrugSearcher");

            EnsureDirectoryExists(appFolder);

            var dbPath = Path.Combine(appFolder, "drugSearcher.db");
            LogDatabasePath("应用程序数据库", dbPath);

            return dbPath;
        }
        catch (Exception ex)
        {
            LogError("获取应用程序数据库路径失败", ex);
            return GetFallbackDatabasePath("drugSearcher.db");
        }
    }

    /// <summary>
    /// 获取药物数据库路径
    /// </summary>
    private static string GetDrugDatabasePath()
    {
        try
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "DrugSearcher");

            EnsureDirectoryExists(appFolder);

            var dbPath = Path.Combine(appFolder, "drugs.db");
            LogDatabasePath("药物数据库", dbPath);

            return dbPath;
        }
        catch (Exception ex)
        {
            LogError("获取药物数据库路径失败", ex);
            return GetFallbackDatabasePath("drugs.db");
        }
    }

    /// <summary>
    /// 确保目录存在
    /// </summary>
    private static void EnsureDirectoryExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath)) return;
        Directory.CreateDirectory(directoryPath);
        System.Diagnostics.Debug.WriteLine($"创建目录: {directoryPath}");
    }

    /// <summary>
    /// 获取备用数据库路径
    /// </summary>
    private static string GetFallbackDatabasePath(string fileName)
    {
        var fallbackPath = Path.Combine(Environment.CurrentDirectory, fileName);
        System.Diagnostics.Debug.WriteLine($"使用备用数据库路径: {fallbackPath}");
        return fallbackPath;
    }

    /// <summary>
    /// 记录数据库路径
    /// </summary>
    private static void LogDatabasePath(string dbType, string path)
    {
        System.Diagnostics.Debug.WriteLine($"{dbType}路径: {path}");
    }

    /// <summary>
    /// 记录错误信息
    /// </summary>
    private static void LogError(string message, Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"{message}: {ex.Message}");
    }
}