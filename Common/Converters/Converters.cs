using DrugSearcher.Views;
using System.Windows.Data;

namespace DrugSearcher.Common.Converters;

/// <summary>
/// 转换器集合
/// </summary>
public static class Converters
{
    /// <summary>
    /// 布尔值到可见性转换器
    /// </summary>
    public static readonly IValueConverter BooleanToVisibilityConverter = new BooleanToVisibilityConverter();

    /// <summary>
    /// 反向布尔值到可见性转换器
    /// </summary>
    public static readonly IValueConverter InverseBooleanToVisibilityConverter = new InverseBooleanToVisibilityConverter();

    /// <summary>
    /// 编辑模式到标题转换器
    /// </summary>
    public static readonly IValueConverter EditModeToTitleConverter = new EditModeToTitleValueConverter();

    /// <summary>
    /// 大于比较转换器
    /// </summary>
    public static readonly IValueConverter GreaterThanConverter = new GreaterThanValueConverter();

    /// <summary>
    /// 小于比较转换器
    /// </summary>
    public static readonly IValueConverter LessThanConverter = new LessThanValueConverter();
}