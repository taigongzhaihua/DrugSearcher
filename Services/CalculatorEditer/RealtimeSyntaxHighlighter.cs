using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace DrugSearcher.Services;

/// <summary>
/// 实时语法高亮器
/// </summary>
public partial class RealtimeSyntaxHighlighter : DocumentColorizingTransformer
{
    private readonly JavaScriptDynamicContext _dynamicContext;
    private readonly HashSet<string?> _localIdentifiers = [];
    private readonly bool _isDarkTheme;
    private readonly Lock _lock = new();

    public RealtimeSyntaxHighlighter(JavaScriptDynamicContext dynamicContext, bool isDarkTheme)
    {
        _dynamicContext = dynamicContext;
        _isDarkTheme = isDarkTheme;
        _dynamicContext.ContextChanged += OnContextChanged;
    }

    private void OnContextChanged(object? sender, ContextChangedEventArgs e)
    {
        lock (_lock)
        {
            _localIdentifiers.Clear();
            _localIdentifiers.UnionWith(_dynamicContext.GetAllLocalIdentifiers());
        }
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line.Offset, line.Length);
        var lineStartOffset = line.Offset;

        // 查找所有标识符
        var matches = IdentifierRegex().Matches(text);

        foreach (var match in matches.Cast<Match>())
        {
            var identifier = match.Groups[1].Value;
            var startOffset = lineStartOffset + match.Index;
            var endOffset = startOffset + identifier.Length;

            // 跳过关键字（它们已经被基础语法高亮处理）
            if (JavaScriptLanguageDefinition.IsKeyword(identifier))
                continue;

            // 检查是否是数组字面量后的方法调用
            if (IsArrayMethodCall(text, match.Index))
            {
                // 使用函数颜色高亮数组方法
                var color = _isDarkTheme
                    ? JavaScriptLanguageDefinition.SyntaxColors.DarkTheme["Function"]
                    : JavaScriptLanguageDefinition.SyntaxColors.LightTheme["Function"];

                ChangeLinePart(startOffset, endOffset, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(color));
                });
            }
            // 检查是否是局部变量
            else if (_localIdentifiers.Contains(identifier))
            {
                var color = _isDarkTheme
                    ? JavaScriptLanguageDefinition.SyntaxColors.DarkTheme["Variable"]
                    : JavaScriptLanguageDefinition.SyntaxColors.LightTheme["Variable"];

                ChangeLinePart(startOffset, endOffset, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(new SolidColorBrush(color));
                });
            }
        }
    }

    /// <summary>
    /// 检查是否是数组方法调用
    /// </summary>
    private static bool IsArrayMethodCall(string line, int identifierIndex)
    {
        // 检查标识符前是否有点号
        if (identifierIndex == 0) return false;

        var beforeIndex = identifierIndex - 1;
        while (beforeIndex >= 0 && char.IsWhiteSpace(line[beforeIndex]))
            beforeIndex--;

        if (beforeIndex < 0 || line[beforeIndex] != '.') return false;

        // 检查点号前是否是数组相关的表达式
        beforeIndex--;
        while (beforeIndex >= 0 && char.IsWhiteSpace(line[beforeIndex]))
            beforeIndex--;

        if (beforeIndex < 0) return false;

        // 如果是闭合的方括号，可能是数组字面量或数组访问
        if (line[beforeIndex] != ']') return false;
        // 简单检查：如果标识符是已知的数组方法，则认为是数组方法调用
        var match = IdentifierRegex().Match(line[identifierIndex..]);
        if (!match.Success) return false;
        var methodName = match.Groups[1].Value;
        return JavaScriptLanguageDefinition.BuiltIns.ArrayPrototypeMethods.ContainsKey(methodName);

    }

    [GeneratedRegex(@"\b([a-zA-Z_$][a-zA-Z0-9_$]*)\b")]
    private static partial Regex IdentifierRegex();
}