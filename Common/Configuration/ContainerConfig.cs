using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using DrugSearcher.Data;
using DrugSearcher.Managers;
using DrugSearcher.Repositories;
using DrugSearcher.Services;
using DrugSearcher.ViewModels;
using DrugSearcher.Views;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace DrugSearcher.Configuration;

/// <summary>
/// Microsoft.Extensions.DependencyInjection 依赖注入容器配置
/// </summary>
public static class ContainerConfig
{
    /// <summary>
    /// 配置依赖注入容器
    /// </summary>
    /// <returns>配置好的容器实例</returns>
    public static IServiceProvider Configure()
    {
        var services = new ServiceCollection();

        try
        {
            // 按类别注册各种依赖
            RegisterLoggingServices(services);
            RegisterDatabaseServices(services);
            RegisterRepositories(services);
            RegisterCacheServices(services);
            RegisterBusinessServices(services);
            RegisterSettingsServices(services);
            RegisterViewModels(services);
            RegisterViews(services);
            RegisterManagers(services);

            var serviceProvider = services.BuildServiceProvider();

            // 初始化动态设置系统
            InitializeDynamicSettings(serviceProvider);

            return serviceProvider;
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
    private static void RegisterLoggingServices(IServiceCollection services)
    {
        // 注册日志服务
        services.AddLogging(config =>
        {
            config.AddConsole();
            config.AddDebug();

#if DEBUG
            config.SetMinimumLevel(LogLevel.Debug);
#else
            config.SetMinimumLevel(LogLevel.Information);
#endif
        });
    }

    /// <summary>
    /// 注册数据库相关服务
    /// </summary>
    private static void RegisterDatabaseServices(IServiceCollection services)
    {
        // 注册 ApplicationDbContext 配置
        services.AddSingleton(provider =>
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
        });

        // 注册药物数据库上下文配置
        services.AddSingleton(provider =>
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
        });

        // 注册数据库上下文（作为 Transient，每次请求创建新实例）
        services.AddTransient<ApplicationDbContext>();
        services.AddTransient<DrugDbContext>();

        // 注册数据库工厂
        services.AddSingleton<IApplicationDbContextFactory, ApplicationDbContextFactory>();
        services.AddSingleton<IDrugDbContextFactory, DrugDbContextFactory>();
    }

    /// <summary>
    /// 注册仓储层
    /// </summary>
    private static void RegisterRepositories(IServiceCollection services)
    {
        // 注册适配器保持向后兼容
        services.AddScoped<IDrugRepository, DrugRepository>();
        services.AddScoped<IOnlineDrugRepository, OnlineDrugRepository>();
        services.AddScoped<IDosageCalculatorRepository, DosageCalculatorRepository>();
    }

    /// <summary>
    /// 注册缓存服务
    /// </summary>
    private static void RegisterCacheServices(IServiceCollection services)
    {
        // 注册 MemoryCache
        services.AddMemoryCache(options =>
        {
            options.CompactionPercentage = 0.05; // 缓存压缩百分比
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5); // 过期扫描频率
        });
    }

    /// <summary>
    /// 注册业务服务层
    /// </summary>
    private static void RegisterBusinessServices(IServiceCollection services)
    {
        // 注册 HttpClient
        services.AddHttpClient();

        // 为 VersionService 注册专用的 HttpClient
        services.AddHttpClient<IVersionService, VersionService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // 注册通用 HttpClient（用于其他服务）
        services.AddScoped(provider =>
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            return httpClientFactory.CreateClient();
        });

        // 快捷键服务 - 全局单例
        services.AddSingleton<IHotKeyService, HotKeyService>();

        // 数据处理服务 - 根据使用场景选择生命周期
        services.AddScoped<IExcelService, ExcelService>();
        services.AddScoped<ILocalDrugService, LocalDrugService>();
        services.AddScoped<IOnlineDrugService, YaozsOnlineDrugService>();

        // 药物搜索服务 - 聚合服务
        services.AddScoped<DrugSearchService>();

        services.AddSingleton<IDatabaseInitializationService, DatabaseInitializationService>();
        services.AddSingleton<JavaScriptDosageCalculatorService>();
        services.AddSingleton<DosageCalculatorAiService>();
    }

    /// <summary>
    /// 注册设置相关服务
    /// </summary>
    private static void RegisterSettingsServices(IServiceCollection services)
    {
        // 注册默认设置提供程序
        services.AddSingleton<IDefaultSettingsProvider, DefaultSettingsProvider>();

        // 注册用户设置服务 - 全局单例
        services.AddSingleton<IUserSettingsService, UserSettingsService>();

        // 注册动态设置服务 - 全局单例
        services.AddSingleton<IDynamicSettingsService, DynamicSettingsService>();
    }

    /// <summary>
    /// 注册视图模型
    /// </summary>
    private static void RegisterViewModels(IServiceCollection services)
    {
        // 主窗口 ViewModel - 全局单例
        services.AddSingleton<MainWindowViewModel>();

        // 设置页面 ViewModel - 全局单例
        services.AddSingleton<SettingsPageViewModel>();

        // 首页页面 ViewModels - 全局单例
        services.AddSingleton<HomePageViewModel>();

        // 注册本地数据管理ViewModel
        services.AddSingleton<LocalDataManagementViewModel>();

        // 注册药物编辑对话框ViewModel
        services.AddTransient<DrugEditDialogViewModel>();

        services.AddSingleton<CrawlerPageViewModel>();
        services.AddSingleton<AboutPageViewModel>();
        services.AddScoped<DosageParameterViewModel>();
    }

    /// <summary>
    /// 注册视图
    /// </summary>
    private static void RegisterViews(IServiceCollection services)
    {
        // 主窗口 - 全局单例
        services.AddSingleton<MainWindow>();

        // 设置页面 - 全局单例（通常设置页面可以复用）
        services.AddSingleton<SettingsPage>();

        // 首页页面 - 全局单例
        services.AddSingleton<HomePage>();

        // 注册本地数据管理页面
        services.AddSingleton<LocalDataManagementPage>();

        // 注册药物编辑对话框
        services.AddTransient<DrugEditDialog>();

        // 注册爬虫页面
        services.AddSingleton<CrawlerPage>();

        services.AddSingleton<AboutPage>();
    }

    /// <summary>
    /// 注册管理器和工具类
    /// </summary>
    private static void RegisterManagers(IServiceCollection services)
    {
        // 主题管理器 - 全局单例
        services.AddSingleton<ThemeManager>();

        services.AddSingleton(provider =>
        {
            // 这里我们需要从容器中解析 MainWindow
            var mainWindow = provider.GetRequiredService<MainWindow>();
            return new HotKeyManager(mainWindow);
        });
    }

    /// <summary>
    /// 初始化动态设置系统
    /// </summary>
    private static void InitializeDynamicSettings(IServiceProvider serviceProvider)
    {
        try
        {
            var dynamicSettingsService = serviceProvider.GetRequiredService<IDynamicSettingsService>();

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
    private static void LogDatabasePath(string dbType, string path) =>
        System.Diagnostics.Debug.WriteLine($"{dbType}路径: {path}");

    /// <summary>
    /// 记录错误信息
    /// </summary>
    private static void LogError(string message, Exception ex) =>
        System.Diagnostics.Debug.WriteLine($"{message}: {ex.Message}");
}