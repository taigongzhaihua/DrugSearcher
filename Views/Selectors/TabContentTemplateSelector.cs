using System.Windows;
using System.Windows.Controls;
using DrugSearcher.ViewModels;

namespace DrugSearcher.Views.Selectors;

/// <summary>
/// Tab内容模板选择器
/// </summary>
public class TabContentTemplateSelector : DataTemplateSelector
{
    public DataTemplate? DefaultTemplate { get; set; }
    public DataTemplate? DosageTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is TabItemViewModel tabItem)
        {
            // 用法用量标签页使用特殊模板
            if (tabItem.Key == "Dosage" && DosageTemplate != null)
            {
                return DosageTemplate;
            }
        }

        return DefaultTemplate;
    }
}