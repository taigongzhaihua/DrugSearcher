namespace DrugSearcher.Models;

/// <summary>
/// 语言选项模型
/// </summary>
public class LanguageOption
{
    /// <summary>
    /// 语言代码
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 返回显示名称
    /// </summary>
    /// <returns>显示名称</returns>
    public override string ToString() => DisplayName;

    /// <summary>
    /// 判断两个语言选项是否相等
    /// </summary>
    /// <param name="obj">比较对象</param>
    /// <returns>是否相等</returns>
    public override bool Equals(object? obj) => obj is LanguageOption other && Code == other.Code;

    /// <summary>
    /// 获取哈希码
    /// </summary>
    /// <returns>哈希码</returns>
    public override int GetHashCode() => Code.GetHashCode();
}