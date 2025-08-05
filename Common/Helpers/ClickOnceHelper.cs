using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace DrugSearcher.Helpers;

/// <summary>
/// ClickOnce部署辅助类 (WPF版本)
/// </summary>
public static class ClickOnceHelper
{
    private const string APPLICATION_NAME = "DrugSearcher";
    private const string PUBLISHER_NAME = "TaiGongZhaiHua";

    /// <summary>
    /// 获取应用程序启动路径
    /// </summary>
    public static string GetStartupPath()
    {
        return AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <summary>
    /// 检查是否是ClickOnce部署
    /// </summary>
    public static bool IsClickOnceDeployed()
    {
        try
        {
            var appPath = Assembly.GetExecutingAssembly().Location;
            var directory = Path.GetDirectoryName(appPath);

            if (!string.IsNullOrEmpty(directory))
            {
                // ClickOnce应用通常在LocalApplicationData\Apps\2.0目录下
                return directory.Contains(@"\Apps\2.0\") ||
                       directory.Contains(@"\AppData\Local\Apps\");
            }
        }
        catch
        {
            // 忽略错误
        }

        return false;
    }

    /// <summary>
    /// 重启应用程序（用于更新后）
    /// </summary>
    public static void RestartApplication()
    {
        try
        {
            if (IsClickOnceDeployed())
            {
                // 查找开始菜单快捷方式
                var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                var shortcutPath = Path.Combine(startMenuPath, PUBLISHER_NAME, $"{APPLICATION_NAME}.appref-ms");

                if (File.Exists(shortcutPath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = shortcutPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // 尝试从当前路径启动
                    var exePath = Assembly.GetExecutingAssembly().Location;
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
            }
            else
            {
                // 普通应用程序重启
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
            }

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"重启应用程序失败: {ex.Message}");
            MessageBox.Show("无法自动重启应用程序，请手动重新启动。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// 创建桌面快捷方式
    /// </summary>
    public static void CreateDesktopShortcut()
    {
        if (!IsClickOnceDeployed())
            return;

        try
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            var sourceShortcut = Path.Combine(startMenuPath, PUBLISHER_NAME, $"{APPLICATION_NAME}.appref-ms");
            var destShortcut = Path.Combine(desktopPath, $"{APPLICATION_NAME}.appref-ms");

            if (File.Exists(sourceShortcut) && !File.Exists(destShortcut))
            {
                File.Copy(sourceShortcut, destShortcut);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"创建桌面快捷方式失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取ClickOnce数据目录
    /// </summary>
    public static string GetClickOnceDataDirectory()
    {
        if (!IsClickOnceDeployed())
            return GetStartupPath();

        try
        {
            // 获取应用程序数据目录
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataPath = Path.Combine(localAppData, PUBLISHER_NAME, APPLICATION_NAME);

            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            return dataPath;
        }
        catch
        {
            return GetStartupPath();
        }
    }

    /// <summary>
    /// 检查是否从可移动磁盘运行
    /// </summary>
    public static bool IsRunningFromRemovableDrive()
    {
        try
        {
            var appPath = Assembly.GetExecutingAssembly().Location;
            var driveInfo = new DriveInfo(Path.GetPathRoot(appPath) ?? "C:\\");
            return driveInfo.DriveType == DriveType.Removable;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 打开应用程序文件夹
    /// </summary>
    public static void OpenApplicationFolder()
    {
        try
        {
            var path = IsClickOnceDeployed() ? GetClickOnceDataDirectory() : GetStartupPath();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"打开应用程序文件夹失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取ClickOnce应用程序路径
    /// </summary>
    public static string GetClickOnceApplicationPath()
    {
        if (!IsClickOnceDeployed())
            return Assembly.GetExecutingAssembly().Location;

        try
        {
            var appPath = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(appPath) ?? appPath;
        }
        catch
        {
            return Assembly.GetExecutingAssembly().Location;
        }
    }

    /// <summary>
    /// 获取ClickOnce缓存路径
    /// </summary>
    public static string GetClickOnceCachePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Apps", "2.0");
    }

    /// <summary>
    /// 检查ClickOnce权限
    /// </summary>
    public static bool CheckClickOncePermissions()
    {
        try
        {
            // 检查是否有写入权限
            var testPath = GetClickOnceDataDirectory();
            var testFile = Path.Combine(testPath, "test.tmp");

            File.WriteAllText(testFile, "test");
            File.Delete(testFile);

            return true;
        }
        catch
        {
            return false;
        }
    }
}