using DrugSearcher.Models;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace DrugSearcher.Services
{
    /// <summary>
    /// 代码提示服务
    /// </summary>
    public class CodeCompletionService : IDisposable
    {
        private readonly TextEditor _textEditor;
        private readonly JavaScriptDynamicContext _dynamicContext;
        private readonly RealtimeJavaScriptCompletionProvider _completionProvider;
        private ThemedCompletionWindow? _completionWindow;
        private readonly HashSet<string> _declaredVariables = [];
        private readonly Lock _lock = new();

        public CodeCompletionService(TextEditor textEditor)
        {
            _textEditor = textEditor;
            _dynamicContext = new JavaScriptDynamicContext();
            _completionProvider = new RealtimeJavaScriptCompletionProvider(_dynamicContext);

            // 绑定事件
            _textEditor.TextArea.TextEntering += OnTextEntering;
            _textEditor.TextArea.TextEntered += OnTextEntered;
            _textEditor.TextArea.KeyDown += OnKeyDown;
            _textEditor.TextChanged += OnTextChanged;

            // 订阅动态上下文变化
            _dynamicContext.ContextChanged += OnContextChanged;
        }

        /// <summary>
        /// 更新参数列表
        /// </summary>
        public void UpdateParameters(List<DosageParameter> parameters)
        {
            _completionProvider.UpdateParameters(parameters);

            // 立即更新声明的变量列表
            UpdateDeclaredVariables(_textEditor.Text);
        }

        /// <summary>
        /// 处理文本变化
        /// </summary>
        private void OnTextChanged(object? sender, EventArgs e)
        {
            // 更新动态上下文
            _dynamicContext.AnalyzeCode(_textEditor.Text, _textEditor.CaretOffset);

            // 更新提供程序的当前代码
            _completionProvider.UpdateCurrentCode(_textEditor.Text);

            // 提取声明的变量
            UpdateDeclaredVariables(_textEditor.Text);
        }

        /// <summary>
        /// 处理上下文变化
        /// </summary>
        private void OnContextChanged(object? sender, ContextChangedEventArgs e)
        {
            // 更新完成提供程序的局部标识符
            _completionProvider.UpdateLocalIdentifiers(e.CurrentScope ?? []);
        }

        /// <summary>
        /// 更新声明的变量
        /// </summary>
        private void UpdateDeclaredVariables(string code)
        {
            lock (_lock)
            {
                _declaredVariables.Clear();

                // 首先添加参数变量
                var parameters = _completionProvider.GetParameters();
                foreach (var param in parameters.Where(param => !string.IsNullOrWhiteSpace(param.Name)))
                {
                    _declaredVariables.Add(param.Name);
                }

                // 提取代码中声明的变量
                var codeVariables = ExtractCodeVariables(code);
                foreach (var variable in codeVariables)
                {
                    _declaredVariables.Add(variable);
                }

                // 更新完成提供程序
                _completionProvider.UpdateDeclaredVariables(_declaredVariables);
            }
        }

        /// <summary>
        /// 提取代码中的变量（仅正确声明的）
        /// </summary>
        private HashSet<string> ExtractCodeVariables(string code)
        {
            var variables = new HashSet<string>();
            var lines = code.Split('\n');

            foreach (var line in lines)
            {
                // 跳过注释
                if (line.Trim().StartsWith("//") || line.Trim().StartsWith("/*"))
                    continue;

                // var/let/const 声明
                ExtractDeclarations(line, variables);

                // 函数声明
                ExtractFunctionDeclarations(line, variables);
            }

            return variables;
        }
        /// <summary>
        /// 提取所有变量（包括参数定义的变量）
        /// </summary>
        private HashSet<string> ExtractAllVariables(string code)
        {
            var variables = new HashSet<string>();
            var lines = code.Split('\n');

            // 首先添加参数定义的变量
            if (_completionProvider != null)
            {
                var parameters = _completionProvider.GetParameters();
                foreach (var param in parameters.Where(param => !string.IsNullOrWhiteSpace(param.Name)))
                {
                    variables.Add(param.Name);
                }
            }

            // 逐行分析，累积变量
            foreach (var line in lines)
            {
                // 跳过注释
                if (line.Trim().StartsWith("//") || line.Trim().StartsWith("/*"))
                    continue;

                // var/let/const 声明
                ExtractDeclarations(line, variables);

                // 函数声明
                ExtractFunctionDeclarations(line, variables);
            }

            return variables;
        }

        /// <summary>
        /// 提取声明
        /// </summary>
        private void ExtractDeclarations(string line, HashSet<string> variables)
        {
            var patterns = new[]
            {
                @"\bvar\s+([a-zA-Z_$][a-zA-Z0-9_$]*(?:\s*,\s*[a-zA-Z_$][a-zA-Z0-9_$]*)*)",
                @"\blet\s+([a-zA-Z_$][a-zA-Z0-9_$]*(?:\s*,\s*[a-zA-Z_$][a-zA-Z0-9_$]*)*)",
                @"\bconst\s+([a-zA-Z_$][a-zA-Z0-9_$]*(?:\s*,\s*[a-zA-Z_$][a-zA-Z0-9_$]*)*)"
            };

            foreach (var pattern in patterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(line, pattern);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var varList = match.Groups[1].Value;
                    var varNames = varList.Split(',').Select(v => v.Trim().Split('=')[0].Trim());
                    foreach (var varName in varNames.Where(n => !string.IsNullOrWhiteSpace(n)))
                    {
                        variables.Add(varName);
                    }
                }
            }
        }

        /// <summary>
        /// 提取函数声明
        /// </summary>
        private void ExtractFunctionDeclarations(string line, HashSet<string> variables)
        {
            // 函数声明
            const string funcPattern = @"\bfunction\s+([a-zA-Z_$][a-zA-Z0-9_$]*)\s*\(([^)]*)\)";
            var funcMatches = System.Text.RegularExpressions.Regex.Matches(line, funcPattern);

            foreach (System.Text.RegularExpressions.Match match in funcMatches)
            {
                // 函数名
                variables.Add(match.Groups[1].Value);

                // 函数参数
                var paramList = match.Groups[2].Value;
                if (!string.IsNullOrWhiteSpace(paramList))
                {
                    var paramNames = paramList.Split(',').Select(p => p.Trim().Split('=')[0].Trim());
                    foreach (var paramName in paramNames.Where(n => !string.IsNullOrWhiteSpace(n)))
                    {
                        variables.Add(paramName);
                    }
                }
            }
        }

        /// <summary>
        /// 提取赋值语句
        /// </summary>
        private void ExtractAssignments(string line, HashSet<string> variables)
        {

        }

        /// <summary>
        /// 处理文本输入中事件
        /// </summary>
        private void OnTextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length <= 0 || _completionWindow == null) return;
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
            {
                // 当输入非字母数字字符时，提交当前的提示
                _completionWindow.CompletionList.RequestInsertion(e);
            }
            else
            {
                // 如果是字母或数字，更新过滤
                _completionWindow.CompletionList.SelectItem(GetCurrentWord() + e.Text);
            }
        }

        /// <summary>
        /// 处理文本输入后事件
        /// </summary>
        private void OnTextEntered(object sender, TextCompositionEventArgs e)
        {
            // 如果窗口已经打开，需要刷新数据
            if (_completionWindow != null && char.IsLetterOrDigit(e.Text[0]))
            {
                // 关闭现有窗口并重新打开以刷新数据
                _completionWindow.Close();
                _completionWindow = null;
            }

            ShowCompletion(e.Text);
        }

        /// <summary>
        /// 处理键盘按下事件
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+Space 手动触发代码提示
            if (e.Key == Key.Space && e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                ShowCompletion(null);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 显示代码提示
        /// </summary>
        private void ShowCompletion(string triggerText)
        {
            // 如果已经有提示窗口，先关闭
            if (_completionWindow != null)
            {
                _completionWindow.Close();
                _completionWindow = null;
            }

            // 判断是否应该显示提示
            if (!ShouldShowCompletion(triggerText))
            {
                return;
            }

            // 获取当前光标位置的单词
            var currentWord = GetCurrentWord();

            // 获取提示数据
            var completionData = _completionProvider.GetCompletionData(currentWord, _textEditor.CaretOffset);

            if (completionData.Count == 0)
            {
                return;
            }

            // 创建主题化的提示窗口
            _completionWindow = new ThemedCompletionWindow(_textEditor.TextArea);

            // 添加提示数据
            foreach (var item in completionData)
            {
                _completionWindow.CompletionList.CompletionData.Add(item);
            }

            // 设置选择的起始位置
            if (!string.IsNullOrEmpty(currentWord))
            {
                _completionWindow.StartOffset = _textEditor.CaretOffset - currentWord.Length;
            }

            // 显示窗口
            _completionWindow.Show();
            _completionWindow.Closed += (sender, args) => _completionWindow = null;
        }

        /// <summary>
        /// 判断是否应该显示代码提示
        /// </summary>
        private bool ShouldShowCompletion(string triggerText)
        {
            // 手动触发（Ctrl+Space）
            if (triggerText == null)
            {
                return true;
            }

            // 输入字母或下划线时触发
            if (triggerText.Length == 1 && (char.IsLetter(triggerText[0]) || triggerText[0] == '_'))
            {
                return true;
            }

            // 输入点号时触发（用于对象属性）
            if (triggerText == ".")
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 获取当前光标位置的单词
        /// </summary>
        private string GetCurrentWord()
        {
            var document = _textEditor.Document;
            var offset = _textEditor.CaretOffset;

            if (offset == 0 || offset > document.TextLength)
            {
                return string.Empty;
            }

            // 查找单词的开始位置
            var startOffset = offset;
            while (startOffset > 0)
            {
                var ch = document.GetCharAt(startOffset - 1);
                if (!char.IsLetterOrDigit(ch) && ch != '_')
                {
                    break;
                }
                startOffset--;
            }

            // 查找单词的结束位置
            var endOffset = offset;
            while (endOffset < document.TextLength)
            {
                var ch = document.GetCharAt(endOffset);
                if (!char.IsLetterOrDigit(ch) && ch != '_')
                {
                    break;
                }
                endOffset++;
            }

            if (startOffset == endOffset)
            {
                return string.Empty;
            }

            return document.GetText(startOffset, endOffset - startOffset);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_textEditor != null)
            {
                _textEditor.TextArea.TextEntering -= OnTextEntering;
                _textEditor.TextArea.TextEntered -= OnTextEntered;
                _textEditor.TextArea.KeyDown -= OnKeyDown;
                _textEditor.TextChanged -= OnTextChanged;
            }

            if (_dynamicContext != null)
            {
                _dynamicContext.ContextChanged -= OnContextChanged;
            }

            _completionWindow?.Close();
        }
    }

    /// <summary>
    /// 实时JavaScript代码提示提供程序
    /// </summary>
    public class RealtimeJavaScriptCompletionProvider(JavaScriptDynamicContext dynamicContext)
        : JavaScriptCompletionDataProvider
    {
        private HashSet<string> _localIdentifiers = [];
        private HashSet<string> _declaredVariables = [];
        private List<DosageParameter> _parameters = [];

        public void UpdateLocalIdentifiers(HashSet<string> identifiers)
        {
            _localIdentifiers = new HashSet<string>(identifiers);
        }

        public void UpdateDeclaredVariables(HashSet<string> variables)
        {
            _declaredVariables = new HashSet<string>(variables);
        }


        public List<DosageParameter> GetParameters()
        {
            return _parameters;
        }

        public new void UpdateParameters(List<DosageParameter>? parameters)
        {
            _parameters = parameters ?? [];
        }

        public override IList<ICompletionData> GetCompletionData(string currentWord, int currentPosition)
        {
            var completionData = base.GetCompletionData(currentWord, currentPosition);

            // 添加动态上下文中的局部变量
            foreach (var identifier in _localIdentifiers.Where(identifier => completionData.All(cd => cd.Text != identifier)))
            {
                completionData.Add(new LocalVariableCompletionData(new LocalVariable
                {
                    Name = identifier,
                    Type = DetermineVariableType(identifier),
                    LineNumber = 0,
                    IsFunction = dynamicContext.GetAllLocalIdentifiers().Contains(identifier)
                }));
            }

            // 添加声明的变量（包括赋值语句）
            foreach (var variable in _declaredVariables.Where(variable => completionData.All(cd => cd.Text != variable)))
            {
                completionData.Add(new LocalVariableCompletionData(new LocalVariable
                {
                    Name = variable,
                    Type = "var",
                    LineNumber = 0,
                    IsFunction = false
                }));
            }

            // 过滤和排序
            if (!string.IsNullOrEmpty(currentWord))
            {
                completionData = completionData
                    .Where(cd => cd.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return completionData
                .OrderBy(GetCompletionPriority)
                .ThenBy(cd => cd.Text)
                .ToList();
        }

        private string DetermineVariableType(string identifier)
        {
            // 简单的类型推断
            if (identifier.StartsWith("is") || identifier.StartsWith("has"))
                return "boolean";
            if (identifier.EndsWith("Count") || identifier.EndsWith("Index"))
                return "number";
            if (identifier.EndsWith("Name") || identifier.EndsWith("Text"))
                return "string";
            return "var";
        }

        private double GetCompletionPriority(ICompletionData data)
        {
            return data switch
            {
                LocalVariableCompletionData => 0.5,
                ParameterCompletionData => 0.6,
                MethodCompletionData => 0.7,
                KeywordCompletionData => 0.8,
                _ => 1.0
            };
        }
    }
}