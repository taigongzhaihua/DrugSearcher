using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Color = System.Windows.Media.Color;

namespace DrugSearcher.Helpers
{
    /// <summary>
    /// 跨 Windows 版本的窗口颜色管理器
    /// 支持自定义边框和标题栏颜色
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    public static partial class WindowColorManager
    {
        #region Windows API 定义

        // DWM API (Windows 11)
        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref uint attrValue, uint attrSize);

        [LibraryImport("dwmapi.dll")]
        private static partial int DwmSetWindowAttribute(IntPtr hwnd, uint attr, ref int attrValue, uint attrSize);

        // 组合属性 API (Windows 10/11)
        [LibraryImport("user32.dll")]
        private static partial int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        // UxTheme API
        [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
        private static partial int SetWindowTheme(IntPtr hwnd, string? pszSubAppName, string? pszSubIdList);

        // 窗口重绘
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        [LibraryImport("user32.dll")]
        private static partial int GetWindowLong(IntPtr hwnd, int index);

        [LibraryImport("user32.dll")]
        private static partial int SetWindowLong(IntPtr hwnd, int index, int newStyle);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter,
            int x, int y, int cx, int cy, uint flags);


        [LibraryImport("user32.dll")]
        private static partial IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        #endregion

        #region 常量定义

        // Windows 11 DWM 属性
        private const uint DwmwaBorderColor = 34;
        private const uint DwmwaCaptionColor = 35;
        private const uint DwmwaUseImmersiveDarkMode = 20;

        // Windows 10 组合属性
        private enum WindowCompositionAttribute
        {
            WcaAccentPolicy = 19,
            WcaUsedarkmodecolors = 26
        }

        #endregion

        #region 结构体定义

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public AccentState AccentState;
            public AccentFlags AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        private enum AccentState
        {
            AccentDisabled = 0,
            AccentEnableGradient = 1,
            AccentEnableTransparentgradient = 2,
            AccentEnableBlurbehind = 3,
            AccentEnableAcrylicblurbehind = 4,
            AccentEnableHostbackdrop = 5
        }

        private enum AccentFlags
        {
            None = 0,
            DrawLeftBorder = 0x20,
            DrawTopBorder = 0x40,
            DrawRightBorder = 0x80,
            DrawBottomBorder = 0x100,
            DrawAllBorders = DrawLeftBorder | DrawTopBorder | DrawRightBorder | DrawBottomBorder
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置窗口边框和标题栏颜色
        /// </summary>
        /// <param name="window">目标窗口</param>
        /// <param name="borderColor">边框颜色</param>
        /// <param name="titleBarColor">标题栏颜色（可选，默认与边框同色）</param>
        /// <returns>设置结果</returns>
        public static WindowColorResult SetWindowColors(Window? window, Color borderColor, Color? titleBarColor = null)
        {
            if (window == null)
                return new WindowColorResult(false, false, "None", "窗口为空");

            var actualTitleBarColor = titleBarColor ?? borderColor;
            var hwnd = new WindowInteropHelper(window).Handle;

            if (hwnd == IntPtr.Zero)
                return new WindowColorResult(false, false, "None", "无法获取窗口句柄");

            // Windows 11: 使用原生 API
            if (IsWindows11OrLater())
            {
                return SetWindows11Colors(hwnd, borderColor, actualTitleBarColor);
            }


            // Windows 10: 使用组合方法
            if (IsWindows10())
            {
                var result = SetWindows10Colors(hwnd, borderColor, actualTitleBarColor);
                if (result != null)
                {
                    window.UpdateLayout();
                    window.Width += 1;
                    window.Width -= 1;
                    return result;
                }
                    
            }

            return new WindowColorResult(false, false, "Unsupported", "不支持的系统版本");
        }

        /// <summary>
        /// 重置窗口颜色为系统默认
        /// </summary>
        /// <param name="window">目标窗口</param>
        /// <returns>重置结果</returns>
        public static WindowColorResult ResetWindowColors(Window? window)
        {
            if (window == null)
                return new WindowColorResult(false, false, "None", "窗口为空");

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
                return new WindowColorResult(false, false, "None", "无法获取窗口句柄");

            if (IsWindows11OrLater())
            {
                return ResetWindows11Colors(hwnd);
            }

            if (IsWindows10())
            {
                var result = ResetWindows10Colors(hwnd);
                if (result != null)
                {
                    window.UpdateLayout();
                    window.Width += 1;
                    window.Width -= 1;
                    return result;
                }
            }

            return new WindowColorResult(false, false, "Unsupported", "不支持的系统版本");
        }

        #endregion

        #region Windows 11 实现

        private static WindowColorResult SetWindows11Colors(IntPtr hwnd, Color borderColor, Color titleBarColor)
        {
            try
            {
                var borderColorRef = ColorToColorRef(borderColor);
                var titleColorRef = ColorToColorRef(titleBarColor);

                var borderResult = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref borderColorRef, sizeof(uint));
                var titleResult = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref titleColorRef, sizeof(uint));

                var borderSuccess = borderResult == 0;
                var titleSuccess = titleResult == 0;

                return new WindowColorResult(borderSuccess, titleSuccess, "Windows11",
                    borderSuccess && titleSuccess ? "Windows 11 原生 API 设置成功" : "部分设置失败");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows 11 颜色设置失败: {ex.Message}");
                return new WindowColorResult(false, false, "Windows11", ex.Message);
            }
        }

        private static WindowColorResult ResetWindows11Colors(IntPtr hwnd)
        {
            try
            {
                uint defaultColor = 0xFFFFFFFF; // 系统默认

                var borderResult = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref defaultColor, sizeof(uint));
                var titleResult = DwmSetWindowAttribute(hwnd, DwmwaCaptionColor, ref defaultColor, sizeof(uint));

                var borderSuccess = borderResult == 0;
                var titleSuccess = titleResult == 0;

                return new WindowColorResult(borderSuccess, titleSuccess, "Windows11",
                    borderSuccess && titleSuccess ? "Windows 11 重置成功" : "部分重置失败");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows 11 颜色重置失败: {ex.Message}");
                return new WindowColorResult(false, false, "Windows11", ex.Message);
            }
        }

        #endregion

        #region Windows 10 实现

        private static WindowColorResult SetWindows10Colors(IntPtr hwnd, Color borderColor, Color titleBarColor)
        {
            try
            {

                // 方法2: 尝试设置标题栏主题
                var titleSuccess = SetWindows10TitleBarTheme(hwnd, titleBarColor);

                // 方法1: 尝试使用组合属性设置边框颜色
                var borderSuccess = SetWindows10BorderColor(hwnd, borderColor);


                if (!borderSuccess)
                {
                    // 备选方案: 使用毛玻璃效果
                    borderSuccess = SetWindows10AcrylicColor(hwnd, borderColor);
                }

                if (hwnd != IntPtr.Zero)
                {
                    // 发送 WM_NCPAINT 消息
                    const int WM_NCPAINT = 0x0085;
                    SendMessage(hwnd, WM_NCPAINT, new IntPtr(1), IntPtr.Zero);

                    // 发送 WM_NCACTIVATE 消息
                    const int WM_NCACTIVATE = 0x0086;
                    SendMessage(hwnd, WM_NCACTIVATE, new IntPtr(1), IntPtr.Zero);

                    // 使用 RedrawWindow 指定只重绘边框
                    const uint RDW_FRAME = 0x0400;
                    const uint RDW_INVALIDATE = 0x0001;
                    const uint RDW_UPDATENOW = 0x0100;

                    RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_INVALIDATE | RDW_UPDATENOW);
                }
                var method = borderSuccess ? "Windows10_Composition" : "Windows10_Fallback";
                var message = $"Windows 10 设置结果: 边框={borderSuccess}, 标题栏={titleSuccess}";

                return new WindowColorResult(borderSuccess, titleSuccess, method, message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows 10 颜色设置失败: {ex.Message}");
                return new WindowColorResult(false, false, "Windows10", ex.Message);
            }
        }

        private static bool SetWindows10BorderColor(IntPtr hwnd, Color color)
        {
            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.AccentDisabled,
                    AccentFlags = AccentFlags.None,
                    GradientColor = ColorToAbgr(color),
                    AnimationId = 0
                };

                return SetCompositionAttribute(hwnd, WindowCompositionAttribute.WcaAccentPolicy, accent);
            }
            catch
            {
                return false;
            }
        }

        private static bool SetWindows10AcrylicColor(IntPtr hwnd, Color color)
        {
            try
            {
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.AccentEnableAcrylicblurbehind,
                    AccentFlags = AccentFlags.DrawAllBorders,
                    GradientColor = ColorToAbgr(color),
                    AnimationId = 0
                };

                return SetCompositionAttribute(hwnd, WindowCompositionAttribute.WcaAccentPolicy, accent);
            }
            catch
            {
                return false;
            }
        }

        private static bool SetWindows10TitleBarTheme(IntPtr hwnd, Color color)
        {
            try
            {
                // 根据颜色亮度决定使用明暗主题
                var brightness = (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) / 255;
                var useDarkMode = brightness < 0.5;

                // 设置暗色模式
                var darkMode = useDarkMode ? 1 : 0;
                var result1 = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));

                // 设置窗口主题
                var themeResult = SetWindowTheme(hwnd, useDarkMode ? "DarkMode_Explorer" : null, null);

                if (themeResult == 0)
                {
                    InvalidateRect(hwnd, IntPtr.Zero, true);
                }

                return result1 == 0 || themeResult == 0;
            }
            catch
            {
                return false;
            }
        }

        private static WindowColorResult ResetWindows10Colors(IntPtr hwnd)
        {
            try
            {
                // 重置组合属性
                var accent = new AccentPolicy
                {
                    AccentState = AccentState.AccentDisabled
                };

                var borderSuccess = SetCompositionAttribute(hwnd, WindowCompositionAttribute.WcaAccentPolicy, accent);

                // 重置主题
                var titleSuccess = SetWindowTheme(hwnd, null, null) == 0;

                if (titleSuccess)
                {
                    InvalidateRect(hwnd, IntPtr.Zero, true);
                }

                return new WindowColorResult(borderSuccess, titleSuccess, "Windows10",
                    borderSuccess && titleSuccess ? "Windows 10 重置成功" : "部分重置失败");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Windows 10 颜色重置失败: {ex.Message}");
                return new WindowColorResult(false, false, "Windows10", ex.Message);
            }
        }

        #endregion

        #region 辅助方法

        private static bool SetCompositionAttribute<T>(IntPtr hwnd, WindowCompositionAttribute attribute, T data) where T : struct
        {
            try
            {
                var structSize = Marshal.SizeOf(data);
                var dataPtr = Marshal.AllocHGlobal(structSize);
                Marshal.StructureToPtr(data, dataPtr, false);

                var compData = new WindowCompositionAttributeData
                {
                    Attribute = attribute,
                    SizeOfData = structSize,
                    Data = dataPtr
                };

                var result = SetWindowCompositionAttribute(hwnd, ref compData);
                Marshal.FreeHGlobal(dataPtr);

                return result == 1;
            }
            catch
            {
                return false;
            }
        }

        private static uint ColorToColorRef(Color color)
        {
            return (uint)(color.R | (color.G << 8) | (color.B << 16));
        }

        private static uint ColorToAbgr(Color color)
        {
            return (uint)((color.A << 24) | (color.B << 16) | (color.G << 8) | color.R);
        }

        private static bool IsWindows11OrLater()
        {
            var version = Environment.OSVersion.Version;
            return version is { Major: >= 10, Build: >= 22000 };
        }

        private static bool IsWindows10()
        {
            var version = Environment.OSVersion.Version;
            return version is { Major: 10, Build: >= 10240 and < 22000 };
        }

        #endregion
    }

    /// <summary>
    /// 窗口颜色设置结果
    /// </summary>
    public record WindowColorResult(bool BorderSuccess, bool TitleBarSuccess, string Method, string Message)
    {
        public bool IsFullySuccessful => BorderSuccess && TitleBarSuccess;
        public bool IsPartiallySuccessful => BorderSuccess || TitleBarSuccess;
    }
}