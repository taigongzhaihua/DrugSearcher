using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace DrugSearcher.Services
{
    /// <summary>
    /// 实时语法高亮器
    /// </summary>
    public class RealtimeSyntaxHighlighter : DocumentColorizingTransformer
    {
        private readonly JavaScriptDynamicContext _dynamicContext;
        private HashSet<string> _localIdentifiers = [];
        private readonly bool _isDarkTheme;

        public RealtimeSyntaxHighlighter(JavaScriptDynamicContext dynamicContext, bool isDarkTheme)
        {
            _dynamicContext = dynamicContext;
            _isDarkTheme = isDarkTheme;
            _dynamicContext.ContextChanged += OnContextChanged;
        }

        private void OnContextChanged(object sender, ContextChangedEventArgs e)
        {
            _localIdentifiers = _dynamicContext.GetAllLocalIdentifiers();
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            var text = CurrentContext.Document.GetText(line.Offset, line.Length);
            var lineStartOffset = line.Offset;

            // 查找所有标识符
            var identifierPattern = @"\b([a-zA-Z_$][a-zA-Z0-9_$]*)\b";
            var matches = Regex.Matches(text, identifierPattern);

            foreach (Match match in matches)
            {
                var identifier = match.Groups[1].Value;
                var startOffset = lineStartOffset + match.Index;
                var endOffset = startOffset + identifier.Length;

                // 跳过关键字（它们已经被基础语法高亮处理）
                if (JavaScriptLanguageDefinition.IsKeyword(identifier))
                    continue;

                // 检查是否是局部变量
                if (_localIdentifiers.Contains(identifier))
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
    }
}