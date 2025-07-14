using System.Collections.ObjectModel;

namespace DrugSearcher.Models;

/// <summary>
/// 动态设置项模型
/// </summary>
public class DynamicSettingItem
{
    /// <summary>
    /// 设置键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 设置描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 设置分类
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 设置类型
    /// </summary>
    public DynamicSettingType SettingType { get; set; }

    /// <summary>
    /// 当前值
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// 是否只读
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// 是否可见
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 搜索关键词
    /// </summary>
    public List<string> SearchKeywords { get; set; } = [];

    /// <summary>
    /// 验证器
    /// </summary>
    public Func<object?, bool>? Validator { get; set; }

    /// <summary>
    /// 值变更回调
    /// </summary>
    public Action<object?>? OnValueChanged { get; set; }

    /// <summary>
    /// 附加数据（用于特定类型的设置）
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; set; } = [];

    /// <summary>
    /// 排序权重
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; set; } = string.Empty;
}

/// <summary>
/// 动态设置类型
/// </summary>
public enum DynamicSettingType
{
    /// <summary>
    /// 布尔值（开关）
    /// </summary>
    Toggle,

    /// <summary>
    /// 下拉选择
    /// </summary>
    ComboBox,

    /// <summary>
    /// 滑块
    /// </summary>
    Slider,

    /// <summary>
    /// 文本输入
    /// </summary>
    TextBox,

    /// <summary>
    /// 数字输入
    /// </summary>
    NumberBox,

    /// <summary>
    /// 按钮
    /// </summary>
    Button,

    /// <summary>
    /// 文件选择
    /// </summary>
    FilePicker,

    /// <summary>
    /// 颜色选择
    /// </summary>
    ColorPicker,

    /// <summary>
    /// 快捷键
    /// </summary>
    HotKey,

    /// <summary>
    /// 自定义控件
    /// </summary>
    Custom
}

/// <summary>
/// 设置分组
/// </summary>
public class DynamicSettingGroup
{
    /// <summary>
    /// 分组名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 分组描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 分组图标
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 排序权重
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 是否可见
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// 设置项列表
    /// </summary>
    public ObservableCollection<DynamicSettingItem> Items { get; set; } = [];

    /// <summary>
    /// 是否可重置
    /// </summary>
    public bool CanReset { get; set; } = true;

    /// <summary>
    /// 自定义重置操作
    /// </summary>
    public Action? CustomResetAction { get; set; }
}