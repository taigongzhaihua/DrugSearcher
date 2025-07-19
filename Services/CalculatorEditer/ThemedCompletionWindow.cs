using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;

namespace DrugSearcher.Services;

/// <summary>
/// 主题感知的代码提示窗口
/// </summary>
public class ThemedCompletionWindow : CompletionWindow
{
    private string _lastFilter = string.Empty;
    public ThemedCompletionWindow(TextArea textArea) : base(textArea)
    {
        ApplyTheme();
        ConfigureWindowChrome();

        // 监听文本变化以实时更新过滤
        textArea.TextEntered += OnTextAreaTextEntered;
        textArea.TextEntering += OnTextAreaTextEntering;
    }

    private void ApplyTheme()
    {
        try
        {
            // 应用主题样式
            var backgroundBrush = Application.Current.FindResource("BackgroundBrush") as Brush;
            var surfaceBrush = Application.Current.FindResource("SurfaceBrush") as Brush;
            var primaryTextBrush = Application.Current.FindResource("PrimaryTextBrush") as Brush;
            _ = Application.Current.FindResource("SecondaryTextBrush") as Brush;
            var borderBrush = Application.Current.FindResource("BorderBrush") as Brush;
            var accentBrush = Application.Current.FindResource("AccentBrush") as Brush;
            var primaryBrush = Application.Current.FindResource("PrimaryBrush") as Brush;

            // 设置窗口背景
            if (backgroundBrush != null)
            {
                Background = backgroundBrush;
            }

            // 设置边框
            if (borderBrush != null)
            {
                BorderBrush = borderBrush;
                BorderThickness = new Thickness(1);
            }

            // 设置列表框样式
            var listBox = CompletionList?.ListBox;
            if (listBox != null)
            {
                if (surfaceBrush != null)
                {
                    listBox.Background = surfaceBrush;
                }
                if (primaryTextBrush != null)
                {
                    listBox.Foreground = primaryTextBrush;
                }
                if (borderBrush != null)
                {
                    listBox.BorderBrush = borderBrush;
                }

                // 设置列表项样式
                var itemContainerStyle = new Style(typeof(ListBoxItem));

                // 普通状态
                if (surfaceBrush != null)
                {
                    itemContainerStyle.Setters.Add(new Setter(BackgroundProperty, surfaceBrush));
                }
                if (primaryTextBrush != null)
                {
                    itemContainerStyle.Setters.Add(new Setter(ForegroundProperty, primaryTextBrush));
                }

                itemContainerStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(6, 2, 6, 2)));
                itemContainerStyle.Setters.Add(new Setter(MarginProperty, new Thickness(0)));
                itemContainerStyle.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0)));

                // 鼠标悬停状态
                var mouseOverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
                if (accentBrush != null)
                {
                    mouseOverTrigger.Setters.Add(new Setter(BackgroundProperty, accentBrush));
                }
                if (backgroundBrush != null)
                {
                    mouseOverTrigger.Setters.Add(new Setter(ForegroundProperty, backgroundBrush));
                }
                itemContainerStyle.Triggers.Add(mouseOverTrigger);

                // 选中状态
                var selectedTrigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
                if (accentBrush != null)
                {
                    selectedTrigger.Setters.Add(new Setter(BackgroundProperty, accentBrush));
                }
                if (backgroundBrush != null)
                {
                    selectedTrigger.Setters.Add(new Setter(ForegroundProperty, primaryBrush));
                }
                itemContainerStyle.Triggers.Add(selectedTrigger);

                listBox.ItemContainerStyle = itemContainerStyle;
            }

            // 设置窗口阴影效果
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 315,
                ShadowDepth = 3,
                BlurRadius = 5,
                Opacity = 0.3
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用主题失败: {ex.Message}");
        }
    }

    private void ConfigureWindowChrome()
    {
        var windowChrome = new WindowChrome
        {
            ResizeBorderThickness = new Thickness(5),
            CaptionHeight = 0,
            CornerRadius = new CornerRadius(8),
            GlassFrameThickness = new Thickness(-1),
            NonClientFrameEdges = NonClientFrameEdges.Bottom |
                                  NonClientFrameEdges.Left |
                                  NonClientFrameEdges.Right,
            UseAeroCaptionButtons = false
        };

        WindowChrome.SetWindowChrome(this, windowChrome);
        Debug.WriteLine("窗口Chrome配置完成");
    }
    private void OnTextAreaTextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        // 记录当前过滤文本
        if (StartOffset < 0 || TextArea.Document.TextLength < StartOffset) return;
        var length = TextArea.Caret.Offset - StartOffset;
        if (length >= 0)
        {
            _lastFilter = TextArea.Document.GetText(StartOffset, length);
        }
    }

    private void OnTextAreaTextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        // 更新过滤
        if (e.Text.Length != 1 || !char.IsLetterOrDigit(e.Text[0])) return;
        var newFilter = _lastFilter + e.Text;
        CompletionList.SelectItem(newFilter);
    }
    protected override void OnClosed(EventArgs e)
    {
        // 清理事件订阅
        TextArea.TextEntered -= OnTextAreaTextEntered;
        TextArea.TextEntering -= OnTextAreaTextEntering;

        base.OnClosed(e);
    }
}