using System.Globalization;
using System.Windows.Data;
using Orientation = System.Windows.Controls.Orientation;

namespace DrugSearcher.Converters;

/// <summary>
/// Orientation为Vertical时返回true，Horizontal时返回false
/// </summary>
public class OrientationToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Orientation orientation)
        {
            return orientation == Orientation.Vertical;
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isVertical)
        {
            return isVertical ? Orientation.Vertical : Orientation.Horizontal;
        }
        return Orientation.Horizontal;
    }
}