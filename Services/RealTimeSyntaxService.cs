using DrugSearcher.Configuration;
using DrugSearcher.Managers;
using DrugSearcher.Models;
using ICSharpCode.AvalonEdit;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace DrugSearcher.Services
{
    /// <summary>
    /// 实时语法服务 - 集成语法检测、错误标记和动态高亮
    /// </summary>
    public partial class RealTimeSyntaxService : IDisposable
    {
        private readonly TextEditor _textEditor;
        private readonly ILogger<RealTimeSyntaxService> _logger;
        private readonly JavaScriptDynamicContext _dynamicContext;
        private readonly DispatcherTimer _validationTimer;
        private readonly SyntaxErrorTextMarkerService _errorMarkerService; // 使用新的服务
        private readonly RealtimeSyntaxHighlighter _realtimeHighlighter;
        private readonly ErrorToolTipService _errorToolTipService;


        private List<DosageParameter> _parameters = [];
        private bool _isValidating;
        private int _headerLinesCount;

        public event EventHandler<string?>? StatusChanged;
        public event EventHandler<SyntaxValidationResult>? ValidationCompleted;

        public RealTimeSyntaxService(TextEditor textEditor, ILogger<RealTimeSyntaxService> logger)
        {
            _textEditor = textEditor;
            _logger = logger;

            // 初始化组件
            _dynamicContext = new JavaScriptDynamicContext();
            _errorMarkerService = new SyntaxErrorTextMarkerService(textEditor); // 使用新的服务
            _errorToolTipService = new ErrorToolTipService(textEditor);

            // 初始化实时高亮器
            var isDarkTheme = ContainerAccessor.Resolve<ThemeManager>().CurrentTheme.Mode == Enums.ThemeMode.Dark;
            _realtimeHighlighter = new RealtimeSyntaxHighlighter(_dynamicContext, isDarkTheme);
            _textEditor.TextArea.TextView.LineTransformers.Add(_realtimeHighlighter);

            // 设置验证计时器
            _validationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _validationTimer.Tick += OnValidationTimerTick;

            // 订阅事件
            _textEditor.TextChanged += OnTextChanged;
            _dynamicContext.ContextChanged += OnContextChanged;
        }

        /// <summary>
        /// 开始语法检测
        /// </summary>
        public void StartSyntaxChecking()
        {
            UpdateStatus("语法检测已启动");
            RequestValidation();
        }

        /// <summary>
        /// 更新参数
        /// </summary>
        public void UpdateParameters(List<DosageParameter>? parameters)
        {
            _parameters = parameters ?? [];
            RequestValidation();
        }

        /// <summary>
        /// 文本变化处理
        /// </summary>
        private void OnTextChanged(object? sender, EventArgs e)
        {
            // 更新动态上下文
            _dynamicContext.AnalyzeCode(_textEditor.Text, _textEditor.CaretOffset);

            // 请求验证
            RequestValidation();
        }

        /// <summary>
        /// 上下文变化处理
        /// </summary>
        private void OnContextChanged(object? sender, ContextChangedEventArgs e) =>
            // 刷新显示
            _textEditor.Dispatcher.InvokeAsync(() =>
            {
                _textEditor.TextArea.TextView.Redraw();
            });

        /// <summary>
        /// 请求验证
        /// </summary>
        private void RequestValidation()
        {
            _validationTimer.Stop();
            _validationTimer.Start();
            UpdateStatus("等待输入...");
        }

        /// <summary>
        /// 验证计时器触发
        /// </summary>
        private void OnValidationTimerTick(object? sender, EventArgs e)
        {
            _validationTimer.Stop();

            if (_isValidating) return;

            _isValidating = true;
            UpdateStatus("正在检测语法...");

            // 在UI线程中获取文本
            var textToValidate = _textEditor.Text;
            var parametersToValidate = new List<DosageParameter>(_parameters);

            // 异步执行验证
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var result = ValidateJavaScript(textToValidate, parametersToValidate);

                    _textEditor.Dispatcher.Invoke(() =>
                    {
                        // 更新错误标记
                        _errorMarkerService.UpdateErrorMarkers(result.Errors);

                        // 更新状态
                        UpdateValidationStatus(result);

                        // 触发完成事件
                        ValidationCompleted?.Invoke(this, result);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "语法验证失败");
                    _textEditor.Dispatcher.Invoke(() =>
                    {
                        UpdateStatus("验证失败");
                    });
                }
                finally
                {
                    _isValidating = false;
                }
            });
        }

        /// <summary>
        /// 验证JavaScript代码
        /// </summary>
        private SyntaxValidationResult ValidateJavaScript(string code, List<DosageParameter> parameters)
        {
            var result = new SyntaxValidationResult();

            if (string.IsNullOrWhiteSpace(code))
            {
                result.IsValid = true;
                return result;
            }

            try
            {
                // 1. 检查字符串字面量
                var stringErrors = CheckStringLiterals(code);
                result.Errors.AddRange(stringErrors);

                // 2. 预处理代码（移除注释和字符串）
                var processedCode = PreprocessCode(code);

                // 3. 检查括号匹配
                var bracketErrors = CheckBracketMatchingGlobal(processedCode);
                result.Errors.AddRange(bracketErrors);

                // 4. 检查函数调用参数
                var functionErrors = CheckFunctionCallParameters(code, parameters);
                result.Errors.AddRange(functionErrors);

                // 5. 检查常见语法错误
                var commonErrors = CheckCommonErrors(code);
                result.Errors.AddRange(commonErrors);

                // 6. 检查未定义的标识符
                var undefinedErrors = CheckUndefinedIdentifiers(code, parameters);
                result.Errors.AddRange(undefinedErrors);

                // 7. 使用V8引擎进行编译检查
                var v8Errors = CheckWithV8Engine(code, parameters);
                result.Errors.AddRange(v8Errors);

                // 过滤掉无效的错误（行号小于等于0的错误）
                result.Errors = [.. result.Errors.Where(e => e.Line > 0)];

                result.IsValid = result.Errors.Count == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "语法验证过程中发生错误");
                result.Errors.Add(new SyntaxError
                {
                    Message = $"验证器错误: {ex.Message}",
                    Line = 1,
                    Column = 1,
                    Severity = SyntaxErrorSeverity.Error
                });
                result.IsValid = false;
            }

            return result;
        }

        /// <summary>
        /// 检查字符串字面量
        /// </summary>
        private static List<SyntaxError> CheckStringLiterals(string code)
        {
            var errors = new List<SyntaxError>();
            var lines = code.Split('\n');

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var lineNumber = lineIndex + 1;

                // 检查双引号字符串
                errors.AddRange(CheckStringLiteralType(line, lineNumber, '"'));

                // 检查单引号字符串
                errors.AddRange(CheckStringLiteralType(line, lineNumber, '\''));

                // 检查模板字符串
                errors.AddRange(CheckStringLiteralType(line, lineNumber, '`'));
            }

            return errors;
        }

        /// <summary>
        /// 检查特定类型的字符串字面量
        /// </summary>
        private static List<SyntaxError> CheckStringLiteralType(string line, int lineNumber, char quoteChar)
        {
            var errors = new List<SyntaxError>();
            var inString = false;
            var escaped = false;
            var stringStart = -1;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (ch == quoteChar)
                    {
                        inString = false;
                        stringStart = -1;
                    }
                }
                else
                {
                    if (ch == quoteChar)
                    {
                        inString = true;
                        stringStart = i;
                    }
                }
            }

            // 检查未闭合的字符串
            if (inString && stringStart >= 0)
            {
                var quoteType = quoteChar switch
                {
                    '"' => "双引号",
                    '\'' => "单引号",
                    '`' => "模板字符串",
                    _ => "引号"
                };

                errors.Add(new SyntaxError
                {
                    Message = $"未闭合的{quoteType}字符串",
                    Line = lineNumber,
                    Column = stringStart + 1,
                    Severity = SyntaxErrorSeverity.Error
                });
            }

            return errors;
        }

        /// <summary>
        /// 预处理代码，移除注释和字符串内容
        /// </summary>
        private static string PreprocessCode(string code) => RemoveStringContents(code);

        /// <summary>
        /// 移除字符串内容
        /// </summary>
        private static string RemoveStringContents(string code)
        {
            var result = new System.Text.StringBuilder();
            var inDoubleQuote = false;
            var inSingleQuote = false;
            var inTemplateString = false;
            var inSingleLineComment = false;
            var inMultiLineComment = false;
            var escaped = false;

            for (var i = 0; i < code.Length; i++)
            {
                var ch = code[i];
                var nextCh = i + 1 < code.Length ? code[i + 1] : '\0';

                // 处理转义字符
                if (escaped)
                {
                    escaped = false;
                    if (inDoubleQuote || inSingleQuote || inTemplateString)
                    {
                        result.Append(' ');
                    }
                    else
                    {
                        result.Append(ch);
                    }
                    continue;
                }

                // 处理注释
                if (!inDoubleQuote && !inSingleQuote && !inTemplateString)
                {
                    switch (ch)
                    {
                        case '/' when nextCh == '/' && !inMultiLineComment:
                            inSingleLineComment = true;
                            result.Append("//");
                            i++;
                            continue;
                        case '/' when nextCh == '*' && !inSingleLineComment:
                            inMultiLineComment = true;
                            result.Append("/*");
                            i++;
                            continue;
                        case '*' when nextCh == '/' && inMultiLineComment:
                            inMultiLineComment = false;
                            result.Append("*/");
                            i++;
                            continue;
                    }
                }

                // 处理换行
                if (ch is '\n' or '\r')
                {
                    inSingleLineComment = false;
                    result.Append(ch);
                    continue;
                }

                // 在注释中
                if (inSingleLineComment || inMultiLineComment)
                {
                    result.Append(' ');
                    continue;
                }

                switch (ch)
                {
                    // 处理转义字符
                    case '\\':
                        {
                            escaped = true;
                            if (inDoubleQuote || inSingleQuote || inTemplateString)
                            {
                                result.Append(' ');
                            }
                            else
                            {
                                result.Append(ch);
                            }
                            continue;
                        }
                    // 处理字符串
                    case '"' when !inSingleQuote && !inTemplateString:
                        {
                            if (inDoubleQuote)
                            {
                                inDoubleQuote = false;
                                result.Append('"');
                            }
                            else
                            {
                                inDoubleQuote = true;
                                result.Append('"');
                            }
                            continue;
                        }
                    case '\'' when !inDoubleQuote && !inTemplateString:
                        {
                            if (inSingleQuote)
                            {
                                inSingleQuote = false;
                                result.Append('\'');
                            }
                            else
                            {
                                inSingleQuote = true;
                                result.Append('\'');
                            }
                            continue;
                        }
                    case '`' when !inDoubleQuote && !inSingleQuote:
                        {
                            if (inTemplateString)
                            {
                                inTemplateString = false;
                                result.Append('`');
                            }
                            else
                            {
                                inTemplateString = true;
                                result.Append('`');
                            }
                            continue;
                        }
                }

                // 在字符串内部，用空格替换内容
                if (inDoubleQuote || inSingleQuote || inTemplateString)
                {
                    result.Append(' ');
                }
                else
                {
                    result.Append(ch);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// 检查括号匹配
        /// </summary>
        private static List<SyntaxError> CheckBracketMatchingGlobal(string processedCode) => BracketMatchingService.CheckBracketMatching(processedCode);


        /// <summary>
        /// 检查函数调用参数（使用已有的 FindFunctionCalls 方法）
        /// </summary>
        private static List<SyntaxError> CheckFunctionCallParameters(string code, List<DosageParameter> parameters)
        {
            var errors = new List<SyntaxError>();
            var lines = code.Split('\n');

            // 构建已知标识符列表（包括参数）
            var knownIdentifiers = GetKnownIdentifiers(parameters);

            // 提取所有声明的变量
            var declaredVariables = ExtractProperlyDeclaredVariables(code);
            knownIdentifiers.UnionWith(declaredVariables);

            // 逐行累积作用域变量
            var scopeVariables = new HashSet<string?>(knownIdentifiers);

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var lineNumber = lineIndex + 1;

                // 跳过注释行
                if (line.Trim().StartsWith("//") || line.Trim().StartsWith("/*"))
                    continue;

                // 查找函数调用
                var functionCalls = FindFunctionCalls(line);

                foreach (var call in functionCalls)
                {
                    if (call.IsControlFlow)
                    {
                        // 处理控制流语句
                        var controlFlowErrors = ValidateControlFlowCondition(
                            call.FunctionName,
                            call.ArgumentsText,
                            scopeVariables,
                            lineNumber,
                            call.ArgumentsStartIndex + 1
                        );
                        errors.AddRange(controlFlowErrors);
                    }
                    else
                    {
                        // 处理函数调用
                        var funcErrors = ValidateFunctionCall(
                            call.FunctionName,
                            call.ArgumentsText,
                            scopeVariables,
                            lineNumber,
                            call.ArgumentsStartIndex + 1
                        );
                        errors.AddRange(funcErrors);
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// 查找函数调用
        /// </summary>
        private static List<FunctionCallInfo> FindFunctionCalls(string line)
        {
            var calls = new List<FunctionCallInfo>();
            var processedPositions = new HashSet<int>();

            // 首先处理控制流语句
            var controlFlowPattern = @"\b(if|switch|while|for|catch)\s*\(";
            var controlFlowMatches = Regex.Matches(line, controlFlowPattern);

            foreach (Match match in controlFlowMatches)
            {
                var startPos = match.Index + match.Length - 1; // 括号位置
                var argsInfo = ExtractArgumentsFromPosition(line, startPos);

                if (argsInfo != null)
                {
                    calls.Add(new FunctionCallInfo
                    {
                        FunctionName = match.Groups[1].Value,
                        ArgumentsText = argsInfo.ArgumentsText,
                        StartIndex = match.Index,
                        ArgumentsStartIndex = startPos,
                        IsControlFlow = true
                    });

                    // 标记这些位置已处理
                    for (var i = match.Index; i <= argsInfo.EndIndex; i++)
                    {
                        processedPositions.Add(i);
                    }
                }
            }

            // 处理普通函数调用
            var functionPattern = @"([a-zA-Z_$][a-zA-Z0-9_$]*)\s*\(";
            var functionMatches = Regex.Matches(line, functionPattern);

            foreach (Match match in functionMatches)
            {
                // 跳过已处理的位置
                if (processedPositions.Contains(match.Index))
                    continue;

                var functionName = match.Groups[1].Value;
                var parenPos = match.Index + match.Length - 1;

                // 检查是否是控制流关键字
                if (IsControlFlowKeyword(functionName))
                    continue;

                var argsInfo = ExtractArgumentsFromPosition(line, parenPos);

                if (argsInfo != null)
                {
                    calls.Add(new FunctionCallInfo
                    {
                        FunctionName = functionName,
                        ArgumentsText = argsInfo.ArgumentsText,
                        StartIndex = match.Index,
                        ArgumentsStartIndex = parenPos,
                        IsControlFlow = false
                    });
                }
            }

            return calls;
        }

        /// <summary>
        /// 从指定位置提取参数
        /// </summary>
        private static ArgumentsExtractionResult? ExtractArgumentsFromPosition(string text, int openParenIndex)
        {
            if (openParenIndex >= text.Length || text[openParenIndex] != '(')
                return null;

            var parenLevel = 0;
            var inString = false;
            var stringChar = '\0';
            var escaped = false;
            var startIndex = openParenIndex + 1;

            for (var i = openParenIndex; i < text.Length; i++)
            {
                var ch = text[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (!inString)
                {
                    switch (ch)
                    {
                        case '"' or '\'' or '`':
                            inString = true;
                            stringChar = ch;
                            continue;
                        case '(':
                            parenLevel++;
                            break;
                        case ')':
                            {
                                parenLevel--;
                                if (parenLevel == 0)
                                {
                                    // 找到匹配的闭括号
                                    return new ArgumentsExtractionResult
                                    {
                                        ArgumentsText = text[startIndex..i],
                                        EndIndex = i
                                    };
                                }

                                break;
                            }
                    }
                }
                else
                {
                    if (ch == stringChar)
                    {
                        inString = false;
                        stringChar = '\0';
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 检查是否是控制流关键字
        /// </summary>
        private static bool IsControlFlowKeyword(string? word) => word is "if" or "switch" or "while" or "for" or "catch" or "do";

        /// <summary>
        /// 函数调用信息
        /// </summary>
        private class FunctionCallInfo
        {
            public string? FunctionName { get; set; } = "";
            public string? ArgumentsText { get; set; } = "";
            public int StartIndex { get; set; }
            public int ArgumentsStartIndex { get; set; }
            public bool IsControlFlow { get; set; }
        }

        /// <summary>
        /// 参数提取结果
        /// </summary>
        private class ArgumentsExtractionResult
        {
            public string? ArgumentsText { get; init; } = "";
            public int EndIndex { get; init; }
        }
        /// <summary>
        /// 验证函数调用
        /// </summary>
        private static List<SyntaxError> ValidateFunctionCall(
            string? functionName,
            string? argumentsText,
            HashSet<string?> knownIdentifiers,
            int lineNumber,
            int columnOffset)
        {
            var errors = new List<SyntaxError>();

            // 解析参数
            var arguments = ParseFunctionArguments(argumentsText);

            // 检查已知函数的参数数量
            if (JavaScriptLanguageDefinition.IsCustomFunction(functionName))
            {
                var funcDef = JavaScriptLanguageDefinition.GetFunctionDefinition(functionName);
                if (funcDef != null)
                {
                    // 检查参数数量
                    var requiredParams = funcDef.Parameters.Count(p => !p.Optional);
                    var totalParams = funcDef.Parameters.Length;

                    if (arguments.Count < requiredParams)
                    {
                        errors.Add(new SyntaxError
                        {
                            Message = $"函数 {functionName} 需要至少 {requiredParams} 个参数，但只提供了 {arguments.Count} 个",
                            Line = lineNumber,
                            Column = columnOffset - 1,
                            Severity = SyntaxErrorSeverity.Error
                        });
                    }
                    else if (arguments.Count > totalParams)
                    {
                        errors.Add(new SyntaxError
                        {
                            Message = $"函数 {functionName} 最多接受 {totalParams} 个参数，但提供了 {arguments.Count} 个",
                            Line = lineNumber,
                            Column = columnOffset - 1,
                            Severity = SyntaxErrorSeverity.Warning
                        });
                    }
                }
            }

            // 验证每个参数
            foreach (var arg in arguments)
            {
                var argErrors = ValidateArgument(arg, knownIdentifiers, lineNumber, columnOffset);
                errors.AddRange(argErrors);
            }

            return errors;
        }

        /// <summary>
        /// 验证控制流条件
        /// </summary>
        private static List<SyntaxError> ValidateControlFlowCondition(
            string? keyword,
            string? condition,
            HashSet<string?> knownIdentifiers,
            int lineNumber,
            int columnOffset)
        {
            var errors = new List<SyntaxError>();
            condition = condition?.Trim();

            if (string.IsNullOrEmpty(condition))
            {
                errors.Add(new SyntaxError
                {
                    Message = $"{keyword} 语句缺少条件",
                    Line = lineNumber,
                    Column = columnOffset,
                    Severity = SyntaxErrorSeverity.Error
                });
                return errors;
            }

            // 特殊处理不同的控制流语句
            switch (keyword)
            {
                case "if":
                case "while":
                    // 这些语句接受任何可以转换为布尔值的表达式
                    errors.AddRange(ValidateConditionExpression(condition, knownIdentifiers, lineNumber, columnOffset));
                    break;

                case "switch":
                    // switch 接受任何表达式
                    errors.AddRange(ValidateSwitchExpression(condition, knownIdentifiers, lineNumber, columnOffset));
                    break;

                case "for":
                    // for 循环有特殊的三部分结构
                    errors.AddRange(ValidateForLoopCondition(condition, knownIdentifiers, lineNumber, columnOffset));
                    break;

                case "catch":
                    // catch 接受一个标识符作为异常参数
                    if (IsValidIdentifier(condition))
                    {
                        // catch 的参数是新的局部变量，不需要验证是否已定义
                    }
                    else
                    {
                        errors.Add(new SyntaxError
                        {
                            Message = "catch 语句需要有效的异常参数名",
                            Line = lineNumber,
                            Column = columnOffset,
                            Severity = SyntaxErrorSeverity.Error
                        });
                    }
                    break;
            }

            return errors;
        }

        /// <summary>
        /// 验证条件表达式（if/while）
        /// </summary>
        private static List<SyntaxError> ValidateConditionExpression(
            string? condition,
            HashSet<string?> knownIdentifiers,
            int lineNumber,
            int columnOffset)
        {
            var errors = new List<SyntaxError>();

            // 条件可以是：
            // 1. 单个标识符（会被转换为布尔值）
            // 2. 布尔字面量
            // 3. 比较表达式
            // 4. 逻辑表达式
            // 5. 函数调用
            // 6. 任何其他表达式

            // 如果是单个标识符
            if (IsValidIdentifier(condition))
            {
                if (!knownIdentifiers.Contains(condition))
                {
                    errors.Add(new SyntaxError
                    {
                        Message = $"未定义的变量: {condition}",
                        Line = lineNumber,
                        Column = columnOffset,
                        Severity = SyntaxErrorSeverity.Warning
                    });
                }
                // 即使是单个标识符也是有效的条件
                return errors;
            }

            // 如果是表达式，验证其中的标识符
            if (condition == null) return errors;
            var matches = IdentifierPatternRegex().Matches(condition);

            foreach (Match match in matches)
            {
                var identifier = match.Groups[1].Value;

                // 跳过关键字和已知标识符
                if (JavaScriptLanguageDefinition.IsKeyword(identifier) ||
                    knownIdentifiers.Contains(identifier) ||
                    JavaScriptLanguageDefinition.IsBuiltInFunction(identifier))
                    continue;

                // 检查是否在属性访问中
                if (IsInPropertyAccess(condition, match.Index))
                    continue;

                errors.Add(new SyntaxError
                {
                    Message = $"未定义的变量: {identifier}",
                    Line = lineNumber,
                    Column = columnOffset + match.Index,
                    Severity = SyntaxErrorSeverity.Warning
                });
            }

            return errors;
        }
        /// <summary>
        /// 验证 switch 表达式
        /// </summary>
        private static List<SyntaxError> ValidateSwitchExpression(
            string? expression,
            HashSet<string?> knownIdentifiers,
            int lineNumber,
            int columnOffset) =>
            // switch 表达式的验证逻辑与条件表达式类似
            ValidateConditionExpression(expression, knownIdentifiers, lineNumber, columnOffset);

        /// <summary>
        /// 验证 for 循环条件
        /// </summary>
        private static List<SyntaxError> ValidateForLoopCondition(
            string? condition,
            HashSet<string?> knownIdentifiers,
            int lineNumber,
            int columnOffset)
        {
            var errors = new List<SyntaxError>();

            // for 循环可能有三个部分，用分号分隔
            var parts = condition?.Split(';');

            if (parts == null) return errors;
            switch (parts.Length)
            {
                case 3:
                    {
                        // 传统 for 循环: for (init; condition; update)
                        var currentOffset = columnOffset;

                        for (var i = 0; i < parts.Length; i++)
                        {
                            var part = parts[i].Trim();

                            if (i == 0 && part.StartsWith("var ") || part.StartsWith("let ") ||
                                part.StartsWith("const "))
                            {
                                // 初始化部分可能声明新变量
                                var varPattern = @"(?:var|let|const)\s+([a-zA-Z_$][a-zA-Z0-9_$]*)";
                                var varMatch = Regex.Match(part, varPattern);
                                if (varMatch.Success)
                                {
                                    knownIdentifiers.Add(varMatch.Groups[1].Value);
                                }
                            }
                            else if (!string.IsNullOrEmpty(part))
                            {
                                // 验证其他部分的表达式
                                errors.AddRange(ValidateConditionExpression(part, knownIdentifiers, lineNumber,
                                    currentOffset));
                            }

                            currentOffset += part.Length + (i < parts.Length - 1 ? 1 : 0); // +1 for semicolon
                        }

                        break;
                    }
                case 1:
                    {
                        // 可能是 for...in 或 for...of 循环
                        if (condition != null && (condition.Contains(" in ") || condition.Contains(" of ")))
                        {
                            var match = ForInOfPatternRegex().Match(condition);
                            if (match.Success)
                            {
                                var iterableExpr = match.Groups[2].Value;

                                // 迭代变量是新声明的，不需要检查
                                // 但需要验证被迭代的表达式
                                errors.AddRange(ValidateConditionExpression(iterableExpr, knownIdentifiers, lineNumber,
                                    columnOffset + match.Groups[2].Index));
                            }
                        }
                        else
                        {
                            // 其他情况，作为普通表达式验证
                            errors.AddRange(ValidateConditionExpression(condition, knownIdentifiers, lineNumber,
                                columnOffset));
                        }

                        break;
                    }
            }

            return errors;
        }

        /// <summary>
        /// 解析函数参数（增强版，支持嵌套函数调用）
        /// </summary>
        private static List<FunctionArgument> ParseFunctionArguments(string? argumentsText)
        {
            var arguments = new List<FunctionArgument>();
            var currentArg = new System.Text.StringBuilder();
            var parenLevel = 0;
            var bracketLevel = 0;
            var braceLevel = 0;
            var inString = false;
            var stringChar = '\0';
            var escaped = false;
            var argStartIndex = 0;

            if (argumentsText != null)
                for (var i = 0; i < argumentsText.Length; i++)
                {
                    var ch = argumentsText[i];

                    if (escaped)
                    {
                        escaped = false;
                        currentArg.Append(ch);
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        currentArg.Append(ch);
                        continue;
                    }

                    if (!inString)
                    {
                        if (ch is '"' or '\'' or '`')
                        {
                            inString = true;
                            stringChar = ch;
                            currentArg.Append(ch);
                            continue;
                        }

                        switch (ch)
                        {
                            case '(':
                                parenLevel++;
                                currentArg.Append(ch);
                                break;
                            case ')':
                                parenLevel--;
                                currentArg.Append(ch);
                                break;
                            case '[':
                                bracketLevel++;
                                currentArg.Append(ch);
                                break;
                            case ']':
                                bracketLevel--;
                                currentArg.Append(ch);
                                break;
                            case '{':
                                braceLevel++;
                                currentArg.Append(ch);
                                break;
                            case '}':
                                braceLevel--;
                                currentArg.Append(ch);
                                break;
                            case ',':
                                // 只有在所有括号都闭合的情况下，逗号才是参数分隔符
                                if (parenLevel == 0 && bracketLevel == 0 && braceLevel == 0)
                                {
                                    var argText = currentArg.ToString();
                                    arguments.Add(new FunctionArgument
                                    {
                                        Text = argText,
                                        StartIndex = argStartIndex
                                    });

                                    currentArg.Clear();
                                    argStartIndex = i + 1;
                                }
                                else
                                {
                                    // 否则逗号是参数内容的一部分
                                    currentArg.Append(ch);
                                }

                                break;
                            default:
                                currentArg.Append(ch);
                                break;
                        }
                    }
                    else
                    {
                        if (ch == stringChar)
                        {
                            inString = false;
                            stringChar = '\0';
                        }

                        currentArg.Append(ch);
                    }
                }

            // 添加最后一个参数
            var lastArgText = currentArg.ToString();
            arguments.Add(new FunctionArgument
            {
                Text = lastArgText,
                StartIndex = argStartIndex
            });

            // 特殊处理：如果最后一个字符是逗号，说明有一个空参数
            if (argumentsText != null && !argumentsText.TrimEnd().EndsWith(','))
            {
                return arguments;
            }

            if (argumentsText != null)
                arguments.Add(new FunctionArgument
                {
                    Text = "",
                    StartIndex = argumentsText.Length
                });

            return arguments;
        }


        /// <summary>
        /// 验证参数（增强版）
        /// </summary>
        private static List<SyntaxError> ValidateArgument(FunctionArgument argument, HashSet<string?> knownIdentifiers, int lineNumber, int columnOffset)
        {
            var errors = new List<SyntaxError>();
            var argText = argument.Text?.Trim();

            // 检查空参数（连续的逗号）
            if (string.IsNullOrEmpty(argText))
            {
                errors.Add(new SyntaxError
                {
                    Message = "缺少参数",
                    Line = lineNumber,
                    Column = columnOffset + argument.StartIndex + 1,
                    Severity = SyntaxErrorSeverity.Error
                });
                return errors;
            }

            // 获取参数类型
            var argType = GetArgumentType(argText, knownIdentifiers);

            switch (argType)
            {
                case ArgumentType.Empty:
                    errors.Add(new SyntaxError
                    {
                        Message = "参数不能为空",
                        Line = lineNumber,
                        Column = columnOffset + argument.StartIndex + 1,
                        Severity = SyntaxErrorSeverity.Error
                    });
                    break;

                case ArgumentType.Invalid:
                    errors.Add(new SyntaxError
                    {
                        Message = $"无效的参数: {argText}",
                        Line = lineNumber,
                        Column = columnOffset + argument.StartIndex + 1,
                        Severity = SyntaxErrorSeverity.Error
                    });
                    break;

                case ArgumentType.UnknownIdentifier:
                    // 提取实际的标识符位置
                    var identifierMatch = IdentifierPatternRegex().Match(argText);
                    if (identifierMatch.Success)
                    {
                        errors.Add(new SyntaxError
                        {
                            Message = $"未定义的标识符: {identifierMatch.Groups[1].Value}",
                            Line = lineNumber,
                            Column = columnOffset + argument.StartIndex + identifierMatch.Index + 1,
                            Severity = SyntaxErrorSeverity.Warning
                        });
                    }
                    else
                    {
                        errors.Add(new SyntaxError
                        {
                            Message = $"未定义的标识符: {argText}",
                            Line = lineNumber,
                            Column = columnOffset + argument.StartIndex + 1,
                            Severity = SyntaxErrorSeverity.Warning
                        });
                    }
                    break;

                case ArgumentType.InvalidCharacters:
                    errors.Add(new SyntaxError
                    {
                        Message = $"参数包含无效字符: {argText}",
                        Line = lineNumber,
                        Column = columnOffset + argument.StartIndex + 1,
                        Severity = SyntaxErrorSeverity.Error
                    });
                    break;
            }

            return errors;
        }

        /// <summary>
        /// 获取参数类型（更新版）
        /// </summary>
        private static ArgumentType GetArgumentType(string? argText, HashSet<string?> knownIdentifiers)
        {
            // 空参数
            if (string.IsNullOrWhiteSpace(argText))
                return ArgumentType.Empty;

            // 字符串字面量
            if (IsStringLiteral(argText))
                return ArgumentType.StringLiteral;

            // 数字
            if (IsNumber(argText))
                return ArgumentType.Number;

            switch (argText)
            {
                // 布尔值
                case "true" or "false":
                    return ArgumentType.Boolean;
                // null 或 undefined
                case "null" or "undefined":
                    return ArgumentType.Null;
            }

            // 数组字面量
            if (IsArrayLiteral(argText))
                return ArgumentType.ArrayLiteral;

            // 对象字面量
            if (IsObjectLiteral(argText))
                return ArgumentType.ObjectLiteral;

            // 函数调用
            if (IsFunctionCall(argText))
                return ArgumentType.FunctionCall;

            // 表达式（包括逻辑非）
            if (IsExpression(argText))
                return ArgumentType.Expression;

            // 有效的标识符
            if (IsValidIdentifier(argText))
            {
                if (knownIdentifiers.Contains(argText))
                    return ArgumentType.KnownIdentifier;
                else
                    return ArgumentType.UnknownIdentifier;
            }

            // 包含中文或其他无效字符
            if (ContainsInvalidCharacters(argText))
                return ArgumentType.InvalidCharacters;

            return ArgumentType.Invalid;
        }
        // Updated code to fix CA1866 diagnostic issues by replacing string.StartsWith(string) and string.EndsWith(string) with their char overloads where applicable.

        private static bool IsStringLiteral(string? text)
        {
            if (text is { Length: < 2 }) return false;

            if (text == null) return false;
            var firstChar = text[0];
            var lastChar = text[^1]; // Using ^1 for the last character

            return (firstChar == '"' && lastChar == '"') ||
                   (firstChar == '\'' && lastChar == '\'') ||
                   (firstChar == '`' && lastChar == '`');
        }

        /// <summary>
        /// 检查是否为数字
        /// </summary>
        private static bool IsNumber(string? text) => text != null && (IsNumber10Regex().IsMatch(text) ||
                                                                       IsNumber16Regex().IsMatch(text));

        /// <summary>
        /// 检查是否为数组字面量
        /// </summary>
        private static bool IsArrayLiteral(string? text)
        {
            var trimmedText = text?.Trim();
            return trimmedText != null && trimmedText.StartsWith('[') && trimmedText.EndsWith(']');
        }

        /// <summary>
        /// 检查是否为对象字面量
        /// </summary>
        private static bool IsObjectLiteral(string? text)
        {
            var trimmedText = text?.Trim();
            return trimmedText != null && trimmedText.StartsWith('{') && trimmedText.EndsWith('}');
        }

        /// <summary>
        /// 检查是否为函数调用
        /// </summary>
        private static bool IsFunctionCall(string? text) => text != null && IsFunctionCallRegex().IsMatch(text);

        /// <summary>
        /// 检查是否为表达式（增强版）
        /// </summary>
        private static bool IsExpression(string? text)
        {
            while (true)
            {
                text = text?.Trim();

                if (text != null && text.StartsWith('!'))
                {
                    var afterNot = text[1..].Trim();
                    if (afterNot.StartsWith('[') && afterNot.EndsWith(']')) return true;
                }

                if (text != null && text.StartsWith("!!"))
                {
                    var afterDoubleNot = text[2..].Trim();
                    text = "!" + afterDoubleNot;
                    continue;
                }

                if (text != null && !text.Contains('.')) return false;
                var parts = text?.Split(['.'], 2);
                if (parts is { Length: <= 0 }) return false;
                var firstPart = parts?[0].Trim();
                return firstPart != null && firstPart.StartsWith('[') && firstPart.EndsWith(']');
            }
        }

        /*
                /// <summary>
                /// 检查是否为数组方法调用
                /// </summary>
                private static bool IsArrayMethodCall(string? text)
                {
                    // 常见的数组方法
                    var arrayMethods = new[] {
                        "includes", "indexOf", "push", "pop", "shift", "unshift",
                        "slice", "splice", "filter", "map", "reduce", "forEach",
                        "find", "findIndex", "some", "every", "sort", "reverse",
                        "join", "concat", "flat", "flatMap"
                    };

                    foreach (var method in arrayMethods)
                    {
                        if (text.Contains($".{method}("))
                            return true;
                    }

                    return false;
                }
        */


        /// <summary>
        /// 检查是否为有效标识符
        /// </summary>
        private static bool IsValidIdentifier(string? text) => text != null && IsValidIdentifierRegex().IsMatch(text);

        /// <summary>
        /// 检查是否包含无效字符
        /// </summary>
        private static bool ContainsInvalidCharacters(string? text) =>
            // 检查是否包含中文或其他非ASCII字符
            text != null && text.Any(c => c > 127 && !char.IsLetterOrDigit(c));

        /// <summary>
        /// 检查常见错误
        /// </summary>
        private static List<SyntaxError> CheckCommonErrors(string code)
        {
            var errors = new List<SyntaxError>();
            var lines = code.Split('\n');

            // 预处理代码
            var processedCode = RemoveStringContents(code);
            var processedLines = processedCode.Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var originalLine = lines[i];
                var processedLine = i < processedLines.Length ? processedLines[i] : "";
                var lineNumber = i + 1;

                // 跳过空行和注释行
                if (string.IsNullOrWhiteSpace(processedLine.Trim()) ||
                    processedLine.Trim().StartsWith("//") ||
                    processedLine.Trim().StartsWith("/*"))
                    continue;

                // 检查分号缺失
                var missingSemicolonPattern = @"^\s*(return\s+[^;]+|break|continue|throw\s+[^;]+)(?!\s*;)\s*$";
                if (Regex.IsMatch(processedLine.Trim(), missingSemicolonPattern))
                {
                    errors.Add(new SyntaxError
                    {
                        Message = "可能缺少分号",
                        Line = lineNumber,
                        Column = originalLine.Length,
                        Severity = SyntaxErrorSeverity.Warning
                    });
                }

                // 检查常见的类型错误
                var typeErrorPattern = @"==\s*null|!=\s*null|==\s*undefined|!=\s*undefined";
                var typeErrorMatch = Regex.Match(processedLine, typeErrorPattern);
                if (typeErrorMatch.Success)
                {
                    errors.Add(new SyntaxError
                    {
                        Message = "建议使用 === 或 !== 进行严格比较",
                        Line = lineNumber,
                        Column = typeErrorMatch.Index + 1,
                        Severity = SyntaxErrorSeverity.Warning
                    });
                }

                // 检查可能的无限循环
                if (DieRingRegex().IsMatch(processedLine))
                {
                    var match = DieRingRegex().Match(processedLine);
                    errors.Add(new SyntaxError
                    {
                        Message = "检测到可能的无限循环",
                        Line = lineNumber,
                        Column = match.Index + 1,
                        Severity = SyntaxErrorSeverity.Warning
                    });
                }

                // 检查引号风格一致性
                CheckQuoteConsistency(originalLine, lineNumber, errors);
            }

            return errors;
        }

        /// <summary>
        /// 检查引号风格一致性
        /// </summary>
        private static void CheckQuoteConsistency(string line, int lineNumber, List<SyntaxError> errors)
        {
            var hasDoubleQuote = false;
            var hasSingleQuote = false;
            var inString = false;
            var stringChar = '\0';
            var escaped = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (!inString)
                {
                    switch (ch)
                    {
                        case '"':
                            hasDoubleQuote = true;
                            inString = true;
                            stringChar = '"';
                            break;
                        case '\'':
                            hasSingleQuote = true;
                            inString = true;
                            stringChar = '\'';
                            break;
                    }
                }
                else
                {
                    if (ch == stringChar)
                    {
                        inString = false;
                        stringChar = '\0';
                    }
                }
            }

            if (hasDoubleQuote && hasSingleQuote)
            {
                errors.Add(new SyntaxError
                {
                    Message = "建议在同一行中使用统一的引号风格",
                    Line = lineNumber,
                    Column = 1,
                    Severity = SyntaxErrorSeverity.Info
                });
            }
        }
        /// <summary>
        /// 检查未定义的标识符（完整修复版）
        /// </summary>
        private static List<SyntaxError> CheckUndefinedIdentifiers(string code, List<DosageParameter> parameters)
        {
            var errors = new List<SyntaxError>();
            _ = code.Split('\n');

            // 预处理代码
            var processedCode = RemoveStringContents(code);
            string?[] processedLines = processedCode.Split('\n');

            // 获取已知标识符（包括参数定义的变量）
            var knownIdentifiers = GetKnownIdentifiers(parameters);

            // 从处理后的代码中提取正确声明的变量
            var properlyDeclaredVariables = ExtractProperlyDeclaredVariables(processedCode);
            knownIdentifiers.UnionWith(properlyDeclaredVariables);

            // 逐行累积作用域中的变量
            var scopeVariables = new HashSet<string?>(knownIdentifiers);
            var implicitlyDeclaredVariables = new HashSet<string?>();

            // 处理每一行
            for (var i = 0; i < processedLines.Length; i++)
            {
                var processedLine = processedLines[i];
                var lineNumber = i + 1;

                // 跳过注释行
                if (processedLine != null && (processedLine.Trim().StartsWith("//") || processedLine.Trim().StartsWith("/*")))
                    continue;

                // 检查直接赋值语句（没有var/let/const）
                if (processedLine != null)
                {
                    var assignmentMatch = MyRegex().Match(processedLine);

                    if (assignmentMatch.Success)
                    {
                        var varName = assignmentMatch.Groups[1].Value;

                        // 检查是否是 var/let/const 声明
                        var declarationPattern = @"^\s*(var|let|const)\s+" + Regex.Escape(varName);
                        if (!Regex.IsMatch(processedLine, declarationPattern))
                        {
                            // 这是一个没有声明的赋值
                            if (!scopeVariables.Contains(varName))
                            {
                                // 变量未声明就使用
                                errors.Add(new SyntaxError
                                {
                                    Message = $"变量 '{varName}' 未声明就赋值（应使用 var、let 或 const 声明）",
                                    Line = lineNumber,
                                    Column = assignmentMatch.Groups[1].Index + 1,
                                    Severity = SyntaxErrorSeverity.Error
                                });

                                // 记录为隐式声明的变量（用于后续检查）
                                implicitlyDeclaredVariables.Add(varName);
                            }
                        }
                    }
                }

                // 查找函数调用（排除控制流语句）
                if (processedLine != null)
                {
                    var matches = MyRegex1().Matches(processedLine);

                    foreach (Match match in matches)
                    {
                        var functionName = match.Groups[1].Value;

                        // 跳过控制流关键字
                        if (IsControlFlowKeyword(functionName))
                            continue;

                        // 检查是否是已知的函数
                        if (!scopeVariables.Contains(functionName) &&
                            !JavaScriptLanguageDefinition.IsBuiltInFunction(functionName) &&
                            !JavaScriptLanguageDefinition.IsCustomFunction(functionName))
                        {
                            errors.Add(new SyntaxError
                            {
                                Message = $"未定义的函数: {functionName}",
                                Line = lineNumber,
                                Column = match.Index + 1,
                                Severity = SyntaxErrorSeverity.Warning
                            });
                        }
                    }
                }

                // 查找变量使用（排除赋值左侧、函数调用、控制流语句）
                if (processedLine == null) continue;
                {
                    var variableMatches = MyRegex2().Matches(processedLine);

                    foreach (Match match in variableMatches)
                    {
                        var variableName = match.Groups[1].Value;

                        // 跳过关键字、已知标识符
                        if (JavaScriptLanguageDefinition.IsKeyword(variableName) ||
                            IsControlFlowKeyword(variableName) ||
                            scopeVariables.Contains(variableName) ||
                            char.IsDigit(variableName[0]))
                            continue;

                        // 检查是否在赋值语句的左侧
                        if (IsInAssignmentLeft(processedLine, match.Index))
                            continue;

                        // 检查是否在属性访问中
                        if (IsInPropertyAccess(processedLine, match.Index))
                            continue;

                        // 检查是否是函数调用
                        if (IsFollowedByParenthesis(processedLine, match.Index + match.Length))
                            continue;

                        // 如果变量在隐式声明列表中，说明它是通过不当方式"声明"的
                        if (implicitlyDeclaredVariables.Contains(variableName))
                            continue;

                        // 如果不在任何已知上下文中，报告未定义
                        errors.Add(new SyntaxError
                        {
                            Message = $"未定义的变量: {variableName}",
                            Line = lineNumber,
                            Column = match.Index + 1,
                            Severity = SyntaxErrorSeverity.Warning
                        });
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// 检查是否紧跟着括号（用于识别函数调用）
        /// </summary>
        private static bool IsFollowedByParenthesis(string? line, int position)
        {
            // 跳过空白字符
            while (line != null && position < line.Length && char.IsWhiteSpace(line[position]))
            {
                position++;
            }

            return line != null && position < line.Length && line[position] == '(';
        }

        /// <summary>
        /// 提取正确声明的变量（使用var/let/const）
        /// </summary>
        private static HashSet<string> ExtractProperlyDeclaredVariables(string code)
        {
            var variables = new HashSet<string>();

            // 提取 var 声明
            var varPattern = @"\bvar\s+([a-zA-Z_$][a-zA-Z0-9_$]*(?:\s*,\s*[a-zA-Z_$][a-zA-Z0-9_$]*)*)";
            var varMatches = Regex.Matches(code, varPattern);
            foreach (Match match in varMatches)
            {
                var varList = match.Groups[1].Value;
                var varNames = varList.Split(',').Select(v => v.Trim().Split('=')[0].Trim());
                foreach (var varName in varNames)
                {
                    if (!string.IsNullOrWhiteSpace(varName))
                    {
                        variables.Add(varName);
                    }
                }
            }

            // 提取 let 声明
            var letPattern = @"\blet\s+([a-zA-Z_$][a-zA-Z0-9_$]*(?:\s*,\s*[a-zA-Z_$][a-zA-Z0-9_$]*)*)";
            var letMatches = Regex.Matches(code, letPattern);
            foreach (Match match in letMatches)
            {
                var letList = match.Groups[1].Value;
                var letNames = letList.Split(',').Select(v => v.Trim().Split('=')[0].Trim());
                foreach (var letName in letNames)
                {
                    if (!string.IsNullOrWhiteSpace(letName))
                    {
                        variables.Add(letName);
                    }
                }
            }

            // 提取 const 声明
            var constPattern = @"\bconst\s+([a-zA-Z_$][a-zA-Z0-9_$]*(?:\s*,\s*[a-zA-Z_$][a-zA-Z0-9_$]*)*)";
            var constMatches = Regex.Matches(code, constPattern);
            foreach (Match match in constMatches)
            {
                var constList = match.Groups[1].Value;
                var constNames = constList.Split(',').Select(v => v.Trim().Split('=')[0].Trim());
                foreach (var constName in constNames)
                {
                    if (!string.IsNullOrWhiteSpace(constName))
                    {
                        variables.Add(constName);
                    }
                }
            }

            // 提取函数声明和参数
            var funcDeclPattern = @"function\s+([a-zA-Z_$][a-zA-Z0-9_$]*)\s*\(([^)]*)\)";
            var funcDeclMatches = Regex.Matches(code, funcDeclPattern);
            foreach (Match match in funcDeclMatches)
            {
                // 函数名
                variables.Add(match.Groups[1].Value);

                // 函数参数
                var paramList = match.Groups[2].Value;
                ExtractFunctionParameters(paramList, variables);
            }

            // 注意：不再将简单的赋值语句视为变量声明

            return variables;
        }

        /// <summary>
        /// 获取已知标识符（包括参数定义的变量）
        /// </summary>
        private static HashSet<string?> GetKnownIdentifiers(List<DosageParameter> parameters)
        {
            var identifiers = new HashSet<string?>();

            // 添加参数名称 - 这些是在计算器中可用的全局变量
            foreach (var param in parameters)
            {
                if (!string.IsNullOrWhiteSpace(param.Name))
                {
                    identifiers.Add(param.Name);
                }
            }

            // 添加内置函数
            identifiers.UnionWith(JavaScriptLanguageDefinition.CustomFunctions.Names);

            return identifiers;
        }

        /// <summary>
        /// 提取函数参数
        /// </summary>
        private static void ExtractFunctionParameters(string paramList, HashSet<string> variables)
        {
            if (string.IsNullOrWhiteSpace(paramList))
                return;

            // 处理默认参数和解构参数
            var paramPattern = @"([a-zA-Z_$][a-zA-Z0-9_$]*)(?:\s*=\s*[^,)]+)?";
            var paramMatches = Regex.Matches(paramList, paramPattern);

            foreach (Match match in paramMatches)
            {
                var paramName = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(paramName))
                {
                    variables.Add(paramName);
                }
            }
        }
        /// <summary>
        /// 检查变量是否在赋值语句的左侧
        /// </summary>
        private static bool IsInAssignmentLeft(string? line, int position)
        {
            // 检查是否在声明语句中
            var beforePosition = line?[..position]?.Trim();
            if (beforePosition != null && (beforePosition.EndsWith("var ") || beforePosition.EndsWith("let ") || beforePosition.EndsWith("const ")))
            {
                return true;
            }

            // 检查是否在赋值表达式的左侧
            var afterVariable = line?[position..];

            // 查找第一个非字母数字字符
            var endOfVariable = 0;
            while (afterVariable != null &&
                   endOfVariable < afterVariable.Length &&
                   (char.IsLetterOrDigit(afterVariable[endOfVariable]) || afterVariable[endOfVariable] == '_'))
            {
                endOfVariable++;
            }

            // 检查变量后面是否紧跟着等号
            var afterVarName = afterVariable?[endOfVariable..]?.Trim();
            return afterVarName != null && afterVarName.StartsWith('=') && !afterVarName.StartsWith("==") && !afterVarName.StartsWith("===");
        }

        /// <summary>
        /// 检查是否在属性访问中
        /// </summary>
        private static bool IsInPropertyAccess(string? line, int position)
        {
            // 检查前面是否有点号
            if (position > 0 && line != null && line[position - 1] == '.')
                return true;

            // 检查后面是否有点号
            var afterVariable = line?[position..];
            var endOfVariable = 0;
            while (afterVariable != null && endOfVariable < afterVariable.Length &&
                   (char.IsLetterOrDigit(afterVariable[endOfVariable]) || afterVariable[endOfVariable] == '_'))
            {
                endOfVariable++;
            }

            return afterVariable != null && endOfVariable < afterVariable.Length && afterVariable[endOfVariable] == '.';
        }

        /// <summary>
        /// 使用V8引擎检查
        /// </summary>
        private List<SyntaxError> CheckWithV8Engine(string code, List<DosageParameter> parameters)
        {
            var errors = new List<SyntaxError>();

            try
            {
                using var engine = new V8ScriptEngine();
                engine.DefaultAccess = Microsoft.ClearScript.ScriptAccess.ReadOnly;

                // 构建完整的脚本进行检查
                var fullScript = BuildValidationScript(code, parameters);

                // 尝试编译
                engine.Compile(fullScript);
            }
            catch (Microsoft.ClearScript.ScriptEngineException ex)
            {
                var error = ParseV8Error(ex.Message);
                if (error is { Line: > 0 })
                {
                    errors.Add(error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "V8引擎检查时发生错误");
            }

            return errors;
        }

        /// <summary>
        /// 构建验证脚本
        /// </summary>
        private string? BuildValidationScript(string userCode, List<DosageParameter> parameters)
        {
            var scriptBuilder = new System.Text.StringBuilder();

            // 添加固定的全局变量和函数
            scriptBuilder.AppendLine("// 固定的全局变量和函数");
            scriptBuilder.AppendLine("var results = [];");
            scriptBuilder.AppendLine();

            // 动态添加参数声明
            scriptBuilder.AppendLine("// 动态参数声明");
            foreach (var parameter in parameters)
            {
                if (!string.IsNullOrWhiteSpace(parameter.Name))
                {
                    var defaultValue = GetParameterDefaultValue(parameter);
                    scriptBuilder.AppendLine($"var {parameter.Name} = {defaultValue};");
                }
            }
            scriptBuilder.AppendLine();

            // 添加辅助函数
            scriptBuilder.AppendLine("// 辅助函数");
            var helperFunctionCode = @"
function addResult(description, dose, unit, frequency, duration, notes, isWarning, warningMessage) {
    // 验证用函数
}

function addWarning(description, dose, unit, frequency, warningMessage) {
    // 验证用函数
}

function addNormalResult(description, dose, unit, frequency, duration, notes) {
    // 验证用函数
}

function round(value, decimals) {
    return Math.round(value * Math.pow(10, decimals)) / Math.pow(10, decimals);
}

function clamp(value, min, max) {
    return Math.min(Math.max(value, min), max);
}

function isValidNumber(value) {
    return !isNaN(value) && isFinite(value);
}

function safeParseFloat(value, defaultValue) {
    var parsed = parseFloat(value);
    return isNaN(parsed) ? defaultValue : parsed;
}

function safeParseInt(value, defaultValue) {
    var parsed = parseInt(value);
    return isNaN(parsed) ? defaultValue : parsed;
}
";
            scriptBuilder.AppendLine(helperFunctionCode);

            // 计算前置行数
            var headerLines = scriptBuilder.ToString().Split('\n').Length;
            _headerLinesCount = headerLines;

            // 添加用户代码
            scriptBuilder.AppendLine("// 用户代码");
            scriptBuilder.AppendLine(userCode);

            return scriptBuilder.ToString();
        }

        /// <summary>
        /// 获取参数的默认值
        /// </summary>
        private static string? GetParameterDefaultValue(DosageParameter parameter) => parameter.DataType switch
        {
            ParameterTypes.Number => parameter.DefaultValue?.ToString() ?? "0",
            ParameterTypes.Boolean => parameter.DefaultValue?.ToString()?.ToLower() ?? "false",
            ParameterTypes.Text => $"'{parameter.DefaultValue?.ToString() ?? ""}'",
            ParameterTypes.Select => $"'{parameter.DefaultValue?.ToString() ?? ""}'",
            _ => $"'{parameter.DefaultValue?.ToString() ?? ""}'"
        };

        /// <summary>
        /// 解析V8错误
        /// </summary>
        private SyntaxError? ParseV8Error(string errorMessage)
        {
            // 尝试解析行号和列号
            var lineMatch = V8ErrorLinePlaceRegex().Match(errorMessage);
            var columnMatch = V8ErrorColumnPlaceRegex().Match(errorMessage);

            if (!lineMatch.Success)
            {
                lineMatch = V8ErrorLineIndexRegex().Match(errorMessage);
            }

            if (!columnMatch.Success)
            {
                columnMatch = V8ErrorColumnIndexRegex().Match(errorMessage);
            }

            var line = lineMatch.Success ? int.Parse(lineMatch.Groups[1].Value) : 1;
            var column = columnMatch.Success ? int.Parse(columnMatch.Groups[1].Value) : 1;

            // 调整行号，减去前置代码行数
            var adjustedLine = line - _headerLinesCount;

            // 确保行号至少为1
            if (adjustedLine >= 1)
                return new SyntaxError
                {
                    Message = CleanErrorMessage(errorMessage),
                    Line = adjustedLine,
                    Column = column,
                    Severity = SyntaxErrorSeverity.Error
                };
            _logger.LogDebug($"V8错误行号调整：原始行号 {line}，前置行数 {_headerLinesCount}，调整后行号 {adjustedLine}，将被过滤");
            return null;

        }

        /// <summary>
        /// 清理错误消息
        /// </summary>
        private static string? CleanErrorMessage(string errorMessage)
        {
            // 移除V8引擎特定的信息
            var cleanMessage = errorMessage;
            cleanMessage = V8ErrorLinePlaceRegex().Replace(cleanMessage, "");
            cleanMessage = V8ErrorColumnPlaceRegex().Replace(cleanMessage, "");
            cleanMessage = cleanMessage.Replace("Script compilation failed.", "");
            cleanMessage = cleanMessage.Trim();

            return string.IsNullOrEmpty(cleanMessage) ? "语法错误" : cleanMessage;
        }

        /// <summary>
        /// 更新状态
        /// </summary>
        private void UpdateStatus(string status) => StatusChanged?.Invoke(this, status);

        /// <summary>
        /// 更新验证状态
        /// </summary>
        private void UpdateValidationStatus(SyntaxValidationResult result)
        {
            // 更新错误标记
            _errorMarkerService.UpdateErrorMarkers(result.Errors);

            // 更新错误工具提示
            _errorToolTipService.UpdateErrors(result.Errors);

            var errorCount = result.Errors.Count(e => e.Severity == SyntaxErrorSeverity.Error);
            var warningCount = result.Errors.Count(e => e.Severity == SyntaxErrorSeverity.Warning);
            var infoCount = result.Errors.Count(e => e.Severity == SyntaxErrorSeverity.Info);

            if (errorCount > 0)
            {
                UpdateStatus($"✗ {errorCount} 错误, {warningCount} 警告, {infoCount} 提示");
            }
            else if (warningCount > 0)
            {
                UpdateStatus($"⚠ {warningCount} 警告, {infoCount} 提示");
            }
            else if (infoCount > 0)
            {
                UpdateStatus($"ℹ {infoCount} 提示");
            }
            else
            {
                UpdateStatus("✓ 语法检测通过");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _validationTimer?.Stop();
            if (_validationTimer != null) _validationTimer.Tick -= OnValidationTimerTick;

            _textEditor.TextChanged -= OnTextChanged;
            _dynamicContext.ContextChanged -= OnContextChanged;

            if (_realtimeHighlighter != null)
            {
                _textEditor.TextArea.TextView.LineTransformers.Remove(_realtimeHighlighter);
            }

            _errorMarkerService?.Dispose();
            _errorToolTipService?.Dispose();
            GC.SuppressFinalize(this);
        }

        [GeneratedRegex(@"^\d+(\.\d+)?([eE][+-]?\d+)?$")]
        private static partial Regex IsNumber10Regex();
        [GeneratedRegex(@"[a-zA-Z_$][a-zA-Z0-9_$]*\s*\(.*\)")]
        private static partial Regex IsFunctionCallRegex();
        [GeneratedRegex(@"\b([a-zA-Z_$][a-zA-Z0-9_$]*)\b")]
        private static partial Regex IdentifierPatternRegex();
        [GeneratedRegex(@"(?:var|let|const)?\s*([a-zA-Z_$][a-zA-Z0-9_$]*)\s+(?:in|of)\s+(.+)")]
        private static partial Regex ForInOfPatternRegex();
        [GeneratedRegex(@"^0[xX][0-9a-fA-F]+$")]
        private static partial Regex IsNumber16Regex();
        [GeneratedRegex(@"^[a-zA-Z_$][a-zA-Z0-9_$]*$")]
        private static partial Regex IsValidIdentifierRegex();
        [GeneratedRegex(@"while\s*\(\s*true\s*\)")]
        private static partial Regex DieRingRegex();
        [GeneratedRegex(@"at line (\d+)")]
        private static partial Regex V8ErrorLinePlaceRegex();
        [GeneratedRegex(@"at column (\d+)")]
        private static partial Regex V8ErrorColumnPlaceRegex();
        [GeneratedRegex(@"line (\d+)")]
        private static partial Regex V8ErrorLineIndexRegex();
        [GeneratedRegex(@"column (\d+)")]
        private static partial Regex V8ErrorColumnIndexRegex();
        [GeneratedRegex(@"^\s*([a-zA-Z_$][a-zA-Z0-9_$]*)\s*=(?!=)")]
        private static partial Regex MyRegex();
        [GeneratedRegex(@"\b([a-zA-Z_$][a-zA-Z0-9_$]*)\s*\(")]
        private static partial Regex MyRegex1();
        [GeneratedRegex(@"\b([a-zA-Z_$][a-zA-Z0-9_$]*)\b")]
        private static partial Regex MyRegex2();
    }
    /// <summary>
    /// 括号匹配服务
    /// </summary>
    public static class BracketMatchingService
    {
        public static List<SyntaxError> CheckBracketMatching(string processedCode)
        {
            var errors = new List<SyntaxError>();
            var bracketStack = new Stack<BracketInfo>();
            var bracketPairs = new Dictionary<char, char>
            {
                { '(', ')' },
                { '[', ']' },
                { '{', '}' }
            };

            var lines = processedCode.Split('\n');

            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var lineNumber = lineIndex + 1;

                for (var columnIndex = 0; columnIndex < line.Length; columnIndex++)
                {
                    var ch = line[columnIndex];
                    var column = columnIndex + 1;

                    // 开括号
                    if (bracketPairs.TryGetValue(ch, out var value))
                    {
                        bracketStack.Push(new BracketInfo
                        {
                            Character = ch,
                            Line = lineNumber,
                            Column = column,
                            ExpectedClosing = value
                        });
                    }
                    // 闭括号
                    else if (bracketPairs.ContainsValue(ch))
                    {
                        if (bracketStack.Count == 0)
                        {
                            errors.Add(new SyntaxError
                            {
                                Message = $"意外的闭合括号 '{ch}'",
                                Line = lineNumber,
                                Column = column,
                                Severity = SyntaxErrorSeverity.Error
                            });
                        }
                        else
                        {
                            var lastOpen = bracketStack.Peek();

                            // 检查是否匹配
                            if (lastOpen.ExpectedClosing == ch)
                            {
                                bracketStack.Pop();
                            }
                            else
                            {
                                // 查找栈中是否有匹配的开括号
                                var tempStack = new Stack<BracketInfo>();
                                var foundMatch = false;

                                while (bracketStack.Count > 0)
                                {
                                    var bracket = bracketStack.Pop();
                                    if (bracket.ExpectedClosing == ch)
                                    {
                                        foundMatch = true;

                                        // 报告跳过的未闭合括号
                                        while (tempStack.Count > 0)
                                        {
                                            var skipped = tempStack.Pop();
                                            errors.Add(new SyntaxError
                                            {
                                                Message = $"未闭合的括号 '{skipped.Character}'，期望 '{skipped.ExpectedClosing}'",
                                                Line = skipped.Line,
                                                Column = skipped.Column,
                                                Severity = SyntaxErrorSeverity.Error
                                            });
                                        }
                                        break;
                                    }
                                    tempStack.Push(bracket);
                                }

                                if (!foundMatch)
                                {
                                    // 恢复栈
                                    while (tempStack.Count > 0)
                                    {
                                        bracketStack.Push(tempStack.Pop());
                                    }

                                    errors.Add(new SyntaxError
                                    {
                                        Message = $"括号不匹配，期望 '{lastOpen.ExpectedClosing}' 但找到 '{ch}'",
                                        Line = lineNumber,
                                        Column = column,
                                        Severity = SyntaxErrorSeverity.Error
                                    });
                                }
                            }
                        }
                    }
                }
            }

            // 检查剩余的未闭合括号
            while (bracketStack.Count > 0)
            {
                var unclosed = bracketStack.Pop();
                errors.Add(new SyntaxError
                {
                    Message = $"未闭合的括号 '{unclosed.Character}'，期望 '{unclosed.ExpectedClosing}'",
                    Line = unclosed.Line,
                    Column = unclosed.Column,
                    Severity = SyntaxErrorSeverity.Error
                });
            }

            return errors;
        }
    }

    /// <summary>
    /// 括号信息
    /// </summary>
    public class BracketInfo
    {
        public char Character { get; set; }
        public char ExpectedClosing { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }

    /// <summary>
    /// 函数参数信息
    /// </summary>
    internal class FunctionArgument
    {
        public string? Text { get; set; }
        public int StartIndex { get; set; }
    }

    /// <summary>
    /// 参数类型
    /// </summary>
    internal enum ArgumentType
    {
        StringLiteral,
        Number,
        Boolean,
        Null,
        ArrayLiteral,
        ObjectLiteral,
        FunctionCall,
        Expression,
        KnownIdentifier,
        UnknownIdentifier,
        Invalid,
        InvalidCharacters,
        Empty
    }
}