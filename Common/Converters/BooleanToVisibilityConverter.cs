using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DrugSearcher.Converters;

/// <summary>
/// 布尔值转可见性转换器
/// </summary>
public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            bool boolValue => boolValue ? Visibility.Visible : Visibility.Collapsed,
            string strValue => string.IsNullOrEmpty(strValue) ? Visibility.Collapsed : Visibility.Visible,
            > 0 => Visibility.Visible,
            _ => Visibility.Collapsed
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}