using Autofac;
using DrugSearcher.Configuration;
using DrugSearcher.Data;
using DrugSearcher.Managers;
using DrugSearcher.Services;
using DrugSearcher.Views;
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

            // 配置并初始化Autofac容器
            var container = ContainerConfig.Configure();
            ContainerAccessor.Initialize(container);
            Console.WriteLine("容器初始化完成");

            // 等待数据库和设置服务初始化完成
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

            // 获取数据库工厂并确保数据库已创建
            var dbContextFactory = ContainerAccessor.Resolve<IApplicationDbContextFactory>();
            await dbContextFactory.EnsureDatabaseCreatedAsync();
            Console.WriteLine("数据库初始化完成");

            // 获取设置服务以触发其初始化
            var settingsService = ContainerAccessor.Resolve<IUserSettingsService>();

            // 等待一小段时间让设置服务完成异步初始化
            await Task.Delay(100);

            Console.WriteLine("服务初始化完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"服务初始化失败: {ex.Message}");
            // 不抛出异常，允许应用程序继续启动
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
                var themeManager = ContainerAccessor.Container.ResolveOptional<ThemeManager>();
                themeManager?.Dispose();

                // 释放数据库上下文
                var dbContext = ContainerAccessor.Container.ResolveOptional<ApplicationDbContext>();
                dbContext?.Dispose();

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