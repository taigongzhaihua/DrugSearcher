using Autofac;
using DrugSearcher.Data;
using DrugSearcher.Managers;
using DrugSearcher.Repositories;
using DrugSearcher.Services;
using DrugSearcher.ViewModels;
using DrugSearcher.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;

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
            RegisterLoggingServices(builder);
            RegisterDatabaseServices(builder);
            RegisterRepositories(builder);
            RegisterBusinessServices(builder);
            RegisterSettingsServices(builder); // 新增：注册设置服务
            RegisterViewModels(builder);
            RegisterViews(builder);
            RegisterManagers(builder);

            var container = builder.Build();

            // 初始化动态设置系统
            InitializeDynamicSettings(container);

            return container;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"容器配置失败: {ex.Message}");
            throw new InvalidOperationException($"依赖注入容器配置失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 注册日志服务
    /// </summary>
    private static void RegisterLoggingServices(ContainerBuilder builder)
    {
        // 注册 ILoggerFactory
        builder.Register<ILoggerFactory>(_ =>
        {
            var loggerFactory = LoggerFactory.Create(config =>
            {
                config.AddConsole();
                config.AddDebug();
                config.SetMinimumLevel(LogLevel.Information);

                // 开发环境下启用更详细的日志
#if DEBUG
                config.SetMinimumLevel(LogLevel.Debug);
#endif
            });

            return loggerFactory;
        }).As<ILoggerFactory>().SingleInstance();

        // 注册泛型 ILogger<T>
        builder.RegisterGeneric(typeof(Logger<>))
            .As(typeof(ILogger<>))
            .SingleInstance();

        // 注册 ILogger (非泛型版本)
        builder.Register(context =>
        {
            var loggerFactory = context.Resolve<ILoggerFactory>();
            return loggerFactory.CreateLogger("DrugSearcher");
        }).As<ILogger>().SingleInstance();
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

#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.LogTo(message => System.Diagnostics.Debug.WriteLine(message), LogLevel.Information);
#endif

            return optionsBuilder.Options;
        }).As<DbContextOptions<ApplicationDbContext>>().SingleInstance();

        // 注册药物数据库上下文配置
        builder.Register(_ =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<DrugDbContext>();
            var dbPath = GetDrugDatabasePath();
            var connectionString = $"Data Source={dbPath}";
            optionsBuilder.UseSqlite(connectionString);

#if DEBUG
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.LogTo(message => System.Diagnostics.Debug.WriteLine(message), LogLevel.Information);
#endif

            return optionsBuilder.Options;
        }).As<DbContextOptions<DrugDbContext>>().SingleInstance();

        // 注册数据库工厂
        builder.RegisterType<ApplicationDbContextFactory>()
            .As<IApplicationDbContextFactory>()
            .SingleInstance();

        builder.RegisterType<DrugDbContextFactory>()
            .As<IDrugDbContextFactory>()
            .SingleInstance();
    }

    /// <summary>
    /// 注册仓储层
    /// </summary>
    private static void RegisterRepositories(ContainerBuilder builder)
    {
        // 注册适配器保持向后兼容
        builder.RegisterType<DrugRepositoryAdapter>()
            .As<IDrugRepository>()
            .InstancePerLifetimeScope();

        builder.RegisterType<OnlineDrugRepositoryAdapter>()
            .As<IOnlineDrugRepository>()
            .InstancePerLifetimeScope();
    }

    /// <summary>
    /// 注册业务服务层
    /// </summary>
    private static void RegisterBusinessServices(ContainerBuilder builder)
    {
        // 注册 HttpClient
        builder.Register(_ =>
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5) // 设置超时时间
            };
            return httpClient;
        }).As<HttpClient>().InstancePerLifetimeScope();

        // 快捷键服务 - 全局单例
        builder.RegisterType<HotKeyService>()
            .As<IHotKeyService>()
            .SingleInstance();

        // 数据处理服务 - 根据使用场景选择生命周期
        builder.RegisterType<ExcelService>()
            .As<IExcelService>()
            .InstancePerLifetimeScope(); // Excel 处理可能消耗资源，使用作用域

        builder.RegisterType<LocalDrugService>()
            .As<ILocalDrugService>()
            .InstancePerLifetimeScope(); // 数据操作服务使用作用域

        builder.RegisterType<YaozsOnlineDrugService>()
            .As<IOnlineDrugService>()
            .InstancePerLifetimeScope(); // 在线药物服务使用作用域

        builder.RegisterType<CachedDrugService>()
            .As<ICachedDrugService>()
            .InstancePerLifetimeScope(); // 缓存服务使用作用域

        // 药物搜索服务 - 聚合服务
        builder.RegisterType<DrugSearchService>()
            .AsSelf()
            .InstancePerLifetimeScope(); // 改为作用域，避免状态混乱

        builder.RegisterType<DatabaseInitializationService>()
            .As<IDatabaseInitializationService>()
            .SingleInstance();

        builder.RegisterType<VersionService>()
            .As<IVersionService>()
            .SingleInstance();
    }

    /// <summary>
    /// 注册设置相关服务
    /// </summary>
    private static void RegisterSettingsServices(ContainerBuilder builder)
    {
        // 注册默认设置提供程序
        builder.RegisterType<DefaultSettingsProvider>()
            .As<IDefaultSettingsProvider>()
            .SingleInstance();

        // 注册用户设置服务 - 全局单例
        builder.RegisterType<UserSettingsService>()
            .As<IUserSettingsService>()
            .SingleInstance();

        // 注册动态设置服务 - 全局单例
        builder.RegisterType<DynamicSettingsService>()
            .As<IDynamicSettingsService>()
            .SingleInstance();
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

        builder.RegisterType<CrawlerPageViewModel>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<AboutPageViewModel>()
            .AsSelf()
            .SingleInstance();
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

        // 注册爬虫页面
        builder.RegisterType<CrawlerPage>()
            .AsSelf()
            .SingleInstance(); // 爬虫页面通常是全局单例

        builder.RegisterType<AboutPage>()
            .AsSelf()
            .SingleInstance();
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

        builder.Register(context =>
        {
            // 这里我们需要从容器中解析 MainWindow
            var mainWindow = context.Resolve<MainWindow>();
            return new HotKeyManager(mainWindow);
        })
            .AsSelf()
            .SingleInstance();
        // 其他管理器可以在这里添加
        // builder.RegisterType<ConfigurationManager>()
        //     .AsSelf()
        //     .SingleInstance();
    }

    /// <summary>
    /// 初始化动态设置系统
    /// </summary>
    private static void InitializeDynamicSettings(IContainer container)
    {
        try
        {
            var dynamicSettingsService = container.Resolve<IDynamicSettingsService>();

            // 可选：注册搜索设置（如果你需要的话）
            // dynamicSettingsService.RegisterSearchSettings();

            // 异步加载设置值
            _ = Task.Run(async () =>
            {
                try
                {
                    await dynamicSettingsService.LoadSettingsAsync();
                    System.Diagnostics.Debug.WriteLine("动态设置系统初始化完成");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"动态设置系统加载失败: {ex.Message}");
                }
            });

            System.Diagnostics.Debug.WriteLine("动态设置服务注册完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"初始化动态设置失败: {ex.Message}");
        }
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