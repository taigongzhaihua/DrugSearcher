using DrugSearcher.Models;
using System.Windows;
using System.Windows.Controls;

namespace DrugSearcher.Views.Selectors
{
    public class DosageParameterTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }
        public DataTemplate? NumberTemplate { get; set; }
        public DataTemplate? BooleanTemplate { get; set; }
        public DataTemplate? SelectionTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is not DosageParameter parameter)
                return null;

            return parameter.DataType switch
            {
                ParameterTypes.Text => TextTemplate,
                ParameterTypes.Number => NumberTemplate,
                ParameterTypes.Boolean => BooleanTemplate,
                ParameterTypes.Select => SelectionTemplate,
                _ => TextTemplate
            };
        }
    }
}