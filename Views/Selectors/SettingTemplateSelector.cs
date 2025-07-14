using DrugSearcher.Models;
using System.Windows;
using System.Windows.Controls;

namespace DrugSearcher.Views.Selectors;

/// <summary>
/// 设置模板选择器
/// </summary>
public class SettingTemplateSelector : DataTemplateSelector
{
    /// <summary>
    /// 开关模板
    /// </summary>
    public DataTemplate? ToggleTemplate { get; set; }

    /// <summary>
    /// 下拉框模板
    /// </summary>
    public DataTemplate? ComboBoxTemplate { get; set; }

    /// <summary>
    /// 滑块模板
    /// </summary>
    public DataTemplate? SliderTemplate { get; set; }

    /// <summary>
    /// 文本框模板
    /// </summary>
    public DataTemplate? TextBoxTemplate { get; set; }

    /// <summary>
    /// 数字框模板
    /// </summary>
    public DataTemplate? NumberBoxTemplate { get; set; }

    /// <summary>
    /// 按钮模板
    /// </summary>
    public DataTemplate? ButtonTemplate { get; set; }

    /// <summary>
    /// 文件选择模板
    /// </summary>
    public DataTemplate? FilePickerTemplate { get; set; }

    /// <summary>
    /// 颜色选择模板
    /// </summary>
    public DataTemplate? ColorPickerTemplate { get; set; }

    /// <summary>
    /// 热键模板
    /// </summary>
    public DataTemplate? HotKeyTemplate { get; set; }

    /// <summary>
    /// 自定义模板
    /// </summary>
    public DataTemplate? CustomTemplate { get; set; }

    /// <summary>
    /// 选择模板
    /// </summary>
    /// <param name="item">数据项</param>
    /// <param name="container">容器</param>
    /// <returns>数据模板</returns>
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        if (item is not DynamicSettingItem settingItem)
            return base.SelectTemplate(item, container);

        return settingItem.SettingType switch
        {
            DynamicSettingType.Toggle => ToggleTemplate,
            DynamicSettingType.ComboBox => ComboBoxTemplate,
            DynamicSettingType.Slider => SliderTemplate,
            DynamicSettingType.TextBox => TextBoxTemplate,
            DynamicSettingType.NumberBox => NumberBoxTemplate,
            DynamicSettingType.Button => ButtonTemplate,
            DynamicSettingType.FilePicker => FilePickerTemplate,
            DynamicSettingType.ColorPicker => ColorPickerTemplate,
            DynamicSettingType.HotKey => HotKeyTemplate,
            DynamicSettingType.Custom => CustomTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}