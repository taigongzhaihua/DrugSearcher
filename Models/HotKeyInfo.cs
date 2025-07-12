using System;
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
    /// 获取快捷键显示名称
    /// </summary>
    public string DisplayName
    {
        get
        {
            var parts = new List<string>();

            if ((Modifiers & ModifierKeys.Control) != 0)
                parts.Add("Ctrl");

            if ((Modifiers & ModifierKeys.Alt) != 0)
                parts.Add("Alt");

            if ((Modifiers & ModifierKeys.Shift) != 0)
                parts.Add("Shift");

            if ((Modifiers & ModifierKeys.Windows) != 0)
                parts.Add("Win");

            parts.Add(Key.ToString());

            return string.Join(" + ", parts);
        }
    }

    public override string ToString()
    {
        return $"{DisplayName} - {Description}";
    }
}

/// <summary>
/// 局部快捷键信息
/// </summary>
public class LocalHotKeyInfo : HotKeyInfo
{
    /// <summary>
    /// 快捷键名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    public override string ToString()
    {
        return $"{Name}: {DisplayName} - {Description} ({(IsEnabled ? "启用" : "禁用")})";
    }
}