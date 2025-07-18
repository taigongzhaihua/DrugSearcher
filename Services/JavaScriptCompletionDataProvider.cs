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
        public void UpdateParameters(List<DosageParameter> parameters)
        {
            _parameters = parameters ?? [];
        }

        /// <summary>
        /// 更新当前代码
        /// </summary>
        public void UpdateCurrentCode(string code)
        {
            _currentCode = code ?? string.Empty;
        }

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
                return completionData
                    .Where(cd => cd.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(cd => cd.Priority)
                    .ThenBy(cd => cd.Text)
                    .ToList();
            }

            return completionData
                .OrderBy(cd => cd.Priority)
                .ThenBy(cd => cd.Text)
                .ToList();
        }

        /// <summary>
        /// 创建静态代码提示数据
        /// </summary>
        private List<ICompletionData> CreateStaticCompletionData()
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
        private string GenerateInsertionText(FunctionDefinition func)
        {
            var parameters = func.Parameters.Select((p, i) => $"${{${i + 1}:{p.Name}}}");
            return $"{func.Name}({string.Join(", ", parameters)});";
        }

        /// <summary>
        /// 获取关键字模板
        /// </summary>
        private string GetKeywordTemplate(string keyword)
        {
            return keyword switch
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
    }

    /// <summary>
    /// 局部变量信息
    /// </summary>
    public class LocalVariable
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int LineNumber { get; set; }
        public bool IsFunction { get; set; }
    }

    /// <summary>
    /// 局部变量代码提示数据
    /// </summary>
    public class LocalVariableCompletionData : ICompletionData
    {
        public string Text { get; }
        public object Content { get; }
        public object Description { get; }
        public ImageSource Image { get; }
        public double Priority { get; }

        private readonly LocalVariable _variable;

        public LocalVariableCompletionData(LocalVariable variable)
        {
            _variable = variable;
            Text = variable.Name;
            Content = $"{variable.Name} ({variable.Type})";
            Description = $"局部{GetTypeDescription(variable.Type)}: {variable.Name}" +
                         (variable.LineNumber > 0 ? $"\n声明位置: 第{variable.LineNumber}行" : "");
            Priority = 0.6;
            Image = null;
        }

        private string GetTypeDescription(string type)
        {
            return type switch
            {
                "var" => "变量",
                "let" => "变量",
                "const" => "常量",
                "function" => "函数",
                "parameter" => "参数",
                _ => "变量"
            };
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }

    /// <summary>
    /// 方法代码提示数据
    /// </summary>
    public class MethodCompletionData : ICompletionData
    {
        public string Text { get; }
        public object Content { get; }
        public object Description { get; }
        public ImageSource Image { get; }
        public double Priority { get; }

        private readonly string _insertionText;

        public MethodCompletionData(string text, string signature, string description, string insertionText)
        {
            Text = text;
            Content = signature;
            Description = description;
            _insertionText = insertionText;
            Priority = 1.0;
            Image = null;
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var insertText = _insertionText;

            // 简单的模板变量替换
            insertText = Regex.Replace(insertText, @"\$\{\d+:([^}]+)\}", "$1");
            insertText = Regex.Replace(insertText, @"\$\{\d+\}", "");

            textArea.Document.Replace(completionSegment, insertText);
        }
    }

    /// <summary>
    /// 参数代码提示数据
    /// </summary>
    public class ParameterCompletionData : ICompletionData
    {
        public string Text { get; }
        public object Content { get; }
        public object Description { get; }
        public ImageSource Image { get; }
        public double Priority { get; }

        private readonly DosageParameter _parameter;

        public ParameterCompletionData(DosageParameter parameter)
        {
            _parameter = parameter;
            Text = parameter.Name;
            Content = $"{parameter.Name} ({parameter.DataType})";
            Description = $"{parameter.DisplayName}\n类型: {parameter.DataType}\n描述: {parameter.Description}";
            Priority = 0.8;
            Image = null;
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }

    /// <summary>
    /// 关键字代码提示数据
    /// </summary>
    public class KeywordCompletionData : ICompletionData
    {
        public string Text { get; }
        public object Content { get; }
        public object Description { get; }
        public ImageSource Image { get; }
        public double Priority { get; }

        private readonly string _insertionText;

        public KeywordCompletionData(string text, string description, string insertionText)
        {
            Text = text;
            Content = text;
            Description = description;
            _insertionText = insertionText;
            Priority = 0.9;
            Image = null;
        }

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            var insertText = _insertionText;

            // 简单的模板变量替换
            insertText = Regex.Replace(insertText, @"\$\{\d+:([^}]+)\}", "$1");
            insertText = Regex.Replace(insertText, @"\$\{\d+\}", "");

            textArea.Document.Replace(completionSegment, insertText);
        }
    }
}