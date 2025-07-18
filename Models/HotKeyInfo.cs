using System.Windows.Input;

namespace DrugSearcher.Models;

/// <summary>
/// 快捷键信息基类
/// </summary>
public class HotKeyInfo
{
    /// <summary>
    /// 快捷键ID（仅用于全局快捷键）
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 键值
    /// </summary>
    public Key Key { get; set; }

    /// <summary>
    /// 修饰键
    /// </summary>
    public ModifierKeys Modifiers { get; set; }

    /// <summary>
    /// 回调函数
    /// </summary>
    public Action? Callback { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 是否为全局快捷键
    /// </summary>
    public bool IsGlobal { get; set; }

    /// <summary>
    /// 是否可用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 获取快捷键显示名称
    /// </summary>
    public string DisplayName => GetDisplayText(Key, Modifiers);

    /// <summary>
    /// 获取快捷键的显示文本
    /// </summary>
    public static string GetDisplayText(Key key, ModifierKeys modifiers)
    {
        var parts = new List<string>();

        if (modifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows))
            parts.Add("Win");

        parts.Add(key.ToString());

        return string.Join(" + ", parts);
    }
    public override string ToString() => $"{DisplayName} - {Description}";
}

/// <summary>
/// 局部快捷键信息
/// </summary>
public class LocalHotKeyInfo : HotKeyInfo
{
    /// <summary>
    /// 快捷键名称
    /// </summary>
    public new string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public new bool IsEnabled { get; set; } = true;

    public override string ToString() => $"{Name}: {DisplayName} - {Description} ({(IsEnabled ? "启用" : "禁用")})";
}


/// <summary>
/// 快捷键设置
/// </summary>
public class HotKeySetting
{
    public Key Key { get; set; }
    public ModifierKeys Modifiers { get; set; }
    public bool IsEnabled { get; set; } = true;

    public HotKeySetting() { }

    public HotKeySetting(Key key, ModifierKeys modifiers, bool isEnabled = true)
    {
        Key = key;
        Modifiers = modifiers;
        IsEnabled = isEnabled;
    }

    public override string ToString() => HotKeyInfo.GetDisplayText(Key, Modifiers);
}