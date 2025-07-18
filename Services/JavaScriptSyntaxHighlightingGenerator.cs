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
    public class JavaScriptSyntaxHighlightingGenerator
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
        private static void AddMultilineCommentSpan(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            var multilineComment = new HighlightingSpan
            {
                StartExpression = new Regex(@"/\*"),
                EndExpression = new Regex(@"\*/"),
                SpanColor = color,
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true
            };
            ruleSet.Spans.Add(multilineComment);
        }

        /// <summary>
        /// 添加字符串规则
        /// </summary>
        private static void AddStringSpans(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            // 双引号字符串 - 使用正则表达式处理转义字符
            var doubleQuoteString = new HighlightingSpan
            {
                StartExpression = new Regex("\""),
                EndExpression = new Regex("(?<!\\\\)\""),
                SpanColor = color,
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true
            };
            ruleSet.Spans.Add(doubleQuoteString);

            // 单引号字符串
            var singleQuoteString = new HighlightingSpan
            {
                StartExpression = new Regex("'"),
                EndExpression = new Regex("(?<!\\\\)'"),
                SpanColor = color,
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true
            };
            ruleSet.Spans.Add(singleQuoteString);

            // 模板字符串
            var templateString = new HighlightingSpan
            {
                StartExpression = new Regex("`"),
                EndExpression = new Regex("(?<!\\\\)`"),
                SpanColor = color,
                SpanColorIncludesStart = true,
                SpanColorIncludesEnd = true
            };
            ruleSet.Spans.Add(templateString);
        }

        /// <summary>
        /// 添加单行注释规则
        /// </summary>
        private static void AddSingleLineCommentRule(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            ruleSet.Rules.Add(new HighlightingRule
            {
                Color = color,
                Regex = new Regex(@"//.*$", RegexOptions.Multiline)
            });
        }

        /// <summary>
        /// 添加数字规则
        /// </summary>
        private static void AddNumberRule(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            ruleSet.Rules.Add(new HighlightingRule
            {
                Color = color,
                Regex = new Regex(@"\b0[xX][0-9a-fA-F]+|(\b\d+(\.\d+)?|\.\d+)([eE][+-]?\d+)?")
            });
        }

        /// <summary>
        /// 添加关键字规则
        /// </summary>
        private static void AddKeywordRules(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            var keywords = JavaScriptLanguageDefinition.Keywords.All;
            if (!keywords.Any()) return;

            // 创建关键字正则表达式
            var keywordPattern = string.Join("|", keywords.Select(k => @"\b" + Regex.Escape(k) + @"\b"));

            ruleSet.Rules.Add(new HighlightingRule
            {
                Color = color,
                Regex = new Regex(keywordPattern)
            });
        }

        /// <summary>
        /// 添加内置函数和对象规则
        /// </summary>
        private static void AddBuiltInRules(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            // 内置函数
            var builtInFunctions = JavaScriptLanguageDefinition.BuiltIns.GlobalFunctions.Keys;
            if (builtInFunctions.Any())
            {
                var functionPattern = string.Join("|", builtInFunctions.Select(f => @"\b" + Regex.Escape(f) + @"\b"));
                ruleSet.Rules.Add(new HighlightingRule
                {
                    Color = color,
                    Regex = new Regex(functionPattern)
                });
            }

            // 内置对象
            var builtInObjects = JavaScriptLanguageDefinition.BuiltIns.GlobalObjects.Keys;
            if (builtInObjects.Any())
            {
                var objectPattern = string.Join("|", builtInObjects.Select(o => @"\b" + Regex.Escape(o) + @"\b"));
                ruleSet.Rules.Add(new HighlightingRule
                {
                    Color = color,
                    Regex = new Regex(objectPattern)
                });
            }
        }

        /// <summary>
        /// 添加自定义函数规则
        /// </summary>
        private static void AddCustomFunctionRules(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            var customFunctions = JavaScriptLanguageDefinition.CustomFunctions.Names;
            if (!customFunctions.Any()) return;

            var customFunctionPattern = string.Join("|", customFunctions.Select(f => @"\b" + Regex.Escape(f) + @"\b"));
            ruleSet.Rules.Add(new HighlightingRule
            {
                Color = color,
                Regex = new Regex(customFunctionPattern)
            });
        }

        /// <summary>
        /// 添加函数调用规则
        /// </summary>
        private static void AddFunctionCallRule(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            ruleSet.Rules.Add(new HighlightingRule
            {
                Color = color,
                Regex = new Regex(@"\b[a-zA-Z_$][a-zA-Z0-9_$]*(?=\s*\()")
            });
        }

        /// <summary>
        /// 添加属性访问规则
        /// </summary>
        private static void AddPropertyAccessRule(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            ruleSet.Rules.Add(new HighlightingRule
            {
                Color = color,
                Regex = new Regex(@"(?<=\.)[a-zA-Z_$][a-zA-Z0-9_$]*")
            });
        }

        /// <summary>
        /// 添加操作符规则
        /// </summary>
        private static void AddOperatorRule(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            ruleSet.Rules.Add(new HighlightingRule
            {
                Color = color,
                Regex = new Regex(JavaScriptLanguageDefinition.Operators.AllCharacters)
            });
        }

        /// <summary>
        /// 添加变量规则
        /// </summary>
        private static void AddVariableRule(HighlightingRuleSet ruleSet, HighlightingColor color)
        {
            ruleSet.Rules.Add(new HighlightingRule
            {
                Color = color,
                Regex = new Regex(@"\b[a-zA-Z_$][a-zA-Z0-9_$]*\b")
            });
        }
    }

    /// <summary>
    /// 自定义高亮定义实现
    /// </summary>
    internal class CustomHighlightingDefinition : IHighlightingDefinition
    {
        private readonly string _name;
        private readonly IList<HighlightingColor> _namedHighlightingColors = new List<HighlightingColor>();
        private readonly IDictionary<string, string> _properties = new Dictionary<string, string>();

        public CustomHighlightingDefinition(string name)
        {
            _name = name;
        }

        public string Name => _name;

        public HighlightingRuleSet MainRuleSet { get; set; }

        public IEnumerable<HighlightingColor> NamedHighlightingColors => _namedHighlightingColors;

        public IList<HighlightingColor> NamedHighlightingColorsList => _namedHighlightingColors;

        public IDictionary<string, string> Properties => _properties;

        public HighlightingColor GetNamedColor(string name)
        {
            return _namedHighlightingColors.FirstOrDefault(c => c.Name == name);
        }

        public HighlightingRuleSet GetNamedRuleSet(string name)
        {
            return null; // 简化实现，只使用主规则集
        }
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

        public override Brush GetBrush(ITextRunConstructionContext context)
        {
            return _brush;
        }

        public override Color? GetColor(ITextRunConstructionContext context)
        {
            return _brush.Color;
        }
    }
}