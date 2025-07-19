using Microsoft.Xaml.Behaviors;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DataObject = System.Windows.DataObject;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace DrugSearcher.Behaviors;

public partial class NumericInputBehavior : Behavior<TextBox>
{
    // 允许负数和小数的正则表达式
    private static readonly Regex NumericRegex = GetNumericRegex();
    private string _previousValidText = string.Empty;
    private bool _isUpdating;

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.PreviewTextInput += OnPreviewTextInput;
        AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;
        DataObject.AddPastingHandler(AssociatedObject, OnPasting);
        AssociatedObject.TextChanged += OnTextChanged;
        AssociatedObject.LostFocus += OnLostFocus;

        // 保存初始值
        _previousValidText = AssociatedObject.Text;
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.PreviewTextInput -= OnPreviewTextInput;
        AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
        DataObject.RemovePastingHandler(AssociatedObject, OnPasting);
        AssociatedObject.TextChanged -= OnTextChanged;
        AssociatedObject.LostFocus -= OnLostFocus;
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        // 检查是否是输入法组合状态
        if (e.TextComposition is { CompositionText: not null })
        {
            // 对于输入法，我们在 TextChanged 事件中处理
            return;
        }

        // 获取当前文本框的内容
        var currentText = textBox.Text;
        var selectionStart = textBox.SelectionStart;
        var selectionLength = textBox.SelectionLength;

        // 构建新的文本（考虑选中替换的情况）
        var newText = currentText[..selectionStart] +
                      e.Text +
                      currentText[(selectionStart + selectionLength)..];

        // 检查新文本是否符合数字格式
        if (!IsValidNumericInput(newText))
        {
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // 允许控制键
        if (e.Key
            is Key.Delete or Key.Back or Key.Tab
            or Key.Enter or Key.Escape or Key.Left
            or Key.Right or Key.Home or Key.End)
        {
            return;
        }

        // 允许Ctrl组合键
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            return;
        }

        // 阻止空格键
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var pasteText = (string?)e.DataObject.GetData(typeof(string));

            if (sender is not TextBox textBox) return;
            var currentText = textBox.Text;
            var selectionStart = textBox.SelectionStart;
            var selectionLength = textBox.SelectionLength;

            // 构建粘贴后的文本
            var newText = currentText[..selectionStart] +
                          pasteText +
                          currentText[(selectionStart + selectionLength)..];

            if (IsValidNumericInput(newText)) return;
            // 尝试提取有效数字
            var validPasteText = ExtractValidNumericText(pasteText);
            if (!string.IsNullOrEmpty(validPasteText))
            {
                // 用有效的数字替换剪贴板内容
                e.DataObject.SetData(typeof(string), validPasteText);
            }
            else
            {
                e.CancelCommand();
            }
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (sender is not TextBox textBox) return;

        var currentText = textBox.Text;
        var currentCaretIndex = textBox.CaretIndex;

        // 如果当前文本有效，保存它
        if (IsValidNumericInput(currentText))
        {
            _previousValidText = currentText;
        }
        else if (!string.IsNullOrEmpty(currentText))
        {
            _isUpdating = true;

            // 计算当前光标前的文本长度
            var textBeforeCaret = currentText[..Math.Min(currentCaretIndex, currentText.Length)];

            // 提取有效的数字部分
            var validText = ExtractValidNumericText(currentText);
            var validTextBeforeCaret = ExtractValidNumericText(textBeforeCaret);

            if (string.IsNullOrEmpty(validText))
            {
                // 如果无法提取有效文本，恢复到上一个有效值
                textBox.Text = _previousValidText;
                // 将光标放在文本末尾
                textBox.CaretIndex = textBox.Text.Length;
            }
            else
            {
                // 使用提取的有效文本
                textBox.Text = validText;
                _previousValidText = validText;

                // 计算新的光标位置
                // 光标应该在处理后的有效文本的相应位置
                var newCaretIndex = validTextBeforeCaret.Length;

                // 如果原始光标在某个数字之后，确保新光标也在对应的数字之后
                if (currentCaretIndex > 0 && currentCaretIndex <= currentText.Length)
                {
                    // 计算原始位置前有多少个有效字符
                    var validCharCount = 0;
                    for (var i = 0; i < currentCaretIndex && i < currentText.Length; i++)
                    {
                        if (IsValidChar(currentText[i], i == 0, validText.Contains('.')))
                        {
                            validCharCount++;
                        }
                    }
                    newCaretIndex = Math.Min(validCharCount, validText.Length);
                }

                textBox.CaretIndex = newCaretIndex;
            }

            _isUpdating = false;
        }
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox) return;

        // 失去焦点时，确保文本是有效的数字
        if (!string.IsNullOrEmpty(textBox.Text) && !IsValidNumericInput(textBox.Text))
        {
            textBox.Text = _previousValidText;
        }
    }

    private static bool IsValidChar(char ch, bool isFirstChar, bool hasDecimalPoint)
    {
        if (ch == '-' && isFirstChar) return true;
        if (char.IsDigit(ch)) return true;
        if (ch == '.' && !hasDecimalPoint) return true;
        return false;
    }

    private static string ExtractValidNumericText(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // 尝试提取有效的数字部分
        var result = string.Empty;
        var hasDecimalPoint = false;
        var hasNegativeSign = false;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];

            // 处理负号
            if (ch == '-' && i == 0 && !hasNegativeSign)
            {
                result += ch;
                hasNegativeSign = true;
            }
            // 处理数字
            else if (char.IsDigit(ch))
            {
                result += ch;
            }
            // 处理小数点
            else if (ch == '.' && !hasDecimalPoint)
            {
                // 如果小数点前没有数字，自动补0
                if (result.Length == 0 || result is ['-'])
                {
                    result += "0";
                }
                result += ch;
                hasDecimalPoint = true;
            }
        }

        return result;
    }

    private static bool IsValidNumericInput(string? input)
    {
        // 允许空字符串
        if (string.IsNullOrEmpty(input))
            return true;

        switch (input)
        {
            // 允许单独的负号
            case "-":
            // 允许负号后跟小数点
            case "-.":
            // 允许单独的小数点（会被解释为0.）
            case ".":
                return true;
        }

        // 检查是否只有一个小数点
        var dotCount = input.Split('.').Length - 1;
        if (dotCount > 1)
            return false;

        // 检查负号是否在开头
        if (input.Contains('-') && !input.AsSpan().StartsWith(['-'], StringComparison.Ordinal))
            return false;

        // 使用正则表达式验证
        return NumericRegex.IsMatch(input);
    }

    [GeneratedRegex(@"^-?[0-9]*\.?[0-9]*$", RegexOptions.Compiled)]
    private static partial Regex GetNumericRegex();
}