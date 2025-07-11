using System.Globalization;
using System.Windows.Data;

namespace DrugSearcher.Converters;

public class ListToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is List<string> list)
        {
            return list.Count > 0 ? string.Join(", ", list) : "无";
        }
        return "无";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}