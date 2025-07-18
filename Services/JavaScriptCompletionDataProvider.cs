using DrugSearcher.Models;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace DrugSearcher.Services
{
    /// <summary>
    /// JavaScript代码提示数据提供程序
    /// </summary>
    public class JavaScriptCompletionDataProvider
    {
        private readonly List<ICompletionData> _staticCompletionData;
        private List<DosageParameter> _parameters = [];
        private string _currentCode = string.Empty;

        public JavaScriptCompletionDataProvider()
        {
            _staticCompletionData = CreateStaticCompletionData();
        }

        /// <summary>
        /// 更新参数列表
        /// </summary>
        public void UpdateParameters(List<DosageParameter> parameters) => _parameters = parameters ?? [];

        /// <summary>
        /// 更新当前代码
        /// </summary>
        public void UpdateCurrentCode(string code) => _currentCode = code ?? string.Empty;

        /// <summary>
        /// 获取代码提示数据
        /// </summary>
        public virtual IList<ICompletionData> GetCompletionData(string currentWord, int currentPosition)
        {
            var completionData = new List<ICompletionData>();

            // 添加静态方法
            completionData.AddRange(_staticCompletionData);

            // 添加动态参数
            foreach (var parameter in _parameters)
            {
                completionData.Add(new ParameterCompletionData(parameter));
            }

            // 如果有当前输入的词，进行过滤
            if (!string.IsNullOrEmpty(currentWord))
            {
                return [.. completionData
                    .Where(cd => cd.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(cd => cd.Priority)
                    .ThenBy(cd => cd.Text)];
            }

            return [.. completionData
                .OrderBy(cd => cd.Priority)
                .ThenBy(cd => cd.Text)];
        }

        /// <summary>
        /// 创建静态代码提示数据
        /// </summary>
        private static List<ICompletionData> CreateStaticCompletionData()
        {
            var completionData = new List<ICompletionData>();

            // 从统一定义添加自定义函数
            foreach (var func in JavaScriptLanguageDefinition.CustomFunctions.Functions.Values)
            {
                completionData.Add(new MethodCompletionData(
                    func.Name,
                    func.Signature,
                    func.Description,
                    GenerateInsertionText(func)
                ));
            }

            // 从统一定义添加内置函数
            foreach (var func in JavaScriptLanguageDefinition.BuiltIns.GlobalFunctions)
            {
                completionData.Add(new MethodCompletionData(
                    func.Key,
                    func.Key + "()",
                    func.Value,
                    func.Key + "(${1})"
                ));
            }

            // 添加关键字
            foreach (var keyword in JavaScriptLanguageDefinition.Keywords.All)
            {
                completionData.Add(new KeywordCompletionData(
                    keyword,
                    $"JavaScript关键字: {keyword}",
                    GetKeywordTemplate(keyword)
                ));
            }

            // 添加Math对象方法
            if (JavaScriptLanguageDefinition.BuiltIns.ObjectMethods.TryGetValue("Math", out var mathMethods))
            {
                foreach (var method in mathMethods)
                {
                    completionData.Add(new MethodCompletionData(
                        $"Math.{method}",
                        $"Math.{method}()",
                        $"数学函数: {method}",
                        $"Math.{method}(${{1}})"
                    ));
                }
            }

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
            "for" => "for (var ${1:i} = 0; ${1:i} < ${2:length}; ${1:i}++) {\n    ${3:// 代码}\n}",
            "while" => "while (${1:condition}) {\n    ${2:// 代码}\n}",
            "function" => "function ${1:name}(${2:params}) {\n    ${3:// 代码}\n    return ${4:result};\n}",
            "var" => "var ${1:variable} = ${2:value};",
            "let" => "let ${1:variable} = ${2:value};",
            "const" => "const ${1:variable} = ${2:value};",
            "return" => "return ${1:value};",
            "try" => "try {\n    ${1:// 代码}\n} catch (${2:error}) {\n    ${3:// 错误处理}\n}",
            _ => keyword
        };
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

        public ImageSource? Image { get; } = null;
        public double Priority { get; } = 0.6;

        private readonly LocalVariable _variable = variable;

        private static string GetTypeDescription(string? type) => type switch
        {
            "var" => "变量",
            "let" => "变量",
            "const" => "常量",
            "function" => "函数",
            "parameter" => "参数",
            _ => "变量"
        };

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) => textArea.Document.Replace(completionSegment, Text);
    }

    /// <summary>
    /// 方法代码提示数据
    /// </summary>
    public partial class MethodCompletionData(string? text, string? signature, string? description, string insertionText)
        : ICompletionData
    {
        public string? Text { get; } = text;
        public object? Content { get; } = signature;
        public object? Description { get; } = description;
        public ImageSource? Image { get; } = null;
        public double Priority { get; } = 1.0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var insertText = insertionText;

            // 简单的模板变量替换
            insertText = InsertTextRegex1().Replace(insertText, "$1");
            insertText = InsertTextRegex2().Replace(insertText, "");

            textArea.Document.Replace(completionSegment, insertText);
        }

        [GeneratedRegex(@"\$\{\d+:([^}]+)\}")]
        private static partial Regex InsertTextRegex1();
        [GeneratedRegex(@"\$\{\d+\}")]
        private static partial Regex InsertTextRegex2();
    }

    /// <summary>
    /// 参数代码提示数据
    /// </summary>
    public class ParameterCompletionData(DosageParameter parameter) : ICompletionData
    {
        public string? Text { get; } = parameter.Name;
        public object Content { get; } = $"{parameter.Name} ({parameter.DataType})";
        public object Description { get; } = $"{parameter.DisplayName}\n类型: {parameter.DataType}\n描述: {parameter.Description}";
        public ImageSource? Image { get; } = null;
        public double Priority { get; } = 0.8;

        private readonly DosageParameter _parameter = parameter;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) => textArea.Document.Replace(completionSegment, Text);
    }

    /// <summary>
    /// 关键字代码提示数据
    /// </summary>
    public partial class KeywordCompletionData(string? text, string description, string? insertionText) : ICompletionData
    {
        public string? Text { get; } = text;
        public object? Content { get; } = text;
        public object Description { get; } = description;
        public ImageSource? Image { get; } = null;
        public double Priority { get; } = 0.9;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var insertText = insertionText;

            // 简单的模板变量替换
            if (insertText != null)
            {
                insertText = InsertTextRegex1().Replace(insertText, "$1");
                insertText = InsertTextRegex2().Replace(insertText, "");

                textArea.Document.Replace(completionSegment, insertText);
            }
        }

        [GeneratedRegex(@"\$\{\d+:([^}]+)\}")]
        private static partial Regex InsertTextRegex1();
        [GeneratedRegex(@"\$\{\d+\}")]
        private static partial Regex InsertTextRegex2();
    }
}