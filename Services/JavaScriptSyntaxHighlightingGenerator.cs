using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace DrugSearcher.Services
{
    /// <summary>
    /// JavaScript语法高亮生成器
    /// </summary>
    public partial class JavaScriptSyntaxHighlightingGenerator
    {
        /// <summary>
        /// 生成语法高亮定义
        /// </summary>
        public static IHighlightingDefinition GenerateDefinition(bool isDarkTheme)
        {
            var colors = isDarkTheme
                ? JavaScriptLanguageDefinition.SyntaxColors.DarkTheme
                : JavaScriptLanguageDefinition.SyntaxColors.LightTheme;

            // 创建高亮定义
            var definition = new CustomHighlightingDefinition(isDarkTheme ? "JavaScript Dark" : "JavaScript Light");

            // 创建主规则集
            var mainRuleSet = new HighlightingRuleSet();
            definition.MainRuleSet = mainRuleSet;

            // 添加所有颜色定义到定义中
            var namedColors = CreateNamedColors(colors);
            foreach (var namedColor in namedColors)
            {
                _ = definition.NamedHighlightingColors.Append(namedColor.Value);
            }

            // 添加多行注释 (Span)
            AddMultilineCommentSpan(mainRuleSet, namedColors["Comment"]);

            // 添加字符串 (Span) - 注意：HighlightingSpan 不支持 EscapeCharacter，需要用 Regex 处理
            AddStringSpans(mainRuleSet, namedColors["String"]);

            // 添加单行注释规则
            AddSingleLineCommentRule(mainRuleSet, namedColors["Comment"]);

            // 添加数字规则
            AddNumberRule(mainRuleSet, namedColors["Number"]);

            // 添加关键字规则
            AddKeywordRules(mainRuleSet, namedColors["Keyword"]);

            // 添加内置函数和对象规则
            AddBuiltInRules(mainRuleSet, namedColors["BuiltInFunction"]);

            // 添加自定义函数规则
            AddCustomFunctionRules(mainRuleSet, namedColors["CustomFunction"]);

            // 添加函数调用规则（必须在具体函数名之后）
            AddFunctionCallRule(mainRuleSet, namedColors["Function"]);

            // 添加属性访问规则
            AddPropertyAccessRule(mainRuleSet, namedColors["Property"]);

            // 添加操作符规则
            AddOperatorRule(mainRuleSet, namedColors["Operator"]);

            // 添加变量规则（最后，最低优先级）
            AddVariableRule(mainRuleSet, namedColors["Variable"]);

            return definition;
        }

        /// <summary>
        /// 创建命名颜色
        /// </summary>
        private static Dictionary<string, HighlightingColor> CreateNamedColors(Dictionary<string, Color> colors)
        {
            var namedColors = new Dictionary<string, HighlightingColor>();

            foreach (var color in colors)
            {
                var highlightingColor = new HighlightingColor
                {
                    Name = color.Key,
                    Foreground = new SimpleHighlightingBrush(color.Value)
                };

                // 关键字加粗
                if (color.Key == "Keyword")
                {
                    highlightingColor.FontWeight = FontWeights.Bold;
                }

                namedColors[color.Key] = highlightingColor;
            }

            return namedColors;
        }

        /// <summary>
        /// 添加多行注释
        /// </summary>
        private static void AddMultilineCommentSpan(HighlightingRuleSet? ruleSet, HighlightingColor color)
        {
            var multilineComment = new HighlightingSpan
            {
                StartExpression = MultilineCommentSpanStartRegex(),
                EndExpression = MultilineCommentSpanEndRegex(),
                SpanColor = color,
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true
            };
            ruleSet?.Spans.Add(multilineComment);
        }

        /// <summary>
        /// 添加字符串规则
        /// </summary>
        private static void AddStringSpans(HighlightingRuleSet? ruleSet, HighlightingColor color)
        {
            // 双引号字符串 - 使用正则表达式处理转义字符
            var doubleQuoteString = new HighlightingSpan
            {
                StartExpression = DoubleQuoteStringSpansStartRegex(),
                EndExpression = DoubleQuoteStringSpansEndRegex(),
                SpanColor = color,
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true
            };
            ruleSet?.Spans.Add(doubleQuoteString);

            // 单引号字符串
            var singleQuoteString = new HighlightingSpan
            {
                StartExpression = SingleQuoteStringSpansStartRegex(),
                EndExpression = SingleQuoteStringSpansEndRegex(),
                SpanColor = color,
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true
            };
            ruleSet?.Spans.Add(singleQuoteString);

            // 模板字符串
            var templateString = new HighlightingSpan
            {
                StartExpression = TemplateStringSpansStartRegex(),
                EndExpression = TemplateStringSpansEndRegex(),
                SpanColor = color,
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true
            };
            ruleSet?.Spans.Add(templateString);
        }

        /// <summary>
        /// 添加单行注释规则
        /// </summary>
        private static void AddSingleLineCommentRule(HighlightingRuleSet? ruleSet, HighlightingColor color) => ruleSet?.Rules.Add(new HighlightingRule
        {
            Color = color,
            Regex = SingleLineCommentRegex()
        });

        /// <summary>
        /// 添加数字规则
        /// </summary>
        private static void AddNumberRule(HighlightingRuleSet? ruleSet, HighlightingColor color) => ruleSet?.Rules.Add(new HighlightingRule
        {
            Color = color,
            Regex = NumberRegex()
        });

        /// <summary>
        /// 添加关键字规则
        /// </summary>
        private static void AddKeywordRules(HighlightingRuleSet? ruleSet, HighlightingColor color)
        {
            var keywords = JavaScriptLanguageDefinition.Keywords.All;
            if (keywords.Count == 0) return;

            // 创建关键字正则表达式
            var keywordPattern = string.Join("|", keywords.Select(k =>
            {
                if (k != null) return @"\b" + Regex.Escape(k) + @"\b";
                return null;
            }));

            ruleSet?.Rules.Add(new HighlightingRule
            {
                Color = color,
                Regex = new Regex(keywordPattern)
            });
        }

        /// <summary>
        /// 添加内置函数和对象规则
        /// </summary>
        private static void AddBuiltInRules(HighlightingRuleSet? ruleSet, HighlightingColor color)
        {
            // 内置函数
            var builtInFunctions = JavaScriptLanguageDefinition.BuiltIns.GlobalFunctions.Keys;
            if (builtInFunctions.Count != 0)
            {
                var functionPattern = string.Join("|", builtInFunctions.Select(f => @"\b" + Regex.Escape(f) + @"\b"));
                ruleSet?.Rules.Add(new HighlightingRule
                {
                    Color = color,
                    Regex = new Regex(functionPattern)
                });
            }

            // 内置对象
            var builtInObjects = JavaScriptLanguageDefinition.BuiltIns.GlobalObjects.Keys;
            if (builtInObjects.Count != 0)
            {
                var objectPattern = string.Join("|", builtInObjects.Select(o => @"\b" + Regex.Escape(o) + @"\b"));
                ruleSet?.Rules.Add(new HighlightingRule
                {
                    Color = color,
                    Regex = new Regex(objectPattern)
                });
            }
        }

        /// <summary>
        /// 添加自定义函数规则
        /// </summary>
        private static void AddCustomFunctionRules(HighlightingRuleSet? ruleSet, HighlightingColor color)
        {
            var customFunctions = JavaScriptLanguageDefinition.CustomFunctions.Names;
            if (customFunctions.Count == 0) return;

            var customFunctionPattern = string.Join("|", customFunctions.Select(f =>
            {
                if (f != null) return @"\b" + Regex.Escape(f) + @"\b";
                return null;
            }));
            ruleSet?.Rules.Add(new HighlightingRule
            {
                Color = color,
                Regex = new Regex(customFunctionPattern)
            });
        }

        /// <summary>
        /// 添加函数调用规则
        /// </summary>
        private static void AddFunctionCallRule(HighlightingRuleSet? ruleSet, HighlightingColor color) => ruleSet?.Rules.Add(new HighlightingRule
        {
            Color = color,
            Regex = FunctionCallRegex()
        });

        /// <summary>
        /// 添加属性访问规则
        /// </summary>
        private static void AddPropertyAccessRule(HighlightingRuleSet? ruleSet, HighlightingColor color) => ruleSet?.Rules.Add(new HighlightingRule
        {
            Color = color,
            Regex = PropertyAccessRegex()
        });

        /// <summary>
        /// 添加操作符规则
        /// </summary>
        private static void AddOperatorRule(HighlightingRuleSet? ruleSet, HighlightingColor color) => ruleSet?.Rules.Add(new HighlightingRule
        {
            Color = color,
            Regex = new Regex(JavaScriptLanguageDefinition.Operators.AllCharacters)
        });

        /// <summary>
        /// 添加变量规则
        /// </summary>
        private static void AddVariableRule(HighlightingRuleSet? ruleSet, HighlightingColor color) => ruleSet?.Rules.Add(new HighlightingRule
        {
            Color = color,
            Regex = VariableRegex()
        });

        [GeneratedRegex(@"/\*")]
        private static partial Regex MultilineCommentSpanStartRegex();
        [GeneratedRegex(@"\*/")]
        private static partial Regex MultilineCommentSpanEndRegex();
        [GeneratedRegex("\"")]
        private static partial Regex DoubleQuoteStringSpansStartRegex();
        [GeneratedRegex("(?<!\\\\)\"")]
        private static partial Regex DoubleQuoteStringSpansEndRegex();
        [GeneratedRegex("'")]
        private static partial Regex SingleQuoteStringSpansStartRegex();
        [GeneratedRegex(@"(?<!\\)'")]
        private static partial Regex SingleQuoteStringSpansEndRegex();
        [GeneratedRegex("`")]
        private static partial Regex TemplateStringSpansStartRegex();
        [GeneratedRegex(@"(?<!\\)`")]
        private static partial Regex TemplateStringSpansEndRegex();
        [GeneratedRegex(@"//.*$", RegexOptions.Multiline)]
        private static partial Regex SingleLineCommentRegex();
        [GeneratedRegex(@"\b0[xX][0-9a-fA-F]+|(\b\d+(\.\d+)?|\.\d+)([eE][+-]?\d+)?")]
        private static partial Regex NumberRegex();
        [GeneratedRegex(@"\b[a-zA-Z_$][a-zA-Z0-9_$]*(?=\s*\()")]
        private static partial Regex FunctionCallRegex();
        [GeneratedRegex(@"(?<=\.)[a-zA-Z_$][a-zA-Z0-9_$]*")]
        private static partial Regex PropertyAccessRegex();
        [GeneratedRegex(@"\b[a-zA-Z_$][a-zA-Z0-9_$]*\b")]
        private static partial Regex VariableRegex();
    }

    /// <summary>
    /// 自定义高亮定义实现
    /// </summary>
    internal class CustomHighlightingDefinition(string name) : IHighlightingDefinition
    {
        public string Name => name;

        public HighlightingRuleSet? MainRuleSet { get; set; }

        public IEnumerable<HighlightingColor> NamedHighlightingColors => NamedHighlightingColorsList;

        public IList<HighlightingColor> NamedHighlightingColorsList { get; } = [];

        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();

        public HighlightingColor? GetNamedColor(string colorName) => NamedHighlightingColorsList.FirstOrDefault(c => c.Name == colorName);

        public HighlightingRuleSet? GetNamedRuleSet(string name) => null; // 简化实现，只使用主规则集
    }

    /// <summary>
    /// 简单的高亮画刷
    /// </summary>
    public class SimpleHighlightingBrush : HighlightingBrush
    {
        private readonly SolidColorBrush _brush;

        public SimpleHighlightingBrush(Color color)
        {
            _brush = new SolidColorBrush(color);
            _brush.Freeze();
        }

        public override Brush GetBrush(ITextRunConstructionContext context) => _brush;

        public override Color? GetColor(ITextRunConstructionContext context) => _brush.Color;
    }
}