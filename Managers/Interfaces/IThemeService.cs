using DrugSearcher.Common.Enums;
using DrugSearcher.Models;

namespace DrugSearcher.Managers;

public interface IThemeService
{
    /// <summary>
    /// 当前主题配置
    /// </summary>
    ThemeConfig CurrentTheme { get; }

    /// <summary>
    /// 应用完整的主题配置
    /// </summary>
    void ApplyTheme(ThemeConfig themeConfig);

    /// <summary>
    /// 应用主题模式
    /// </summary>
    void ApplyThemeMode(ThemeMode mode);

    /// <summary>
    /// 应用主题颜色
    /// </summary>
    void ApplyThemeColor(ThemeColor color);

    /// <summary>
    /// 主题变更事件
    /// </summary>
    event EventHandler<ThemeConfig>? ThemeChanged;
}