using DrugSearcher.Models;
using ICSharpCode.AvalonEdit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using ToolTip = System.Windows.Controls.ToolTip;

namespace DrugSearcher.Services
{
    /// <summary>
    /// 错误工具提示服务
    /// </summary>
    public class ErrorToolTipService : IDisposable
    {
        private readonly TextEditor _textEditor;
        private readonly Dictionary<int, List<SyntaxError>> _lineErrors;
        private ToolTip _currentToolTip;
        private readonly Lock _lock = new();

        public ErrorToolTipService(TextEditor textEditor)
        {
            _textEditor = textEditor;
            _lineErrors = new Dictionary<int, List<SyntaxError>>();

            // 订阅事件
            _textEditor.MouseHover += OnMouseHover;
            _textEditor.MouseHoverStopped += OnMouseHoverStopped;
            _textEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        }

        /// <summary>
        /// 更新错误信息
        /// </summary>
        public void UpdateErrors(List<SyntaxError> errors)
        {
            lock (_lock)
            {
                _lineErrors.Clear();

                foreach (var error in errors)
                {
                    if (!_lineErrors.TryGetValue(error.Line, out var value))
                    {
                        value = [];
                        _lineErrors[error.Line] = value;
                    }

                    value.Add(error);
                }
            }
        }

        /// <summary>
        /// 鼠标悬停事件处理
        /// </summary>
        private void OnMouseHover(object sender, MouseEventArgs e)
        {
            var position = _textEditor.GetPositionFromPoint(e.GetPosition(_textEditor));
            if (position.HasValue)
            {
                ShowErrorToolTip(position.Value);
            }
        }

        /// <summary>
        /// 鼠标停止悬停事件处理
        /// </summary>
        private void OnMouseHoverStopped(object sender, MouseEventArgs e)
        {
            HideToolTip();
        }

        /// <summary>
        /// 光标位置改变事件处理
        /// </summary>
        private void OnCaretPositionChanged(object sender, EventArgs e)
        {
            var caretPosition = _textEditor.TextArea.Caret.Position;
            ShowErrorToolTip(caretPosition);
        }

        /// <summary>
        /// 显示错误工具提示
        /// </summary>
        private void ShowErrorToolTip(TextViewPosition position)
        {
            try
            {
                var line = position.Line;

                lock (_lock)
                {
                    if (_lineErrors.TryGetValue(line, out var errors) && errors.Count > 0)
                    {
                        // 检查是否在错误范围内
                        var column = position.Column;
                        var relevantErrors = errors.Where(e => IsInErrorRange(e, column)).ToList();

                        if (relevantErrors.Count > 0)
                        {
                            ShowToolTipForErrors(relevantErrors, position);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示错误提示时出错: {ex.Message}");
            }

            HideToolTip();
        }

        /// <summary>
        /// 检查是否在错误范围内
        /// </summary>
        private bool IsInErrorRange(SyntaxError error, int column)
        {
            // 根据错误类型和消息判断范围
            var errorLength = EstimateErrorLength(error);
            return column >= error.Column && column <= error.Column + errorLength;
        }

        /// <summary>
        /// 估算错误长度
        /// </summary>
        private int EstimateErrorLength(SyntaxError error)
        {
            // 根据错误消息估算长度
            if (error.Message.Contains("未闭合"))
            {
                return 50; // 未闭合的错误通常影响到行尾
            }

            // 尝试从错误消息中提取标识符
            var match = System.Text.RegularExpressions.Regex.Match(error.Message, @"[：:]\s*(\w+)");
            if (match.Success)
            {
                return match.Groups[1].Value.Length;
            }

            return 10; // 默认长度
        }

        /// <summary>
        /// 显示错误工具提示
        /// </summary>
        private void ShowToolTipForErrors(List<SyntaxError> errors, TextViewPosition position)
        {
            HideToolTip();

            var toolTip = new ToolTip
            {
                Placement = PlacementMode.Mouse,
                PlacementTarget = _textEditor,
                IsOpen = true
            };

            // 创建工具提示内容
            var stackPanel = new StackPanel
            {
                Margin = new Thickness(5)
            };

            foreach (var error in errors)
            {
                var errorPanel = new DockPanel
                {
                    Margin = new Thickness(0, 2, 0, 2)
                };

                // 错误图标
                var icon = new TextBlock
                {
                    Text = GetSeverityIcon(error.Severity),
                    FontFamily = new FontFamily("Segoe UI Symbol"),
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = GetSeverityBrush(error.Severity)
                };
                DockPanel.SetDock(icon, Dock.Left);
                errorPanel.Children.Add(icon);

                // 错误消息
                var message = new TextBlock
                {
                    Text = error.Message,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 400
                };
                errorPanel.Children.Add(message);

                stackPanel.Children.Add(errorPanel);
            }

            toolTip.Content = stackPanel;
            _currentToolTip = toolTip;
        }

        /// <summary>
        /// 获取严重性图标
        /// </summary>
        private string GetSeverityIcon(SyntaxErrorSeverity severity)
        {
            return severity switch
            {
                SyntaxErrorSeverity.Error => "⛔",
                SyntaxErrorSeverity.Warning => "⚠",
                SyntaxErrorSeverity.Info => "ℹ",
                _ => "•"
            };
        }

        /// <summary>
        /// 获取严重性画刷
        /// </summary>
        private Brush GetSeverityBrush(SyntaxErrorSeverity severity)
        {
            return severity switch
            {
                SyntaxErrorSeverity.Error => Brushes.Red,
                SyntaxErrorSeverity.Warning => Brushes.Orange,
                SyntaxErrorSeverity.Info => Brushes.Blue,
                _ => Brushes.Gray
            };
        }

        /// <summary>
        /// 隐藏工具提示
        /// </summary>
        private void HideToolTip()
        {
            if (_currentToolTip != null)
            {
                _currentToolTip.IsOpen = false;
                _currentToolTip = null;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            HideToolTip();

            if (_textEditor != null)
            {
                _textEditor.MouseHover -= OnMouseHover;
                _textEditor.MouseHoverStopped -= OnMouseHoverStopped;
                _textEditor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            }
        }
    }
}