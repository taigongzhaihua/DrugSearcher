using DrugSearcher.Configuration;
using DrugSearcher.Data;
using DrugSearcher.Managers;
using DrugSearcher.Services;
using DrugSearcher.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher;

public partial class App
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        // 检查是否为第一个实例
        if (!SingleInstanceManager.IsFirstInstance())
        {
            SingleInstanceManager.NotifyFirstInstance();
            Current.Shutdown();
            return;
        }

        base.OnStartup(e);

        try
        {
            Console.WriteLine("正在启动应用程序...");

            // 配置并初始化依赖注入容器
            var container = ContainerConfig.Configure();
            ContainerAccessor.Initialize(container);
            Console.WriteLine("容器初始化完成");

            // 初始化数据库和服务
            await InitializeServicesAsync();

            // 初始化主题管理器
            var themeManager = ContainerAccessor.Resolve<ThemeManager>();
            themeManager.Initialize();
            Console.WriteLine("主题管理器初始化完成");

            // 手动创建并显示主窗口
            var mainWindow = ContainerAccessor.Resolve<MainWindow>();
            MainWindow = mainWindow;

            // 启动单实例管道监听
            SingleInstanceManager.StartListening(mainWindow);

            mainWindow.Show();

            Console.WriteLine("应用程序启动完成");
        }
        catch (Exception ex)
        {
            var errorMessage = $"应用程序启动失败: {ex.Message}";
            Console.WriteLine(errorMessage);
            Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");

            MessageBox.Show(errorMessage, "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static async Task InitializeServicesAsync()
    {
        try
        {
            Console.WriteLine("正在初始化数据库和服务...");

            // 使用数据库初始化服务
            var databaseInitService = ContainerAccessor.Resolve<IDatabaseInitializationService>();
            await databaseInitService.InitializeAsync();

            // 检查数据库状态
            var isReady = await databaseInitService.CheckDatabaseStatusAsync();
            if (!isReady)
            {
                Console.WriteLine("警告：数据库状态检查未通过");
                MessageBox.Show("数据库初始化可能存在问题，某些功能可能不可用。",
                    "数据库警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // 获取设置服务以触发其初始化
            ContainerAccessor.Resolve<IUserSettingsService>();

            // 等待一小段时间让设置服务完成异步初始化
            await Task.Delay(100);

            Console.WriteLine("所有服务初始化完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"服务初始化失败: {ex.Message}");
            MessageBox.Show($"服务初始化失败: {ex.Message}\n\n应用程序将继续启动，但某些功能可能不可用。",
                "初始化警告", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Console.WriteLine("正在退出应用程序...");

            // 释放ThemeManager资源
            if (ContainerAccessor.IsInitialized)
            {
                // 使用 GetService 替代 ResolveOptional
                var themeManager = ContainerAccessor.Container.GetService<ThemeManager>();
                themeManager?.Dispose();

                // 释放应用程序数据库上下文
                var appDbContext = ContainerAccessor.Container.GetService<ApplicationDbContext>();
                appDbContext?.Dispose();

                // 释放药物数据库上下文
                var drugDbContext = ContainerAccessor.Container.GetService<DrugDbContext>();
                drugDbContext?.Dispose();

                // 释放容器
                ContainerAccessor.Dispose();
            }

            // 清理单实例管理器
            SingleInstanceManager.Cleanup();

            Console.WriteLine("应用程序退出完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用程序退出时发生错误: {ex.Message}");
        }

        base.OnExit(e);
    }
}