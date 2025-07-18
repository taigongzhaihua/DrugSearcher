using System.Globalization;
using System.Windows.Data;

namespace DrugSearcher.Converters;

/// <summary>
/// 大于比较转换器
/// </summary>
public class GreaterThanValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is string paramStr && int.TryParse(paramStr, out var threshold))
        {
            return intValue > threshold;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}