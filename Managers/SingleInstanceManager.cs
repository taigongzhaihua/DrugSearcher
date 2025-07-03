using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Application = System.Windows.Application;

namespace DrugSearcher.Managers;

/// <summary>
/// 单实例管理器，确保应用程序只能运行一个实例
/// 当尝试启动第二个实例时，会激活第一个实例的窗口
/// </summary>
[SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
[SuppressMessage("ReSharper", "MethodHasAsyncOverload")]
public static partial class SingleInstanceManager
{
    #region 私有字段

    private static Mutex? _mutex;
    private static CancellationTokenSource? _cancellationTokenSource;
    private static bool _isListening;

    #endregion

    #region 常量定义

    private const string AppName = "DrugSearcher_SingleInstance";
    private const string PipeName = "DrugSearcher_ActivationPipe";
    private const string ActivationMessage = "ACTIVATE";

    // 窗口显示常量
    private const int SwRestore = 9;
    private const int SwShowNormal = 1;

    // 窗口闪烁常量
    private const uint FlashwAll = 3;
    private const uint FlashwTimerNoFg = 12;
    private const uint FlashCount = 3;

    // 超时常量
    private const int PipeConnectionTimeout = 2000;
    private const int IoErrorRetryDelay = 2000;
    private const int GeneralErrorRetryDelay = 1000;

    #endregion

    #region Win32 API 声明

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BringWindowToTop(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr SetActiveWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr SetFocus(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    private static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlashWindowEx(ref FlashWindowInfo pwfi);

    /// <summary>
    /// 窗口闪烁信息结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FlashWindowInfo
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    #endregion

    #region 公共方法

    /// <summary>
    /// 检查当前是否为第一个实例
    /// </summary>
    /// <returns>如果是第一个实例则返回true</returns>
    public static bool IsFirstInstance()
    {
        try
        {
            _mutex = new Mutex(true, AppName, out var createdNew);

            Debug.WriteLine(createdNew ? "这是第一个应用程序实例" : "检测到应用程序已在运行");

            return createdNew;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"创建互斥锁时出错: {ex.Message}");
            // 如果出错，允许启动以避免阻止用户使用应用程序
            return true;
        }
    }

    /// <summary>
    /// 通知第一个实例激活窗口
    /// </summary>
    public static void NotifyFirstInstance()
    {
        try
        {
            Debug.WriteLine("正在通知第一个实例激活窗口...");

            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);

            // 连接到管道服务器
            pipeClient.Connect(PipeConnectionTimeout);

            // 发送激活消息
            var message = Encoding.UTF8.GetBytes(ActivationMessage);
            pipeClient.Write(message, 0, message.Length);
            pipeClient.Flush();

            Debug.WriteLine("激活通知发送成功");
        }
        catch (TimeoutException)
        {
            Debug.WriteLine("连接管道服务器超时");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"通知第一个实例失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 开始监听激活请求
    /// </summary>
    /// <param name="mainWindow">主窗口实例</param>
    public static void StartListening(Window mainWindow)
    {
        if (_isListening)
        {
            Debug.WriteLine("管道服务器已在监听中");
            return;
        }

        try
        {
            _isListening = true;
            _cancellationTokenSource = new CancellationTokenSource();

            Debug.WriteLine("启动管道服务器监听...");

            // 在后台线程中启动监听
            _ = Task.Run(async () =>
            {
                await ListenForActivationRequestsAsync(mainWindow, _cancellationTokenSource.Token);
            }, _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动监听失败: {ex.Message}");
            _isListening = false;
        }
    }

    /// <summary>
    /// 强制激活窗口（公共接口）
    /// </summary>
    /// <param name="window">要激活的窗口</param>
    public static void ForceActivateWindow(Window window)
    {
        try
        {
            Debug.WriteLine("强制激活窗口请求");
            ActivateWindow(window);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"强制激活窗口失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public static void Cleanup()
    {
        try
        {
            Debug.WriteLine("正在清理单实例管理器资源...");

            StopListening();
            ReleaseMutex();

            Debug.WriteLine("单实例管理器资源清理完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清理资源时出错: {ex.Message}");
        }
    }

    #endregion

    #region 私有方法 - 监听和通信

    /// <summary>
    /// 异步监听激活请求
    /// </summary>
    /// <param name="mainWindow">主窗口实例</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async Task ListenForActivationRequestsAsync(Window mainWindow, CancellationToken cancellationToken)
    {
        Debug.WriteLine("管道服务器开始监听激活请求...");

        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream? currentPipeServer = null;
            try
            {
                // 创建新的管道服务器实例
                currentPipeServer = CreatePipeServer();

                Debug.WriteLine("等待管道客户端连接...");

                // 等待客户端连接
                await currentPipeServer.WaitForConnectionAsync(cancellationToken);

                Debug.WriteLine("管道客户端已连接");

                // 处理激活请求
                await ProcessActivationRequestAsync(currentPipeServer, mainWindow, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("管道服务器操作被取消");
                break;
            }
            catch (ObjectDisposedException)
            {
                Debug.WriteLine("管道服务器已被释放");
                break;
            }
            catch (IOException ioEx)
            {
                Debug.WriteLine($"管道服务器IO错误: {ioEx.Message}");
                await Task.Delay(IoErrorRetryDelay, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"管道服务器错误: {ex.Message}");
                await Task.Delay(GeneralErrorRetryDelay, cancellationToken);
            }
            finally
            {
                // 确保管道正确关闭和释放
                DisposePipeServer(currentPipeServer);
            }
        }

        _isListening = false;
        Debug.WriteLine("管道服务器监听已停止");
    }

    /// <summary>
    /// 创建管道服务器
    /// </summary>
    /// <returns>管道服务器实例</returns>
    private static NamedPipeServerStream CreatePipeServer()
    {
        return new NamedPipeServerStream(
            PipeName,
            PipeDirection.In,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    /// <summary>
    /// 处理激活请求
    /// </summary>
    /// <param name="pipeServer">管道服务器</param>
    /// <param name="mainWindow">主窗口</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async Task ProcessActivationRequestAsync(
        NamedPipeServerStream pipeServer,
        Window mainWindow,
        CancellationToken cancellationToken)
    {
        try
        {
            // 读取消息
            var buffer = new byte[256];
            var bytesRead = await pipeServer.ReadAsync(buffer, cancellationToken);
            var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            Debug.WriteLine($"收到消息: {message}");

            if (message == ActivationMessage)
            {
                // 在UI线程上激活窗口
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        ActivateWindow(mainWindow);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"在UI线程激活窗口时出错: {ex.Message}");
                    }
                });
            }
            else
            {
                Debug.WriteLine($"收到未知消息: {message}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"处理激活请求失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 释放管道服务器资源
    /// </summary>
    /// <param name="pipeServer">管道服务器实例</param>
    private static void DisposePipeServer(NamedPipeServerStream? pipeServer)
    {
        if (pipeServer == null) return;

        try
        {
            if (pipeServer.IsConnected)
            {
                pipeServer.Disconnect();
            }
            pipeServer.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放管道服务器资源失败: {ex.Message}");
        }
    }

    #endregion

    #region 私有方法 - 窗口激活

    /// <summary>
    /// 激活窗口的核心实现
    /// </summary>
    /// <param name="window">要激活的窗口</param>
    private static void ActivateWindow(Window window)
    {
        try
        {
            Debug.WriteLine("开始激活窗口...");

            var windowHandle = GetWindowHandle(window);
            if (windowHandle == IntPtr.Zero)
            {
                Debug.WriteLine("无法获取窗口句柄，激活失败");
                return;
            }

            Debug.WriteLine($"窗口句柄: {windowHandle}");

            // 执行窗口激活步骤
            PerformWindowActivationSteps(window, windowHandle);

            Debug.WriteLine("窗口激活完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"激活窗口时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取窗口句柄
    /// </summary>
    /// <param name="window">窗口实例</param>
    /// <returns>窗口句柄</returns>
    private static IntPtr GetWindowHandle(Window window)
    {
        var windowHandle = new WindowInteropHelper(window).Handle;

        if (windowHandle == IntPtr.Zero)
        {
            Debug.WriteLine("窗口句柄为空，尝试确保窗口已加载...");

            // 如果窗口句柄为空，尝试确保窗口已加载
            if (!window.IsLoaded)
            {
                window.Show();
                window.UpdateLayout();
                windowHandle = new WindowInteropHelper(window).Handle;
            }

            if (windowHandle == IntPtr.Zero)
            {
                Debug.WriteLine("仍然无法获取窗口句柄");
            }
        }

        return windowHandle;
    }

    /// <summary>
    /// 执行窗口激活步骤
    /// </summary>
    /// <param name="window">窗口实例</param>
    /// <param name="windowHandle">窗口句柄</param>
    private static void PerformWindowActivationSteps(Window window, IntPtr windowHandle)
    {
        // 1. 恢复最小化的窗口
        RestoreMinimizedWindow(window, windowHandle);

        // 2. 确保窗口可见
        EnsureWindowVisible(window, windowHandle);

        // 3. 强制获取焦点
        ForceWindowToForeground(windowHandle);

        // 4. WPF层面的激活
        ActivateWindowInWpf(window);

        // 5. 如果仍然无法获取焦点，闪烁窗口提醒用户
        NotifyUserIfActivationFailed(windowHandle);
    }

    /// <summary>
    /// 恢复最小化的窗口
    /// </summary>
    /// <param name="window">窗口实例</param>
    /// <param name="windowHandle">窗口句柄</param>
    private static void RestoreMinimizedWindow(Window window, IntPtr windowHandle)
    {
        if (window.WindowState == WindowState.Minimized || IsIconic(windowHandle))
        {
            Debug.WriteLine("窗口已最小化，正在恢复...");
            window.WindowState = WindowState.Normal;
            ShowWindow(windowHandle, SwRestore);
        }
    }

    /// <summary>
    /// 确保窗口可见
    /// </summary>
    /// <param name="window">窗口实例</param>
    /// <param name="windowHandle">窗口句柄</param>
    private static void EnsureWindowVisible(Window window, IntPtr windowHandle)
    {
        window.Show();
        window.ShowInTaskbar = true;
        ShowWindow(windowHandle, SwShowNormal);
    }

    /// <summary>
    /// 在WPF层面激活窗口
    /// </summary>
    /// <param name="window">窗口实例</param>
    private static void ActivateWindowInWpf(Window window)
    {
        try
        {
            window.Activate();
            window.Focus();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WPF窗口激活失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 如果激活失败则通知用户
    /// </summary>
    /// <param name="windowHandle">窗口句柄</param>
    private static void NotifyUserIfActivationFailed(IntPtr windowHandle)
    {
        if (GetForegroundWindow() != windowHandle)
        {
            Debug.WriteLine("窗口仍未获得焦点，闪烁提醒用户...");
            FlashWindowToNotify(windowHandle);
        }
    }

    /// <summary>
    /// 强制窗口到前台
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    private static void ForceWindowToForeground(IntPtr hWnd)
    {
        try
        {
            var foregroundWindow = GetForegroundWindow();
            var currentThreadId = GetCurrentThreadId();
            var foregroundThreadId = GetWindowThreadProcessId(foregroundWindow, out _);

            Debug.WriteLine($"当前线程: {currentThreadId}, 前台线程: {foregroundThreadId}");

            if (ShouldAttachThreadInput(foregroundThreadId, currentThreadId))
            {
                AttachThreadInputAndSetForeground(hWnd, foregroundThreadId, currentThreadId);
            }
            else
            {
                SetForegroundDirectly(hWnd);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"强制窗口到前台时出错: {ex.Message}");

            // 使用备用方法
            TryBackupActivationMethod(hWnd);
        }
    }

    /// <summary>
    /// 检查是否需要附加线程输入
    /// </summary>
    /// <param name="foregroundThreadId">前台线程ID</param>
    /// <param name="currentThreadId">当前线程ID</param>
    /// <returns>如果需要附加则返回true</returns>
    private static bool ShouldAttachThreadInput(uint foregroundThreadId, uint currentThreadId)
    {
        return foregroundThreadId != currentThreadId && foregroundThreadId != 0;
    }

    /// <summary>
    /// 附加线程输入并设置前台
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="foregroundThreadId">前台线程ID</param>
    /// <param name="currentThreadId">当前线程ID</param>
    private static void AttachThreadInputAndSetForeground(IntPtr hWnd, uint foregroundThreadId, uint currentThreadId)
    {
        var attached = AttachThreadInput(foregroundThreadId, currentThreadId, true);
        Debug.WriteLine($"线程输入已附加: {attached}");

        try
        {
            SetForegroundDirectly(hWnd);
        }
        finally
        {
            // 分离线程输入
            if (attached)
            {
                AttachThreadInput(foregroundThreadId, currentThreadId, false);
                Debug.WriteLine("线程输入已分离");
            }
        }
    }

    /// <summary>
    /// 直接设置前台窗口
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    private static void SetForegroundDirectly(IntPtr hWnd)
    {
        Debug.WriteLine("直接设置前台窗口");
        BringWindowToTop(hWnd);
        SetForegroundWindow(hWnd);
        SetActiveWindow(hWnd);
        SetFocus(hWnd);
    }

    /// <summary>
    /// 尝试备用激活方法
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    private static void TryBackupActivationMethod(IntPtr hWnd)
    {
        try
        {
            Debug.WriteLine("使用备用激活方法");
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);
        }
        catch (Exception backupEx)
        {
            Debug.WriteLine($"备用激活方法也失败: {backupEx.Message}");
        }
    }

    /// <summary>
    /// 闪烁窗口以通知用户
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    private static void FlashWindowToNotify(IntPtr hWnd)
    {
        try
        {
            var flashInfo = new FlashWindowInfo
            {
                cbSize = (uint)Marshal.SizeOf<FlashWindowInfo>(),
                hwnd = hWnd,
                dwFlags = FlashwAll | FlashwTimerNoFg,
                uCount = FlashCount,
                dwTimeout = 0 // 使用默认闪烁频率
            };

            var result = FlashWindowEx(ref flashInfo);
            Debug.WriteLine($"窗口闪烁结果: {result}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"闪烁窗口时出错: {ex.Message}");
        }
    }

    #endregion

    #region 私有方法 - 资源清理

    /// <summary>
    /// 停止监听
    /// </summary>
    private static void StopListening()
    {
        try
        {
            // 停止监听
            _cancellationTokenSource?.Cancel();

            // 清理取消令牌
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _isListening = false;
            Debug.WriteLine("管道服务器监听已停止");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"停止监听时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 释放互斥锁
    /// </summary>
    private static void ReleaseMutex()
    {
        if (_mutex == null) return;

        try
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            _mutex = null;
            Debug.WriteLine("互斥锁已释放");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"释放互斥锁时出错: {ex.Message}");
        }
    }

    #endregion
}