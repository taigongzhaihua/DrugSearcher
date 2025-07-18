namespace DrugSearcher.Models;

/// <summary>
/// 字体大小约束常量
/// </summary>
public static class FontSizeConstraints
{
    /// <summary>
    /// 最小字体大小
    /// </summary>
    public const int MinSize = 8;

    /// <summary>
    /// 最大字体大小
    /// </summary>
    public const int MaxSize = 72;

    /// <summary>
    /// 默认字体大小
    /// </summary>
    public const int DefaultSize = 12;

    /// <summary>
    /// 验证字体大小是否在有效范围内
    /// </summary>
    /// <param name="size">字体大小</param>
    /// <returns>是否有效</returns>
    public static bool IsValidSize(int size) => size is >= MinSize and <= MaxSize;
}