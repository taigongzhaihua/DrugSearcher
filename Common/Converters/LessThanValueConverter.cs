using System.Globalization;
using System.Windows.Data;
using Binding = System.Windows.Forms.Binding;

namespace DrugSearcher.Views;

/// <summary>
/// 小于比较转换器
/// </summary>
public class LessThanValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue && parameter is Binding binding)
        {
            // 这里需要根据实际绑定的值进行比较
            // 简化处理，假设直接传入数值
            return intValue < 10; // 这里需要根据实际情况调整
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}