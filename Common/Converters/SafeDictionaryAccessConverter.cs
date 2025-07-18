using System.Globalization;
using System.Windows.Data;

namespace DrugSearcher.Converters;

/// <summary>
/// 安全的字典访问转换器
/// </summary>
public class SafeDictionaryAccessConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Dictionary<string, object> dictionary && parameter is string key)
        {
            return dictionary.TryGetValue(key, out var result) ? result : null;
        }
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

/// <summary>
/// 字典键存在检查转换器
/// </summary>
public class DictionaryContainsKeyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Dictionary<string, object> dictionary && parameter is string key)
        {
            return dictionary.ContainsKey(key);
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}