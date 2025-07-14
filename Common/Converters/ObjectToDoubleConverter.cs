using System.Globalization;
using System.Windows.Data;

namespace DrugSearcher.Converters;

/// <summary>
/// Object 到 Double 的转换器
/// </summary>
public class ObjectToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return 0.0;

        try
        {
            return value switch
            {
                double d => d,
                int i => (double)i,
                float f => (double)f,
                decimal dec => (double)dec,
                long l => (double)l,
                short s => (double)s,
                byte b => (double)b,
                string str when double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) => result,
                _ => System.Convert.ToDouble(value, CultureInfo.InvariantCulture)
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"转换失败: {value} -> Double, {ex.Message}");
            return 0.0;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return targetType == typeof(int) ? 0 : 0.0;

        try
        {
            var doubleValue = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
            
            return targetType switch
            {
                Type t when t == typeof(int) => (int)Math.Round(doubleValue),
                Type t when t == typeof(float) => (float)doubleValue,
                Type t when t == typeof(decimal) => (decimal)doubleValue,
                Type t when t == typeof(long) => (long)Math.Round(doubleValue),
                Type t when t == typeof(short) => (short)Math.Round(doubleValue),
                Type t when t == typeof(byte) => (byte)Math.Round(doubleValue),
                _ => doubleValue
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"反向转换失败: {value} -> {targetType}, {ex.Message}");
            return targetType == typeof(int) ? 0 : 0.0;
        }
    }
}