using DrugSearcher.Models;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace DrugSearcher.Services;

/// <summary>
/// JavaScript代码提示数据提供程序
/// </summary>
public partial class JavaScriptCompletionDataProvider
{
    private readonly List<ICompletionData> _staticCompletionData = CreateStaticCompletionData();
    private List<DosageParameter> _parameters = [];
    private string _currentCode = string.Empty;

    /// <summary>
    /// 更新参数列表
    /// </summary>
    public void UpdateParameters(List<DosageParameter> parameters) => _parameters = parameters ?? [];

    /// <summary>
    /// 更新当前代码
    /// </summary>
    public void UpdateCurrentCode(string? code) => _currentCode = code ?? string.Empty;

    /// <summary>
    /// 获取代码提示数据
    /// </summary>
    public virtual IList<ICompletionData> GetCompletionData(string currentWord, int currentPosition)
    {
        var completionData = new List<ICompletionData>();

        // 检测是否在成员访问上下文中（点号后，包括已经输入了部分成员名称的情况）
        var memberAccessInfo = GetMemberAccessInfo(currentPosition);

        if (memberAccessInfo.IsMemberAccess)
        {
            // 根据对象类型添加相应的成员
            if (IsArrayExpression(memberAccessInfo.ObjectExpression))
            {
                AddArrayMethods(completionData);
            }
            else if (IsStringExpression(memberAccessInfo.ObjectExpression))
            {
                AddStringMethods(completionData);
            }
            else if (IsMathObject(memberAccessInfo.ObjectExpression))
            {
                AddMathMethods(completionData);
            }
            else if (IsConsoleObject(memberAccessInfo.ObjectExpression))
            {
                AddConsoleMethods(completionData);
            }
            else if (IsArrayParameter(memberAccessInfo.ObjectExpression))
            {
                AddArrayMethods(completionData);
            }
            else
            {
                // 默认添加通用对象方法
                AddObjectMethods(completionData);
            }

            // 使用成员名称进行过滤
            if (!string.IsNullOrEmpty(memberAccessInfo.MemberPrefix))
            {
                return
                [
                    .. completionData
                        .Where(cd => cd.Text != null && cd.Text.StartsWith(memberAccessInfo.MemberPrefix,
                            StringComparison.OrdinalIgnoreCase))
                        .OrderBy(cd => cd.Priority)
                        .ThenBy(cd => cd.Text)
                ];
            }
        }
        else
        {
            // 不在成员访问上下文中，添加所有可用的补全
            completionData.AddRange(_staticCompletionData);

            // 添加参数
            completionData.AddRange(_parameters.Select(parameter => new ParameterCompletionData(parameter)));

            // 添加内置对象
            AddBuiltInObjects(completionData);

            // 如果有当前输入的词，进行过滤
            if (!string.IsNullOrEmpty(currentWord))
            {
                return
                [
                    .. completionData
                        .Where(cd =>
                            cd.Text != null && cd.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(cd => cd.Priority)
                        .ThenBy(cd => cd.Text)
                ];
            }
        }

        return
        [
            .. completionData
                .OrderBy(cd => cd.Priority)
                .ThenBy(cd => cd.Text)
        ];
    }

    /// <summary>
    /// 获取成员访问信息
    /// </summary>
    private MemberAccessInfo GetMemberAccessInfo(int position)
    {
        var info = new MemberAccessInfo();

        if (string.IsNullOrEmpty(_currentCode) || position <= 0)
            return info;

        // 向前查找点号
        var searchPos = position - 1;
        var memberPrefix = "";

        // 首先收集当前已输入的成员名称部分
        while (searchPos >= 0 && (char.IsLetterOrDigit(_currentCode[searchPos]) || _currentCode[searchPos] == '_' ||
                                  _currentCode[searchPos] == '$'))
        {
            memberPrefix = _currentCode[searchPos] + memberPrefix;
            searchPos--;
        }

        // 跳过空白
        while (searchPos >= 0 && char.IsWhiteSpace(_currentCode[searchPos]))
            searchPos--;

        // 检查是否有点号
        if (searchPos >= 0 && _currentCode[searchPos] == '.')
        {
            info.IsMemberAccess = true;
            info.MemberPrefix = memberPrefix;

            // 获取点号前的表达式
            info.ObjectExpression = ExtractExpressionBeforeDot(searchPos);
        }

        return info;
    }

    /// <summary>
    /// 提取点号前的表达式（改进版）
    /// </summary>
    private string ExtractExpressionBeforeDot(int dotPosition)
    {
        if (string.IsNullOrEmpty(_currentCode) || dotPosition <= 0)
            return "";

        var endIndex = dotPosition - 1;
        while (endIndex >= 0 && char.IsWhiteSpace(_currentCode[endIndex]))
            endIndex--;

        if (endIndex < 0) return "";

        switch (_currentCode[endIndex])
        {
            // 如果是闭合的方括号，找到匹配的开始方括号（数组字面量）
            case ']':
                {
                    var bracketLevel = 1;
                    var startIndex = endIndex - 1;
                    var inString = false;
                    var stringChar = '\0';
                    var escaped = false;

                    while (startIndex >= 0 && bracketLevel > 0)
                    {
                        var ch = _currentCode[startIndex];

                        if (escaped)
                        {
                            escaped = false;
                            startIndex--;
                            continue;
                        }

                        if (ch == '\\')
                        {
                            escaped = true;
                            startIndex--;
                            continue;
                        }

                        if (!inString)
                        {
                            switch (ch)
                            {
                                case '"' or '\'' or '`':
                                    inString = true;
                                    stringChar = ch;
                                    break;
                                case ']':
                                    bracketLevel++;
                                    break;
                                case '[':
                                    bracketLevel--;
                                    break;
                            }
                        }
                        else if (ch == stringChar)
                        {
                            inString = false;
                        }

                        startIndex--;
                    }

                    if (bracketLevel == 0 && startIndex >= -1)
                    {
                        return _currentCode.Substring(startIndex + 1, endIndex - startIndex).Trim();
                    }

                    break;
                }
            // 如果是闭合的圆括号，找到匹配的开始括号（函数调用）
            case ')':
                {
                    var parenLevel = 1;
                    var startIndex = endIndex - 1;
                    var inString = false;
                    var stringChar = '\0';
                    var escaped = false;

                    while (startIndex >= 0 && parenLevel > 0)
                    {
                        var ch = _currentCode[startIndex];

                        if (escaped)
                        {
                            escaped = false;
                            startIndex--;
                            continue;
                        }

                        if (ch == '\\')
                        {
                            escaped = true;
                            startIndex--;
                            continue;
                        }

                        if (!inString)
                        {
                            switch (ch)
                            {
                                case '"' or '\'' or '`':
                                    inString = true;
                                    stringChar = ch;
                                    break;
                                case ')':
                                    parenLevel++;
                                    break;
                                case '(':
                                    parenLevel--;
                                    break;
                            }
                        }
                        else if (ch == stringChar)
                        {
                            inString = false;
                        }

                        startIndex--;
                    }

                    // 继续向前查找函数名
                    if (parenLevel == 0 && startIndex >= 0)
                    {
                        return _currentCode[..(endIndex + 1)].Trim();
                    }

                    break;
                }
            // 如果是标识符
            default:
                {
                    if (char.IsLetterOrDigit(_currentCode[endIndex]) || _currentCode[endIndex] == '_' ||
                        _currentCode[endIndex] == '$')
                    {
                        var startIndex = endIndex;
                        while (startIndex > 0 && (char.IsLetterOrDigit(_currentCode[startIndex - 1]) ||
                                                  _currentCode[startIndex - 1] == '_' || _currentCode[startIndex - 1] == '$'))
                            startIndex--;

                        return _currentCode.Substring(startIndex, endIndex - startIndex + 1);
                    }
                    // 如果是字符串字面量
                    else if (_currentCode[endIndex] == '"' || _currentCode[endIndex] == '\'' || _currentCode[endIndex] == '`')
                    {
                        var quote = _currentCode[endIndex];
                        var startIndex = endIndex - 1;
                        var escaped = false;

                        while (startIndex >= 0)
                        {
                            if (escaped)
                            {
                                escaped = false;
                            }
                            else if (_currentCode[startIndex] == '\\')
                            {
                                escaped = true;
                            }
                            else if (_currentCode[startIndex] == quote)
                            {
                                return _currentCode.Substring(startIndex, endIndex - startIndex + 1);
                            }

                            startIndex--;
                        }
                    }

                    break;
                }
        }

        return "";
    }

    /// <summary>
    /// 检查是否为数组表达式
    /// </summary>
    private bool IsArrayExpression(string expression)
    {
        if (string.IsNullOrEmpty(expression)) return false;

        expression = expression.Trim();

        // 检查是否为数组字面量
        if (IsArrayLiteral(expression))
            return true;

        // 检查是否为已知的数组参数
        if (IsArrayParameter(expression))
            return true;

        // 检查是否为返回数组的方法调用
        if (expression.EndsWith(')'))
        {
            var arrayReturningMethods = new[]
            {
                ".split(", ".match(", ".filter(", ".map(", ".slice(",
                ".concat(", ".flat(", ".flatMap(", ".sort(", ".reverse("
            };
            return arrayReturningMethods.Any(m => expression.Contains(m));
        }

        return false;
    }

    /// <summary>
    /// 检查是否为字符串表达式
    /// </summary>
    private static bool IsStringExpression(string expression)
    {
        if (string.IsNullOrEmpty(expression)) return false;

        expression = expression.Trim();

        // 检查是否为字符串字面量
        return (expression.StartsWith('"') && expression.EndsWith('"')) ||
               (expression.StartsWith('\'') && expression.EndsWith('\'')) ||
               (expression.StartsWith('`') && expression.EndsWith('`'));
    }

    /// <summary>
    /// 检查是否为Math对象
    /// </summary>
    private static bool IsMathObject(string expression) =>
        expression.Trim() == "Math";

    /// <summary>
    /// 检查是否为console对象
    /// </summary>
    private static bool IsConsoleObject(string expression) =>
        expression.Trim() == "console";

    /// <summary>
    /// 检查是否为数组参数
    /// </summary>
    private bool IsArrayParameter(string varName)
    {
        varName = varName.Trim();
        var param = _parameters.FirstOrDefault(p => p.Name == varName);
        return param is { DataType: ParameterTypes.ARRAY };
    }

    /// <summary>
    /// 检查是否为数组字面量（更严格的验证）
    /// </summary>
    private static bool IsArrayLiteral(string expression)
    {
        expression = expression.Trim();
        if (!expression.StartsWith('[') || !expression.EndsWith(']'))
            return false;

        // 验证括号匹配
        var bracketCount = 0;
        var inString = false;
        var stringChar = '\0';
        var escaped = false;

        foreach (var ch in expression)
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

            if (!inString)
            {
                switch (ch)
                {
                    case '"' or '\'' or '`':
                        inString = true;
                        stringChar = ch;
                        break;
                    case '[':
                        bracketCount++;
                        break;
                    case ']':
                        bracketCount--;
                        break;
                }
            }
            else if (ch == stringChar)
            {
                inString = false;
            }
        }

        return bracketCount == 0 && !inString;
    }

    /// <summary>
    /// 添加数组方法
    /// </summary>
    private static void AddArrayMethods(List<ICompletionData> completionData)
    {
        foreach (var method in JavaScriptLanguageDefinition.BuiltIns.ArrayPrototypeMethods)
        {
            completionData.Add(new MethodCompletionData(
                method.Key,
                $"{method.Key}()",
                $"(数组方法) {method.Value}",
                GenerateArrayMethodInsertionText(method.Key)
            ));
        }

        // 添加数组属性
        completionData.Add(new PropertyCompletionData("length", "数组长度"));
    }

    /// <summary>
    /// 添加字符串方法
    /// </summary>
    private static void AddStringMethods(List<ICompletionData> completionData)
    {
        var stringMethods = new Dictionary<string, string>
        {
            { "charAt", "返回指定位置的字符" },
            { "charCodeAt", "返回指定位置字符的 Unicode 编码" },
            { "concat", "连接两个或多个字符串" },
            { "includes", "判断字符串是否包含指定的子字符串" },
            { "indexOf", "返回子字符串首次出现的位置" },
            { "lastIndexOf", "返回子字符串最后出现的位置" },
            { "match", "使用正则表达式匹配字符串" },
            { "padEnd", "在字符串末尾填充指定字符" },
            { "padStart", "在字符串开头填充指定字符" },
            { "repeat", "重复字符串指定次数" },
            { "replace", "替换字符串中的内容" },
            { "replaceAll", "替换字符串中的所有匹配内容" },
            { "search", "搜索指定值的位置" },
            { "slice", "提取字符串的一部分" },
            { "split", "将字符串分割成数组" },
            { "startsWith", "判断字符串是否以指定字符串开头" },
            { "endsWith", "判断字符串是否以指定字符串结尾" },
            { "substring", "提取字符串的子串" },
            { "toLowerCase", "转换为小写" },
            { "toUpperCase", "转换为大写" },
            { "trim", "去除两端空白字符" },
            { "trimStart", "去除开头空白字符" },
            { "trimEnd", "去除末尾空白字符" }
        };

        completionData.AddRange(stringMethods.Select(method => new MethodCompletionData(method.Key, $"{method.Key}()", $"(字符串方法) {method.Value}", GenerateStringMethodInsertionText(method.Key))));

        // 添加字符串属性
        completionData.Add(new PropertyCompletionData("length", "字符串长度"));
    }

    /// <summary>
    /// 添加Math方法
    /// </summary>
    private static void AddMathMethods(List<ICompletionData> completionData)
    {
        if (JavaScriptLanguageDefinition.BuiltIns.ObjectMethods.TryGetValue("Math", out var mathMethods))
        {
            completionData.AddRange(mathMethods.Select(method => new MethodCompletionData(method, $"{method}()", $"(数学函数) {method}", $"{method}(${{1}})")));
        }

        // 添加Math常量
        var mathConstants = new Dictionary<string, string>
        {
            { "PI", "圆周率 π (约 3.14159)" },
            { "E", "自然对数的底数 e (约 2.718)" },
            { "LN2", "2 的自然对数" },
            { "LN10", "10 的自然对数" },
            { "LOG2E", "以 2 为底 e 的对数" },
            { "LOG10E", "以 10 为底 e 的对数" },
            { "SQRT1_2", "1/2 的平方根" },
            { "SQRT2", "2 的平方根" }
        };

        foreach (var constant in mathConstants)
        {
            completionData.Add(new PropertyCompletionData(constant.Key, constant.Value));
        }
    }

    /// <summary>
    /// 添加console方法
    /// </summary>
    private static void AddConsoleMethods(List<ICompletionData> completionData)
    {
        var consoleMethods = new Dictionary<string, string>
        {
            { "log", "输出日志信息" },
            { "error", "输出错误信息" },
            { "warn", "输出警告信息" },
            { "info", "输出提示信息" },
            { "debug", "输出调试信息" },
            { "clear", "清空控制台" },
            { "table", "以表格形式显示数据" },
            { "time", "开始计时" },
            { "timeEnd", "结束计时" }
        };

        foreach (var method in consoleMethods)
        {
            completionData.Add(new MethodCompletionData(
                method.Key,
                $"{method.Key}()",
                $"(控制台方法) {method.Value}",
                $"{method.Key}(${{1}})"
            ));
        }
    }

    /// <summary>
    /// 添加通用对象方法
    /// </summary>
    private static void AddObjectMethods(List<ICompletionData> completionData)
    {
        var objectMethods = new Dictionary<string, string>
        {
            { "toString", "返回对象的字符串表示" },
            { "valueOf", "返回对象的原始值" },
            { "hasOwnProperty", "检查对象是否具有指定的属性" },
            { "isPrototypeOf", "检查对象是否在另一个对象的原型链中" },
            { "propertyIsEnumerable", "检查指定属性是否可枚举" }
        };

        completionData.AddRange(objectMethods.Select(method => new MethodCompletionData(method.Key, $"{method.Key}()", $"(对象方法) {method.Value}", method.Key == "hasOwnProperty" ? $"{method.Key}(${{1:'propertyName'}})" : $"{method.Key}()")));
    }

    /// <summary>
    /// 添加内置对象
    /// </summary>
    private static void AddBuiltInObjects(List<ICompletionData> completionData)
    {
        var builtInObjects = new Dictionary<string, string>
        {
            { "Math", "数学函数和常量" },
            { "console", "控制台对象" },
            { "JSON", "JSON 解析和序列化" },
            { "Array", "数组构造函数" },
            { "Object", "对象构造函数" },
            { "String", "字符串构造函数" },
            { "Number", "数字构造函数" },
            { "Boolean", "布尔值构造函数" },
            { "Date", "日期构造函数" },
            { "RegExp", "正则表达式构造函数" }
        };

        completionData.AddRange(builtInObjects.Select(obj => new PropertyCompletionData(obj.Key, $"(内置对象) {obj.Value}")));
    }

    /// <summary>
    /// 生成字符串方法插入文本
    /// </summary>
    private static string GenerateStringMethodInsertionText(string methodName) => methodName switch
    {
        "charAt" or "charCodeAt" => $"{methodName}(${{1:index}})",
        "concat" => $"{methodName}(${{1:string}})",
        "includes" or "startsWith" or "endsWith" => $"{methodName}(${{1:searchString}})",
        "indexOf" or "lastIndexOf" => $"{methodName}(${{1:searchValue}})",
        "match" or "search" => $"{methodName}(${{1:regexp}})",
        "padEnd" or "padStart" => $"{methodName}(${{1:targetLength}}, ${{2:padString}})",
        "repeat" => $"{methodName}(${{1:count}})",
        "replace" => $"{methodName}(${{1:searchValue}}, ${{2:replaceValue}})",
        "replaceAll" => $"{methodName}(${{1:searchValue}}, ${{2:replaceValue}})",
        "slice" or "substring" => $"{methodName}(${{1:start}}, ${{2:end}})",
        "split" => $"{methodName}(${{1:separator}})",
        _ => $"{methodName}()"
    };

    /// <summary>
    /// 生成数组方法插入文本
    /// </summary>
    private static string GenerateArrayMethodInsertionText(string methodName) => methodName switch
    {
        "push" or "unshift" => $"{methodName}(${{1:element}})",
        "slice" => $"{methodName}(${{1:start}}, ${{2:end}})",
        "splice" => $"{methodName}(${{1:start}}, ${{2:deleteCount}}, ${{3:item}})",
        "indexOf" or "lastIndexOf" or "includes" => $"{methodName}(${{1:searchElement}})",
        "join" => $"{methodName}(${{1:separator}})",
        "filter" or "map" or "forEach" => $"{methodName}((${{1:element}}) => ${{2:}})",
        "reduce" or "reduceRight" => $"{methodName}((${{1:acc}}, ${{2:element}}) => ${{3:}}, ${{4:initialValue}})",
        "find" or "findIndex" => $"{methodName}((${{1:element}}) => ${{2:condition}})",
        "some" or "every" => $"{methodName}((${{1:element}}) => ${{2:condition}})",
        "sort" => $"{methodName}((${{1:a}}, ${{2:b}}) => ${{3:a - b}})",
        _ => $"{methodName}()"
    };

    /// <summary>
    /// 创建静态代码提示数据
    /// </summary>
    private static List<ICompletionData> CreateStaticCompletionData()
    {
        var completionData = JavaScriptLanguageDefinition.CustomFunctions.Functions.Values
            .Select(func =>
                new MethodCompletionData(func.Name, func.Signature, func.Description, GenerateInsertionText(func)))
            .Cast<ICompletionData>().ToList();
        completionData.AddRange(JavaScriptLanguageDefinition.BuiltIns.GlobalFunctions.Select(func =>
            new MethodCompletionData(func.Key, func.Key + "()", func.Value, func.Key + "(${1})")));

        // 添加关键字
        completionData.AddRange(JavaScriptLanguageDefinition.Keywords.All.OfType<string>().Select(keyword =>
            new KeywordCompletionData(keyword, $"JavaScript关键字: {keyword}", GetKeywordTemplate(keyword))));

        return completionData;
    }

    /// <summary>
    /// 生成插入文本
    /// </summary>
    private static string GenerateInsertionText(FunctionDefinition func)
    {
        var parameters = func.Parameters.Select((p, i) => $"${{${i + 1}:{p.Name}}}");
        return $"{func.Name}({string.Join(", ", parameters)});";
    }

    /// <summary>
    /// 获取关键字模板
    /// </summary>
    private static string? GetKeywordTemplate(string? keyword) => keyword switch
    {
        "if" => "if (${1:condition}) {\n    ${2:// 代码}\n}",
        "else" => "else {\n    ${1:// 代码}\n}",
        "for" => "for (let ${1:i} = 0; ${1:i} < ${2:length}; ${1:i}++) {\n    ${3:// 代码}\n}",
        "while" => "while (${1:condition}) {\n    ${2:// 代码}\n}",
        "function" => "function ${1:name}(${2:params}) {\n    ${3:// 代码}\n    return ${4:result};\n}",
        "var" => "var ${1:variable} = ${2:value};",
        "let" => "let ${1:variable} = ${2:value};",
        "const" => "const ${1:variable} = ${2:value};",
        "return" => "return ${1:value};",
        "try" => "try {\n    ${1:// 代码}\n} catch (${2:error}) {\n    ${3:// 错误处理}\n}",
        _ => keyword
    };

    [GeneratedRegex(@"\$\{\d+:([^}]+)\}")]
    internal static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"\$\{\d+\}")]
    internal static partial Regex SimplePlaceholderRegex();

    /// <summary>
    /// 成员访问信息
    /// </summary>
    private class MemberAccessInfo
    {
        public bool IsMemberAccess { get; set; }

        public string ObjectExpression { get; set; } = "";

        public string MemberPrefix { get; set; } = "";
    }
}

/// <summary>
/// 局部变量信息
/// </summary>
public class LocalVariable
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public int LineNumber { get; set; }

    public bool IsFunction { get; set; }
}

/// <summary>
/// 局部变量代码提示数据
/// </summary>
public class LocalVariableCompletionData(LocalVariable variable) : ICompletionData
{
    public string? Text { get; } = variable.Name;

    public object Content { get; } = $"{variable.Name} ({variable.Type})";

    public object Description { get; } = $"局部{GetTypeDescription(variable.Type)}: {variable.Name}" +
                                         (variable.LineNumber > 0 ? $"\n声明位置: 第{variable.LineNumber}行" : "");

    public ImageSource? Image => null;
    public double Priority => 0.6;

    private static string GetTypeDescription(string? type) => type switch
    {
        "var" => "变量",
        "let" => "变量",
        "const" => "常量",
        "function" => "函数",
        "parameter" => "参数",
        _ => "变量"
    };

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
        textArea.Document.Replace(completionSegment, Text);
}

/// <summary>
/// 方法代码提示数据
/// </summary>
public class MethodCompletionData(string? text, string? signature, string? description, string insertionText)
    : ICompletionData
{
    public string? Text { get; } = text;

    public object? Content { get; } = signature;

    public object? Description { get; } = description;

    public ImageSource? Image => null;
    public double Priority => 1.0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var insertText = insertionText;

        // 简单的模板变量替换
        insertText = JavaScriptCompletionDataProvider.PlaceholderRegex().Replace(insertText, "$1");
        insertText = JavaScriptCompletionDataProvider.SimplePlaceholderRegex().Replace(insertText, "");

        textArea.Document.Replace(completionSegment, insertText);
    }
}

/// <summary>
/// 参数代码提示数据
/// </summary>
public class ParameterCompletionData(DosageParameter parameter) : ICompletionData
{
    public string? Text { get; } = parameter.Name;

    public object Content { get; } = $"{parameter.Name} ({parameter.DataType})";

    public object Description { get; } =
        $"{parameter.DisplayName}\n类型: {parameter.DataType}\n描述: {parameter.Description}";

    public ImageSource? Image => null;
    public double Priority => 0.8;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
        textArea.Document.Replace(completionSegment, Text);
}

/// <summary>
/// 属性代码提示数据
/// </summary>
public class PropertyCompletionData(string name, string description) : ICompletionData
{
    public string Text { get; } = name;

    public object Content { get; } = name;

    public object Description { get; } = description;

    public ImageSource? Image => null;
    public double Priority => 0.7;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) =>
        textArea.Document.Replace(completionSegment, Text);
}

/// <summary>
/// 关键字代码提示数据
/// </summary>
public class KeywordCompletionData(string? text, string description, string? insertionText) : ICompletionData
{
    public string? Text { get; } = text;

    public object? Content { get; } = text;

    public object Description { get; } = description;

    public ImageSource? Image => null;
    public double Priority => 0.9;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var insertText = insertionText;

        // 简单的模板变量替换
        if (insertText == null) return;
        insertText = JavaScriptCompletionDataProvider.PlaceholderRegex().Replace(insertText, "$1");
        insertText = JavaScriptCompletionDataProvider.SimplePlaceholderRegex().Replace(insertText, "");

        textArea.Document.Replace(completionSegment, insertText);
    }
}