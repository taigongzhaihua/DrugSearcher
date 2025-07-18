using System.Globalization;
using System.Windows.Data;

namespace DrugSearcher.Converters;

/// <summary>
/// 编辑模式到标题转换器
/// </summary>
public class EditModeToTitleValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEditMode)
        {
            return isEditMode ? "编辑药物" : "添加药物";
        }
        return "药物信息";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}