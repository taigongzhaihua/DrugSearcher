using System.Windows;
using System.Windows.Controls;

namespace DrugSearcher.Views;

/// <summary>
/// 计算器生成对话框
/// </summary>
public partial class CalculatorGenerationDialog : Window
{
    public string CalculatorType { get; private set; } = string.Empty;
    public string AdditionalRequirements { get; private set; } = string.Empty;

    public CalculatorGenerationDialog()
    {
        InitializeComponent();
    }

    private void GenerateButton_Click(object sender, RoutedEventArgs e)
    {
        // 获取选择的计算器类型
        if (CalculatorTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            CalculatorType = selectedItem.Content.ToString() ?? "通用剂量计算器";
        }
        else
        {
            CalculatorType = "通用剂量计算器";
        }

        // 获取额外要求
        AdditionalRequirements = AdditionalRequirementsTextBox.Text.Trim();

        // 设置对话框结果为True并关闭
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        // 设置对话框结果为False并关闭
        DialogResult = false;
        Close();
    }
}