using DrugSearcher.Models;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;

namespace DrugSearcher.Services
{
    /// <summary>
    /// 语法错误文本标记服务
    /// </summary>
    public class SyntaxErrorTextMarkerService : IDisposable
    {
        private readonly TextEditor _textEditor;
        private readonly TextSegmentCollection<ErrorTextSegment> _errorSegments;
        private readonly ErrorTextColorizer _colorizer;

        public SyntaxErrorTextMarkerService(TextEditor textEditor)
        {
            _textEditor = textEditor;
            _errorSegments = new TextSegmentCollection<ErrorTextSegment>(_textEditor.Document);

            _colorizer = new ErrorTextColorizer(_errorSegments);
            _textEditor.TextArea.TextView.LineTransformers.Add(_colorizer);
        }

        /// <summary>
        /// 更新错误标记
        /// </summary>
        public void UpdateErrorMarkers(List<SyntaxError> errors)
        {
            _errorSegments.Clear();

            foreach (var error in errors)
            {
                try
                {
                    var document = _textEditor.Document;

                    if (error.Line <= 0 || error.Line > document.LineCount)
                        continue;

                    var line = document.GetLineByNumber(error.Line);
                    var lineText = document.GetText(line.Offset, line.Length);

                    // 计算错误位置和长度
                    var startColumn = Math.Max(1, error.Column);
                    var errorLength = CalculateErrorLength(lineText, startColumn - 1, error.Message);

                    if (startColumn > lineText.Length)
                        startColumn = Math.Max(1, lineText.Length);

                    var startOffset = line.Offset + startColumn - 1;
                    var endOffset = Math.Min(startOffset + errorLength, line.EndOffset);

                    if (startOffset >= document.TextLength || endOffset > document.TextLength)
                        continue;

                    // 创建错误段
                    var segment = new ErrorTextSegment
                    {
                        StartOffset = startOffset,
                        EndOffset = endOffset,
                        Error = error
                    };

                    _errorSegments.Add(segment);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"添加错误标记时出错: {ex.Message}");
                }
            }

            // 触发重绘
            _textEditor.TextArea.TextView.Redraw();
        }

        private int CalculateErrorLength(string lineText, int startIndex, string errorMessage)
        {
            if (startIndex < 0 || startIndex >= lineText.Length)
                return 1;

            // 未闭合的错误标记到行尾
            if (errorMessage.Contains("未闭合"))
                return lineText.Length - startIndex;

            // 尝试提取标识符
            var match = System.Text.RegularExpressions.Regex.Match(errorMessage, @"[：:]\s*([a-zA-Z_$][a-zA-Z0-9_$]*)");
            if (match.Success)
                return match.Groups[1].Value.Length;

            // 找到标识符的结束位置
            var length = 0;
            for (int i = startIndex; i < lineText.Length; i++)
            {
                var ch = lineText[i];
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '$')
                    length++;
                else
                    break;
            }

            return Math.Max(1, length);
        }

        public void Dispose()
        {
            if (_colorizer != null && _textEditor?.TextArea?.TextView != null)
            {
                _textEditor.TextArea.TextView.LineTransformers.Remove(_colorizer);
            }
            _errorSegments?.Clear();
        }
    }

    /// <summary>
    /// 错误文本段
    /// </summary>
    public class ErrorTextSegment : TextSegment
    {
        public SyntaxError Error { get; set; }
    }

    /// <summary>
    /// 错误文本着色器
    /// </summary>
    public class ErrorTextColorizer : DocumentColorizingTransformer
    {
        private readonly TextSegmentCollection<ErrorTextSegment> _errorSegments;

        public ErrorTextColorizer(TextSegmentCollection<ErrorTextSegment> errorSegments)
        {
            _errorSegments = errorSegments;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            var lineStart = line.Offset;
            var lineEnd = lineStart + line.Length;

            foreach (var segment in _errorSegments.FindOverlappingSegments(lineStart, line.Length))
            {
                var startOffset = Math.Max(segment.StartOffset, lineStart);
                var endOffset = Math.Min(segment.EndOffset, lineEnd);

                if (startOffset < endOffset)
                {
                    ChangeLinePart(startOffset, endOffset, element =>
                    {
                        // 添加下划线装饰
                        var decorations = new TextDecorationCollection();
                        var underline = new TextDecoration
                        {
                            Location = TextDecorationLocation.Underline,
                            Pen = new Pen(GetErrorBrush(segment.Error.Severity), 1.5)
                            {
                                DashStyle = new DashStyle([2, 2], 0)
                            }
                        };
                        decorations.Add(underline);
                        element.TextRunProperties.SetTextDecorations(decorations);
                    });
                }
            }
        }

        private Brush GetErrorBrush(SyntaxErrorSeverity severity)
        {
            return severity switch
            {
                SyntaxErrorSeverity.Error => Brushes.Red,
                SyntaxErrorSeverity.Warning => Brushes.Orange,
                SyntaxErrorSeverity.Info => Brushes.Blue,
                _ => Brushes.Red
            };
        }
    }
}