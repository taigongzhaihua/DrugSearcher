using DrugSearcher.Models;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace DrugSearcher.Services
{
    /// <summary>
    /// 语法错误标记服务
    /// </summary>
    public class SyntaxErrorMarkerService : IDisposable
    {
        private readonly TextEditor _textEditor;
        private readonly ErrorUnderlineRenderer _underlineRenderer;
        private List<SyntaxError> _currentErrors = [];

        public SyntaxErrorMarkerService(TextEditor textEditor)
        {
            _textEditor = textEditor;
            _underlineRenderer = new ErrorUnderlineRenderer(textEditor);

            // 添加渲染器到TextView
            _textEditor.TextArea.TextView.BackgroundRenderers.Add(_underlineRenderer);
        }

        /// <summary>
        /// 更新错误标记
        /// </summary>
        public void UpdateErrorMarkers(List<SyntaxError> errors)
        {
            _currentErrors = errors ?? [];
            _underlineRenderer.UpdateErrors(_currentErrors);

            // 重绘视图
            _textEditor.TextArea.TextView.InvalidateVisual();
        }

        /// <summary>
        /// 清除错误标记
        /// </summary>
        public void ClearErrorMarkers()
        {
            _currentErrors.Clear();
            _underlineRenderer.UpdateErrors(_currentErrors);
            _textEditor.TextArea.TextView.InvalidateVisual();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_underlineRenderer != null && _textEditor?.TextArea?.TextView != null)
            {
                _textEditor.TextArea.TextView.BackgroundRenderers.Remove(_underlineRenderer);
            }
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// 错误下划线渲染器
    /// </summary>
    public partial class ErrorUnderlineRenderer(TextEditor textEditor) : IBackgroundRenderer
    {
        private readonly TextEditor _textEditor = textEditor;
        private List<SyntaxError> _errors = [];
        private readonly Lock _lock = new();

        public KnownLayer Layer => KnownLayer.Selection;

        public void UpdateErrors(List<SyntaxError> errors)
        {
            lock (_lock)
            {
                _errors = [.. errors ?? []];
            }
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView.Document == null)
                return;

            lock (_lock)
            {
                foreach (var error in _errors)
                {
                    DrawErrorUnderline(textView, drawingContext, error);
                }
            }
        }

        private static void DrawErrorUnderline(TextView textView, DrawingContext drawingContext, SyntaxError error)
        {
            try
            {
                var document = textView.Document;

                // 验证行号
                if (error.Line <= 0 || error.Line > document.LineCount)
                    return;

                var line = document.GetLineByNumber(error.Line);
                var lineText = document.GetText(line.Offset, line.Length);

                // 计算错误的起始和结束位置
                var startColumn = Math.Max(1, error.Column);
                var errorLength = CalculateErrorLength(lineText, startColumn - 1, error.Message);

                // 确保不超出行的范围
                if (startColumn > lineText.Length)
                    startColumn = Math.Max(1, lineText.Length);

                var startOffset = line.Offset + startColumn - 1;
                var endOffset = Math.Min(startOffset + errorLength, line.EndOffset);

                // 确保偏移量有效
                if (startOffset >= document.TextLength || endOffset > document.TextLength)
                    return;

                var startLocation = document.GetLocation(startOffset);
                var endLocation = document.GetLocation(endOffset);

                // 获取视觉位置
                var startPos = new TextViewPosition(startLocation.Line, startLocation.Column);
                var endPos = new TextViewPosition(endLocation.Line, endLocation.Column);

                var startPoint = textView.GetVisualPosition(startPos, VisualYPosition.LineBottom);
                var endPoint = textView.GetVisualPosition(endPos, VisualYPosition.LineBottom);

                // 检查点是否在可视区域内
                if (startPoint.Y < 0 || startPoint.Y > textView.ActualHeight)
                    return;

                // 创建错误下划线
                DrawWavyLine(drawingContext, startPoint, endPoint, GetErrorBrush(error.Severity));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"绘制错误下划线时出错: {ex.Message}");
            }
        }

        private static void DrawWavyLine(DrawingContext drawingContext, Point start, Point end, Brush brush)
        {
            var pen = new Pen(brush, 1.5);

            // 创建波浪线路径
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(start, false, false);

                var waveHeight = 2.0;
                var waveLength = 4.0;
                var currentX = start.X;

                while (currentX < end.X)
                {
                    var nextX = Math.Min(currentX + waveLength / 2, end.X);
                    var midX = (currentX + nextX) / 2;

                    context.BezierTo(
                        new Point(midX, start.Y - waveHeight),
                        new Point(midX, start.Y - waveHeight),
                        new Point(nextX, start.Y),
                        true, false);

                    currentX = nextX;
                }
            }

            geometry.Freeze();
            drawingContext.DrawGeometry(null, pen, geometry);
        }

        private static int CalculateErrorLength(string lineText, int startIndex, string? errorMessage)
        {
            if (startIndex < 0 || startIndex >= lineText.Length)
                return 1;

            // 根据错误类型计算长度
            if (errorMessage != null && errorMessage.Contains("未闭合"))
            {
                return lineText.Length - startIndex;
            }

            // 尝试从错误消息中提取标识符
            if (errorMessage != null)
            {
                var match = CaculateErrorRegex().Match(errorMessage);
                if (match.Success)
                {
                    return match.Groups[1].Value.Length;
                }
            }

            // 查找下一个非标识符字符
            var length = 0;
            for (var i = startIndex; i < lineText.Length; i++)
            {
                var ch = lineText[i];
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '$')
                {
                    length++;
                }
                else
                {
                    break;
                }
            }

            return Math.Max(1, length);
        }

        private static SolidColorBrush GetErrorBrush(SyntaxErrorSeverity severity) => severity switch
        {
            SyntaxErrorSeverity.Error => Brushes.Red,
            SyntaxErrorSeverity.Warning => Brushes.Orange,
            SyntaxErrorSeverity.Info => Brushes.Blue,
            _ => Brushes.Red
        };
        [System.Text.RegularExpressions.GeneratedRegex(@"[：:]\s*(\w+)")]
        private static partial System.Text.RegularExpressions.Regex CaculateErrorRegex();
    }
}