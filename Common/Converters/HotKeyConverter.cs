using DrugSearcher.Models;
using System.Globalization;
using System.Text.Json;
using System.Windows.Data;

namespace DrugSearcher.Converters;

/// <summary>
/// 快捷键转换器 - 处理快捷键对象与字符串之间的转换
/// </summary>
public class HotKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string jsonString || string.IsNullOrEmpty(jsonString)) return null;
        try
        {
            return JsonSerializer.Deserialize<HotKeySetting>(jsonString);
        }
        catch
        {
            // 如果解析失败，返回null
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not HotKeySetting hotKey) return null;
        try
        {
            return JsonSerializer.Serialize(hotKey);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 快捷键显示文本转换器
/// </summary>
public class HotKeyDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is HotKeySetting hotKey)
        {
            return hotKey.ToString();
        }

        return "未设置";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}