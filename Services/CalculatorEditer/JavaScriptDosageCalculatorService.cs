using DrugSearcher.Models;
using DrugSearcher.Repositories;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace DrugSearcher.Services;

/// <summary>
/// JavaScript剂量计算器服务
/// </summary>
public class JavaScriptDosageCalculatorService(
    IDosageCalculatorRepository calculatorRepository,
    ILogger<JavaScriptDosageCalculatorService> logger)
{
    // 缓存JsonSerializerOptions实例
    private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// 根据统一药物信息获取计算器列表
    /// </summary>
    public async Task<List<DosageCalculator>> GetCalculatorsForDrugAsync(BaseDrugInfo drugInfo) =>
        await calculatorRepository.GetByUnifiedDrugAsync(drugInfo);

    /// <summary>
    /// 根据数据源和药物ID获取计算器列表
    /// </summary>
    public async Task<List<DosageCalculator>> GetCalculatorsForDrugAsync(DataSource dataSource, int drugId) =>
        await calculatorRepository.GetByDataSourceAndDrugIdAsync(dataSource, drugId);

    /// <summary>
    /// 根据药物标识符获取计算器列表
    /// </summary>
    public async Task<List<DosageCalculator>> GetCalculatorsForDrugAsync(string drugIdentifier) =>
        await calculatorRepository.GetByDrugIdentifierAsync(drugIdentifier);

    /// <summary>
    /// 获取计算器参数
    /// </summary>
    public async Task<List<DosageParameter>> GetCalculatorParametersAsync(int calculatorId)
    {
        var calculator = await calculatorRepository.GetByIdAsync(calculatorId);
        if (calculator == null || string.IsNullOrEmpty(calculator.ParameterDefinitions))
            return [];

        try
        {
            var parameters = JsonSerializer.Deserialize<List<DosageParameter>>(
                calculator.ParameterDefinitions, CachedJsonSerializerOptions) ?? [];

            // 设置初始UI值
            foreach (var param in parameters)
            {
                param.Value = param.GetDefaultValueByDataType();
            }

            logger.LogDebug("Successfully loaded {Count} parameters for calculator {CalculatorId}",
                parameters.Count, calculatorId);

            return parameters;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON deserialization failed for calculator {CalculatorId}. Content: {Content}",
                calculatorId, calculator.ParameterDefinitions);
            return [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to deserialize parameter definitions for calculator {CalculatorId}", calculatorId);
            return [];
        }
    }

    /// <summary>
    /// 计算剂量
    /// </summary>
    public async Task<List<DosageCalculationResult>> CalculateDosageAsync(DosageCalculationRequest request)
    {
        var calculator = await calculatorRepository.GetByIdAsync(request.CalculatorId)
            ?? throw new ArgumentException("Calculator not found");
        return await ExecuteJavaScriptAsync(calculator.CalculationCode, request.Parameters);
    }

    /// <summary>
    /// 测试计算
    /// </summary>
    public async Task<List<DosageCalculationResult>> TestCalculationAsync(DosageCalculationRequest request)
    {
        try
        {
            logger.LogInformation("开始测试计算");

            // 如果请求中包含计算代码，直接使用；否则从数据库获取
            string calculationCode;
            if (!string.IsNullOrEmpty(request.CalculationCode))
            {
                calculationCode = request.CalculationCode;
            }
            else if (request.CalculatorId > 0)
            {
                var calculator = await calculatorRepository.GetByIdAsync(request.CalculatorId)
                    ?? throw new ArgumentException("Calculator not found");
                calculationCode = calculator.CalculationCode;
            }
            else
            {
                throw new ArgumentException("Either CalculationCode or CalculatorId must be provided");
            }

            // 执行JavaScript代码
            var results = await ExecuteJavaScriptAsync(calculationCode, request.Parameters);

            logger.LogInformation("测试计算完成，共 {Count} 个结果", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "测试计算失败");
            return
            [
                new()
                {
                    Description = "测试失败",
                    IsWarning = true,
                    WarningMessage = $"测试执行失败: {ex.Message}"
                }
            ];
        }
    }

    /// <summary>
    /// 更新计算器
    /// </summary>
    public async Task<DosageCalculator> UpdateCalculatorAsync(DosageCalculator calculator)
    {
        try
        {
            // 验证代码
            if (!ValidateJavaScript(calculator.CalculationCode))
            {
                throw new ArgumentException("JavaScript代码验证失败");
            }

            // 确保更新时间
            calculator.UpdatedAt = DateTime.Now;

            // 更新计算器
            var updatedCalculator = await calculatorRepository.UpdateAsync(calculator);
            logger.LogInformation("计算器更新成功: {CalculatorName}", calculator.CalculatorName);
            return updatedCalculator;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新计算器失败: {CalculatorName}", calculator.CalculatorName);
            throw;
        }
    }

    /// <summary>
    /// 验证并保存计算器
    /// </summary>
    public async Task<DosageCalculator> ValidateAndSaveCalculatorAsync(DosageCalculator calculator)
    {
        try
        {
            // 验证计算器名称唯一性
            var existingCalculators = await calculatorRepository.GetByDrugIdentifierAsync(calculator.DrugIdentifier);
            if (existingCalculators.Any(c => c.CalculatorName == calculator.CalculatorName && c.Id != calculator.Id))
            {
                throw new InvalidOperationException($"计算器名称 '{calculator.CalculatorName}' 已存在");
            }

            // 验证JavaScript代码
            if (!ValidateJavaScript(calculator.CalculationCode))
            {
                throw new InvalidOperationException("JavaScript代码验证失败");
            }

            // 验证参数定义
            if (!string.IsNullOrEmpty(calculator.ParameterDefinitions))
            {
                var parameters = JsonSerializer.Deserialize<List<DosageParameter>>(
                    calculator.ParameterDefinitions, CachedJsonSerializerOptions);

                if (parameters == null || parameters.Count == 0)
                {
                    throw new InvalidOperationException("参数定义不能为空");
                }

                // 验证参数名称唯一性
                var duplicateNames = parameters
                    .GroupBy(p => p.Name)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);

                var enumerable = duplicateNames.ToList();
                if (enumerable.Count != 0)
                {
                    throw new InvalidOperationException($"参数名称重复: {string.Join(", ", enumerable)}");
                }
            }

            // 保存计算器
            return await SaveCalculatorAsync(calculator);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "验证并保存计算器失败: {CalculatorName}", calculator.CalculatorName);
            throw;
        }
    }

    /// <summary>
    /// 执行JavaScript代码
    /// </summary>
    private async Task<List<DosageCalculationResult>> ExecuteJavaScriptAsync(string code, Dictionary<string, object> parameters) =>
        await Task.Run(() =>
        {
            try
            {
                using var engine = new V8ScriptEngine();
                engine.DefaultAccess = Microsoft.ClearScript.ScriptAccess.ReadOnly;

                // 构建完整的JavaScript代码
                var fullScript = BuildFullJavaScriptCode(code, parameters);

                // 执行完整的脚本
                engine.Execute(fullScript);

                // 获取结果
                var resultsJson = engine.Evaluate("JSON.stringify(results)").ToString();
                var results = JsonSerializer.Deserialize<List<DosageCalculationResult>>(
                    resultsJson ?? string.Empty, CachedJsonSerializerOptions) ?? [];

                logger.LogDebug("JavaScript execution completed successfully, {Count} results returned", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "JavaScript execution failed: {Message}", ex.Message);
                return
                [
                    new()
                    {
                        Description = "计算错误",
                        IsWarning = true,
                        WarningMessage = $"脚本执行失败: {ex.Message}"
                    }
                ];
            }
        });

    /// <summary>
    /// 构建完整的JavaScript代码
    /// </summary>
    private string BuildFullJavaScriptCode(string userCode, Dictionary<string, object> parameters)
    {
        var scriptBuilder = new StringBuilder();

        // 1. 添加参数声明
        scriptBuilder.AppendLine("// 参数声明");
        foreach (var (key, value) in parameters)
        {
            var jsValue = ConvertToJavaScriptLiteral(value);
            scriptBuilder.AppendLine($"var {key} = {jsValue};");
        }

        // 2. 添加结果数组
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("// 结果数组");
        scriptBuilder.AppendLine("var results = [];");

        // 3. 添加辅助函数
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("// 辅助函数");
        scriptBuilder.AppendLine("""

                                 function addResult(description, dose, unit, frequency, duration, notes, isWarning, warningMessage) {
                                     results.push({
                                         Description: String(description || ''),
                                         Dose: Number(dose) || 0,
                                         Unit: String(unit || ''),
                                         Frequency: String(frequency || ''),
                                         Duration: String(duration || ''),
                                         Notes: String(notes || ''),
                                         IsWarning: Boolean(isWarning || false),
                                         WarningMessage: String(warningMessage || '')
                                     });
                                 }

                                 function addWarning(description, dose, unit, frequency, warningMessage) {
                                     addResult(description, dose, unit, frequency, '', '', true, warningMessage);
                                 }

                                 function addNormalResult(description, dose, unit, frequency, duration, notes) {
                                     addResult(description, dose, unit, frequency, duration, notes, false, '');
                                 }

                                 function round(value, decimals) {
                                     var num = Number(value);
                                     var dec = Number(decimals) || 0;
                                     if (isNaN(num)) return 0;
                                     return Math.round(num * Math.pow(10, dec)) / Math.pow(10, dec);
                                 }

                                 function clamp(value, min, max) {
                                     var num = Number(value);
                                     var minNum = Number(min);
                                     var maxNum = Number(max);
                                     if (isNaN(num)) return minNum;
                                     return Math.min(Math.max(num, minNum), maxNum);
                                 }

                                 function isValidNumber(value) {
                                     var num = Number(value);
                                     return !isNaN(num) && isFinite(num);
                                 }

                                 function safeParseFloat(value, defaultValue) {
                                     if (typeof value === 'number') return value;
                                     var parsed = parseFloat(value);
                                     return isNaN(parsed) ? (Number(defaultValue) || 0) : parsed;
                                 }

                                 function safeParseInt(value, defaultValue) {
                                     if (typeof value === 'number') return Math.floor(value);
                                     var parsed = parseInt(value);
                                     return isNaN(parsed) ? (Number(defaultValue) || 0) : parsed;
                                 }

                                 """);

        // 4. 将用户代码包装在函数中执行
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("// 用户代码执行函数");
        scriptBuilder.AppendLine("function executeUserCode() {");
        scriptBuilder.AppendLine("    try {");

        // 添加用户代码，每行都缩进
        var userCodeLines = userCode.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in userCodeLines)
        {
            scriptBuilder.AppendLine($"        {line}");
        }

        scriptBuilder.AppendLine("    } catch (error) {");
        scriptBuilder.AppendLine("        addWarning('执行错误', 0, '', '', '代码执行出错: ' + error.message);");
        scriptBuilder.AppendLine("    }");
        scriptBuilder.AppendLine("}");

        // 5. 执行用户代码函数
        scriptBuilder.AppendLine();
        scriptBuilder.AppendLine("// 执行用户代码");
        scriptBuilder.AppendLine("executeUserCode();");

        var fullScript = scriptBuilder.ToString();

        // 记录完整脚本用于调试
        logger.LogDebug("Full JavaScript code:\n{Script}", fullScript);

        return fullScript;
    }

    /// <summary>
    /// 将值转换为JavaScript字面量
    /// </summary>
    private static string ConvertToJavaScriptLiteral(object? value)
    {
        if (value == null)
            return "null";

        return value switch
        {
            bool b => b ? "true" : "false",
            string s => $"\"{EscapeJavaScriptString(s)}\"",
            double d => FormatNumber(d),
            float f => FormatNumber(f),
            decimal dec => FormatNumber((double)dec),
            int i => i.ToString(),
            long l => l.ToString(),
            byte b => b.ToString(),
            short s => s.ToString(),
            uint ui => ui.ToString(),
            ulong ul => ul.ToString(),
            JsonElement jsonElement => ConvertJsonElementToLiteral(jsonElement),
            System.Collections.IEnumerable enumerable => ConvertArrayToLiteral(enumerable),
            _ => $"\"{EscapeJavaScriptString(value.ToString() ?? "")}\""
        };
    }

    /// <summary>
    /// 转换数组为JavaScript字面量
    /// </summary>
    private static string ConvertArrayToLiteral(System.Collections.IEnumerable enumerable)
    {
        var items = new List<string>();
        foreach (var item in enumerable)
        {
            items.Add(ConvertToJavaScriptLiteral(item));
        }
        return $"[{string.Join(", ", items)}]";
    }

    /// <summary>
    /// 格式化数字为JavaScript兼容格式
    /// </summary>
    private static string FormatNumber(double value)
    {
        if (double.IsNaN(value))
            return "NaN";
        if (double.IsPositiveInfinity(value))
            return "Infinity";
        if (double.IsNegativeInfinity(value))
            return "-Infinity";

        return value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 转换JsonElement为JavaScript字面量
    /// </summary>
    private static string ConvertJsonElementToLiteral(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => $"\"{EscapeJavaScriptString(element.GetString() ?? "")}\"",
        JsonValueKind.Number => FormatNumber(element.GetDouble()),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        JsonValueKind.Undefined => "undefined",
        JsonValueKind.Array => ConvertJsonArrayToLiteral(element),
        JsonValueKind.Object => ConvertJsonObjectToLiteral(element),
        _ => $"\"{EscapeJavaScriptString(element.ToString())}\""
    };

    /// <summary>
    /// 转换JSON数组为JavaScript字面量
    /// </summary>
    private static string ConvertJsonArrayToLiteral(JsonElement array)
    {
        var items = array.EnumerateArray()
            .Select(ConvertJsonElementToLiteral)
            .ToList();
        return $"[{string.Join(", ", items)}]";
    }

    /// <summary>
    /// 转换JSON对象为JavaScript字面量
    /// </summary>
    private static string ConvertJsonObjectToLiteral(JsonElement obj)
    {
        var properties = obj.EnumerateObject()
            .Select(prop => $"\"{EscapeJavaScriptString(prop.Name)}\": {ConvertJsonElementToLiteral(prop.Value)}")
            .ToList();
        return $"{{{string.Join(", ", properties)}}}";
    }

    /// <summary>
    /// 转义JavaScript字符串
    /// </summary>
    private static string EscapeJavaScriptString(string input) => input
            .Replace("\\", @"\\")
            .Replace("\"", "\\\"")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f")
            .Replace("\0", "\\0");

    /// <summary>
    /// 验证JavaScript代码
    /// </summary>
    public bool ValidateJavaScript(string code)
    {
        try
        {
            using var engine = new V8ScriptEngine();
            engine.DefaultAccess = Microsoft.ClearScript.ScriptAccess.ReadOnly;

            // 构建完整的测试环境
            var testScript = BuildTestScript(code);

            // 尝试编译完整脚本
            engine.Compile(testScript);

            // 尝试执行完整脚本
            engine.Execute(testScript);

            logger.LogDebug("JavaScript validation passed");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "JavaScript validation failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// 构建测试脚本
    /// </summary>
    private string BuildTestScript(string userCode)
    {
        // 创建测试参数
        var testParams = new Dictionary<string, object>
        {
            ["weight"] = 70.0,
            ["age"] = 30.0,
            ["height"] = 170.0,
            ["severity"] = "中度",
            ["renalFunction"] = "正常",
            ["hepaticFunction"] = "正常",
            ["isPregnant"] = false,
            ["isLactating"] = false,
            ["hasAllergy"] = false,
            ["gender"] = "男",
            ["bmi"] = 24.0,
            ["testArray"] = new[] { "item1", "item2", "item3" }
        };

        // 使用相同的代码生成逻辑
        return BuildFullJavaScriptCode(userCode, testParams);
    }

    /// <summary>
    /// 保存计算器
    /// </summary>
    public async Task<DosageCalculator> SaveCalculatorAsync(DosageCalculator calculator)
    {
        // 验证代码
        if (!ValidateJavaScript(calculator.CalculationCode))
        {
            throw new ArgumentException("JavaScript代码验证失败");
        }

        // 生成药物标识符
        calculator.DrugIdentifier = DosageCalculator.GenerateDrugIdentifier(
            calculator.DataSource, calculator.OriginalDrugId);

        // 检查是否存在相同名称的计算器
        if (calculator.Id == 0)
        {
            var exists = await calculatorRepository.ExistsAsync(
                calculator.DataSource, calculator.OriginalDrugId, calculator.CalculatorName);
            if (exists)
            {
                throw new InvalidOperationException("同名计算器已存在");
            }

            calculator.CreatedBy = "taigongzhaihua";
            return await calculatorRepository.AddAsync(calculator);
        }
        else
        {
            return await calculatorRepository.UpdateAsync(calculator);
        }
    }

    /// <summary>
    /// 删除计算器
    /// </summary>
    public async Task<bool> DeleteCalculatorAsync(int id) =>
        await calculatorRepository.DeleteAsync(id);

    /// <summary>
    /// 获取计算器统计信息
    /// </summary>
    public async Task<DosageCalculatorStatistics> GetStatisticsAsync() =>
        await calculatorRepository.GetStatisticsAsync();

    /// <summary>
    /// 搜索计算器
    /// </summary>
    public async Task<List<DosageCalculator>> SearchCalculatorsAsync(string keyword) =>
        await calculatorRepository.SearchAsync(keyword);
}