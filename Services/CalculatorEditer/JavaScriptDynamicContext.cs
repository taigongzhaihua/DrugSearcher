using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace DrugSearcher.Services;

/// <summary>
/// JavaScript动态上下文管理器
/// </summary>
public partial class JavaScriptDynamicContext
{
    private readonly Lock _lock = new();
    private readonly HashSet<string> _globalVariables = [];
    private readonly HashSet<string> _globalFunctions = [];
    private readonly ConcurrentDictionary<int, ScopeInfo> _scopes = new();
    private string _lastAnalyzedCode = string.Empty;

    public event EventHandler<ContextChangedEventArgs>? ContextChanged;

    /// <summary>
    /// 分析代码并更新上下文
    /// </summary>
    public void AnalyzeCode(string code, int caretPosition)
    {
        lock (_lock)
        {
            if (code == _lastAnalyzedCode)
                return;

            _lastAnalyzedCode = code;

            // 清空现有数据
            _globalVariables.Clear();
            _globalFunctions.Clear();
            _scopes.Clear();

            // 分析代码
            AnalyzeScopes(code);

            // 触发上下文变化事件
            var currentScope = GetVariablesInScope(caretPosition);
            ContextChanged?.Invoke(this, new ContextChangedEventArgs
            {
                LocalVariables = [.. _globalVariables],
                LocalFunctions = [.. _globalFunctions],
                CurrentScope = currentScope
            });
        }
    }

    /// <summary>
    /// 分析作用域
    /// </summary>
    private void AnalyzeScopes(string code)
    {
        // 移除字符串和注释
        var cleanCode = RemoveStringsAndComments(code);

        var scopeStack = new Stack<ScopeInfo>();
        var globalScope = new ScopeInfo { StartOffset = 0, EndOffset = code.Length, Level = 0 };
        _scopes[0] = globalScope;
        scopeStack.Push(globalScope);

        // 分析全局作用域的变量和函数
        AnalyzeVariablesInScope(cleanCode, globalScope);
        AnalyzeFunctionsInScope(cleanCode, globalScope);

        // 分析嵌套作用域
        var braceLevel = 0;

        for (var i = 0; i < cleanCode.Length; i++)
        {
            var ch = cleanCode[i];

            switch (ch)
            {
                case '{':
                    {
                        braceLevel++;

                        // 创建新作用域
                        var newScope = new ScopeInfo
                        {
                            StartOffset = i,
                            Level = braceLevel,
                            Parent = scopeStack.Peek()
                        };

                        _scopes[i] = newScope;
                        scopeStack.Push(newScope);
                        break;
                    }
                case '}' when braceLevel > 0:
                    {
                        if (scopeStack.Count > 1)
                        {
                            var closingScope = scopeStack.Pop();
                            closingScope.EndOffset = i;

                            // 分析该作用域内的变量
                            if (closingScope.StartOffset >= 0 && closingScope.EndOffset > closingScope.StartOffset)
                            {
                                var scopeCode = cleanCode[closingScope.StartOffset..(closingScope.EndOffset + 1)];
                                AnalyzeVariablesInScope(scopeCode, closingScope);
                            }
                        }

                        braceLevel--;
                        break;
                    }
            }
        }
    }

    /// <summary>
    /// 分析作用域内的变量
    /// </summary>
    private void AnalyzeVariablesInScope(string cleanCode, ScopeInfo scope)
    {
        // let 和 const 声明（块级作用域）
        var blockMatches = BlockScopeVariableRegex().Matches(cleanCode);

        foreach (var match in blockMatches.Cast<Match>())
        {
            if (IsMatchInScope(match.Index, scope))
            {
                ExtractVariables(match.Groups[1].Value, scope.BlockScopeVariables);
            }
        }

        // var 声明（函数作用域）
        var varMatches = VarDeclarationRegex().Matches(cleanCode);

        foreach (var match in varMatches.Cast<Match>())
        {
            if (!IsMatchInScope(match.Index, scope)) continue;
            // var 声明提升到最近的函数作用域或全局作用域
            var functionScope = FindEnclosingFunctionScope(scope);
            ExtractVariables(match.Groups[1].Value, functionScope.FunctionScopeVariables);

            // 如果是全局作用域，也添加到全局变量中
            if (functionScope.Level == 0)
            {
                ExtractVariables(match.Groups[1].Value, _globalVariables);
            }
        }

        // 函数参数
        var paramMatches = FunctionParameterRegex().Matches(cleanCode);

        foreach (var match in paramMatches.Cast<Match>())
        {
            if (!IsMatchInScope(match.Index, scope)) continue;
            var paramList = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(paramList)) continue;

            var paramNames = paramList.Split(',')
                .Select(p => p.Trim())
                .Where(n => !string.IsNullOrWhiteSpace(n));

            foreach (var paramName in paramNames)
            {
                scope.BlockScopeVariables.Add(paramName);
            }
        }
    }

    /// <summary>
    /// 分析作用域内的函数
    /// </summary>
    private void AnalyzeFunctionsInScope(string cleanCode, ScopeInfo scope)
    {
        // 函数声明
        var matches = FunctionDeclarationRegex().Matches(cleanCode);

        foreach (var match in matches.Cast<Match>())
        {
            if (!IsMatchInScope(match.Index, scope)) continue;
            var functionName = match.Groups[1].Value;
            scope.Functions.Add(functionName);

            if (scope.Level == 0)
            {
                _globalFunctions.Add(functionName);
            }
        }

        // 函数表达式
        var funcExprMatches = FunctionExpressionRegex().Matches(cleanCode);
        foreach (var match in funcExprMatches.Cast<Match>())
        {
            if (!IsMatchInScope(match.Index, scope)) continue;
            var functionName = match.Groups[1].Value;
            scope.Functions.Add(functionName);

            if (scope.Level == 0)
            {
                _globalFunctions.Add(functionName);
            }
        }

        // 箭头函数
        var arrowFuncMatches = ArrowFunctionRegex().Matches(cleanCode);
        foreach (var match in arrowFuncMatches.Cast<Match>())
        {
            if (!IsMatchInScope(match.Index, scope)) continue;
            var functionName = match.Groups[1].Value;
            scope.Functions.Add(functionName);

            if (scope.Level == 0)
            {
                _globalFunctions.Add(functionName);
            }
        }
    }

    /// <summary>
    /// 检查匹配是否在作用域内
    /// </summary>
    private bool IsMatchInScope(int matchIndex, ScopeInfo scope)
    {
        // 检查是否直接在该作用域内（不在子作用域中）
        var childScopes = _scopes.Values.Where(s => s.Parent == scope);

        if (childScopes.Any(childScope => matchIndex >= childScope.StartOffset && matchIndex <= childScope.EndOffset))
        {
            return false;
        }

        return matchIndex >= scope.StartOffset && matchIndex <= scope.EndOffset;
    }

    /// <summary>
    /// 查找包含的函数作用域
    /// </summary>
    private ScopeInfo FindEnclosingFunctionScope(ScopeInfo scope)
    {
        var current = scope;
        while (current != null)
        {
            if (current.Level == 0 || current.IsFunctionScope)
            {
                return current;
            }
            current = current.Parent;
        }
        return _scopes[0]; // 返回全局作用域
    }

    /// <summary>
    /// 提取变量名
    /// </summary>
    private static void ExtractVariables(string varList, HashSet<string> targetSet)
    {
        var varNames = varList.Split(',')
            .Select(v => v.Trim().Split('=')[0].Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n));

        foreach (var varName in varNames)
        {
            targetSet.Add(varName);
        }
    }

    /// <summary>
    /// 移除字符串和注释
    /// </summary>
    private static string RemoveStringsAndComments(string code)
    {
        var result = new System.Text.StringBuilder(code.Length);
        var i = 0;

        while (i < code.Length)
        {
            // 处理单行注释
            if (i < code.Length - 1 && code[i] == '/' && code[i + 1] == '/')
            {
                result.Append("//");
                i += 2;
                while (i < code.Length && code[i] != '\n')
                {
                    result.Append(' ');
                    i++;
                }
                if (i < code.Length)
                {
                    result.Append('\n');
                    i++;
                }
                continue;
            }

            // 处理多行注释
            if (i < code.Length - 1 && code[i] == '/' && code[i + 1] == '*')
            {
                result.Append("/*");
                i += 2;
                while (i < code.Length - 1)
                {
                    if (code[i] == '*' && code[i + 1] == '/')
                    {
                        result.Append("*/");
                        i += 2;
                        break;
                    }
                    result.Append(code[i] == '\n' ? '\n' : ' ');
                    i++;
                }
                continue;
            }

            // 处理字符串
            if (code[i] == '"' || code[i] == '\'' || code[i] == '`')
            {
                var quote = code[i];
                result.Append(quote);
                i++;

                while (i < code.Length)
                {
                    if (code[i] == '\\' && i + 1 < code.Length)
                    {
                        result.Append("  ");
                        i += 2;
                        continue;
                    }

                    if (code[i] == quote)
                    {
                        result.Append(quote);
                        i++;
                        break;
                    }

                    result.Append(' ');
                    i++;
                }
                continue;
            }

            result.Append(code[i]);
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    /// 获取特定位置的可见变量
    /// </summary>
    public HashSet<string> GetVariablesInScope(int position)
    {
        lock (_lock)
        {
            var visibleVariables = new HashSet<string>();

            // 查找包含该位置的所有作用域
            var containingScopes = _scopes.Values
                .Where(scope => position >= scope.StartOffset && position <= scope.EndOffset)
                .OrderByDescending(scope => scope.Level)
                .ToList();

            // 收集所有可见的变量
            foreach (var scope in containingScopes)
            {
                // 添加块级作用域变量
                visibleVariables.UnionWith(scope.BlockScopeVariables);

                // 添加函数
                visibleVariables.UnionWith(scope.Functions);

                // 查找函数作用域变量
                var funcScope = FindEnclosingFunctionScope(scope);
                visibleVariables.UnionWith(funcScope.FunctionScopeVariables);
            }

            // 添加全局变量和函数
            visibleVariables.UnionWith(_globalVariables);
            visibleVariables.UnionWith(_globalFunctions);

            return visibleVariables;
        }
    }

    /// <summary>
    /// 获取所有局部标识符
    /// </summary>
    public HashSet<string?> GetAllLocalIdentifiers()
    {
        lock (_lock)
        {
            var identifiers = new HashSet<string?>();

            foreach (var scope in _scopes.Values)
            {
                identifiers.UnionWith(scope.BlockScopeVariables);
                identifiers.UnionWith(scope.FunctionScopeVariables);
                identifiers.UnionWith(scope.Functions);
            }

            return identifiers;
        }
    }

    [GeneratedRegex(@"\b(?:let|const)\s+([a-zA-Z_$][a-zA-Z0-9_$]*(?:\s*,\s*[a-zA-Z_$][a-zA-Z0-9_$]*)*)")]
    private static partial Regex BlockScopeVariableRegex();

    [GeneratedRegex(@"\bvar\s+([a-zA-Z_$][a-zA-Z0-9_$]*(?:\s*,\s*[a-zA-Z_$][a-zA-Z0-9_$]*)*)")]
    private static partial Regex VarDeclarationRegex();

    [GeneratedRegex(@"function\s+[a-zA-Z_$][a-zA-Z0-9_$]*\s*\(([^)]*)\)")]
    private static partial Regex FunctionParameterRegex();

    [GeneratedRegex(@"\bfunction\s+([a-zA-Z_$][a-zA-Z0-9_$]*)\s*\(")]
    private static partial Regex FunctionDeclarationRegex();

    [GeneratedRegex(@"\b(?:const|let|var)\s+([a-zA-Z_$][a-zA-Z0-9_$]*)\s*=\s*function\s*\(")]
    private static partial Regex FunctionExpressionRegex();

    [GeneratedRegex(@"\b(?:const|let|var)\s+([a-zA-Z_$][a-zA-Z0-9_$]*)\s*=\s*(?:\([^)]*\)|[a-zA-Z_$][a-zA-Z0-9_$]*)\s*=>")]
    private static partial Regex ArrowFunctionRegex();
}

/// <summary>
/// 作用域信息
/// </summary>
public class ScopeInfo
{
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public int Level { get; set; }
    public ScopeInfo? Parent { get; set; }
    public bool IsFunctionScope { get; set; }
    public HashSet<string> BlockScopeVariables { get; } = [];
    public HashSet<string> FunctionScopeVariables { get; } = [];
    public HashSet<string> Functions { get; } = [];
}

/// <summary>
/// 上下文变化事件参数
/// </summary>
public class ContextChangedEventArgs : EventArgs
{
    public HashSet<string> LocalVariables { get; set; } = [];
    public HashSet<string> LocalFunctions { get; set; } = [];
    public HashSet<string> CurrentScope { get; set; } = [];
}