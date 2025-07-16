using DrugSearcher.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace DrugSearcher.Services;

/// <summary>
/// AI药物计算器生成服务
/// </summary>
public class DosageCalculatorAiService
{
    private readonly ILogger<DosageCalculatorAiService> _logger;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// AI药物计算器生成服务
    /// </summary>
    public DosageCalculatorAiService(ILogger<DosageCalculatorAiService> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;

        httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    private const string ApiKey = "sk-dfb77b5548ee436598c26e39c2c75432";
    private const string ApiEndpoint = "https://api.deepseek.com/chat/completions";

    /// <summary>
    /// 根据药物信息生成计算器
    /// </summary>
    public async Task<DosageCalculatorGenerationResult> GenerateCalculatorAsync(
        BaseDrugInfo drugInfo,
        string calculatorType = "通用剂量计算器",
        string additionalRequirements = "")
    {
        try
        {
            _logger.LogInformation("开始为药物 {DrugName} 生成计算器", drugInfo.DrugName);

            // 构建AI提示词
            var prompt = BuildPrompt(drugInfo, calculatorType, additionalRequirements);

            // 调用AI API
            var aiResponse = await CallDeepSeekApiAsync(prompt);

            // 解析AI响应
            var result = ParseAiResponse(aiResponse, drugInfo);

            _logger.LogInformation("成功为药物 {DrugName} 生成计算器", drugInfo.DrugName);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成计算器失败: {DrugName}", drugInfo.DrugName);
            return new DosageCalculatorGenerationResult
            {
                Success = false,
                ErrorMessage = $"生成计算器失败: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// 构建AI提示词
    /// </summary>
    private string BuildPrompt(BaseDrugInfo drugInfo, string calculatorType, string additionalRequirements)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# 药物计算器生成任务");
        sb.AppendLine();
        sb.AppendLine("请根据以下药物信息生成一个完整的剂量计算器配置。");
        sb.AppendLine();

        // 药物基本信息
        sb.AppendLine("## 药物信息:");
        sb.AppendLine($"- 药品名称: {drugInfo.DrugName}");
        sb.AppendLine($"- 规格: {drugInfo.Specification}");
        sb.AppendLine($"- 生产厂家: {drugInfo.Manufacturer}");
        sb.AppendLine($"- 批准文号: {drugInfo.ApprovalNumber}");

        switch (drugInfo)
        {
            // 详细信息（如果有）
            case LocalDrugInfo localDrug:
                sb.AppendLine($"- 适应症: {localDrug.Indications}");
                sb.AppendLine($"- 用法用量: {localDrug.Dosage}");
                sb.AppendLine($"- 注意事项: {localDrug.Precautions}");
                break;
            case OnlineDrugInfo onlineDrug:
                sb.AppendLine($"- 主要成分: {onlineDrug.MainIngredients}");
                sb.AppendLine($"- 适应症: {onlineDrug.Indications}");
                sb.AppendLine($"- 用法用量: {onlineDrug.Dosage}");
                sb.AppendLine($"- 注意事项: {onlineDrug.Precautions}");
                break;
        }

        sb.AppendLine();
        sb.AppendLine($"## 计算器类型: {calculatorType}");
        if (!string.IsNullOrEmpty(additionalRequirements))
        {
            sb.AppendLine($"## 额外要求: {additionalRequirements}");
        }

        sb.AppendLine();
        sb.AppendLine("## 输出要求:");
        sb.AppendLine("请以JSON格式输出计算器配置，包含以下部分：");
        sb.AppendLine();

        sb.AppendLine("### 1. 基本信息");
        sb.AppendLine("- calculatorName: 计算器名称");
        sb.AppendLine("- description: 计算器描述");
        sb.AppendLine();

        sb.AppendLine("### 2. 参数定义 (parameters)");
        sb.AppendLine("每个参数包含：");
        sb.AppendLine("- name: 参数名称（英文，用于代码中）");
        sb.AppendLine("- displayName: 显示名称（中文）");
        sb.AppendLine("- dataType: 数据类型（\"number\", \"select\", \"boolean\", \"text\"）");
        sb.AppendLine("- unit: 单位");
        sb.AppendLine("- isRequired: 是否必填");
        sb.AppendLine("- defaultValue: 默认值");
        sb.AppendLine("- minValue: 最小值（仅数字类型）");
        sb.AppendLine("- maxValue: 最大值（仅数字类型）");
        sb.AppendLine("- options: 选项列表（仅选择类型）");
        sb.AppendLine("- description: 参数描述");
        sb.AppendLine();

        sb.AppendLine("### 3. 计算代码 (calculationCode)");
        sb.AppendLine("JavaScript代码要求：");
        sb.AppendLine("- 参数已在函数作用域中声明，直接使用即可");
        sb.AppendLine("- 不要使用var、let等重新声明参数变量，会导致脚本错误");
        sb.AppendLine("- 使用 addNormalResult(description, dose, unit, frequency, duration, notes) 添加正常结果");
        sb.AppendLine("- 使用 addWarning(description, dose, unit, frequency, warningMessage) 添加警告");
        sb.AppendLine("- 使用 round(value, decimals) 四舍五入");
        sb.AppendLine("- 使用 clamp(value, min, max) 限制数值范围");
        sb.AppendLine("- 包含详细的输入验证");
        sb.AppendLine("- 包含年龄、体重相关的剂量调整");
        sb.AppendLine("- 包含安全上限检查");
        sb.AppendLine("- 若有多种用药方案，可以全部列举出来，不用考虑用法单一性");
        sb.AppendLine("- 注意年龄单位");
        sb.AppendLine("- 注意每日剂量和每次剂量的区别");
        sb.AppendLine("- 注意剂量单位（mg/kg、mg/m²等）");
        sb.AppendLine("- 注意剂量频率（每日、每次等）");
        sb.AppendLine("- 注意：如无特别声明，医学上儿童是指12岁以下年龄段，12岁以上均按成人处理。");
        sb.AppendLine("- 注意：如标明为未成年人，则为18岁以下，请注意儿童/成人 与 未成年人/成人 的区别。");
        sb.AppendLine("- 可以使用return语句在验证失败时提前退出");
        sb.AppendLine();

        sb.AppendLine("### 4. 常见参数建议");
        sb.AppendLine("- weight: 体重（kg）");
        sb.AppendLine("- age: 年龄（岁）");
        sb.AppendLine("- ageUnit: 年龄单位（岁/月/天）");
        sb.AppendLine("- height: 身高（cm）");
        sb.AppendLine("- severity: 病情严重程度（轻度/中度/重度）");
        sb.AppendLine("- renalFunction: 肾功能状态（正常/轻度损伤/中度损伤/重度损伤）");
        sb.AppendLine("- hepaticFunction: 肝功能状态（正常/轻度损伤/中度损伤/重度损伤）");
        sb.AppendLine("- isPregnant: 是否怀孕");
        sb.AppendLine("- isLactating: 是否哺乳");
        sb.AppendLine("- hasAllergy: 是否有过敏史");
        sb.AppendLine();

        sb.AppendLine("### 5. 输出格式示例");
        sb.AppendLine("""
                      {
                        "calculatorName": "阿莫西林成人剂量计算器",
                        "description": "用于计算阿莫西林在成人患者中的推荐剂量",
                        "parameters": [
                          {
                            "name": "weight",
                            "displayName": "体重",
                            "dataType": "number",
                            "unit": "kg",
                            "isRequired": true,
                            "defaultValue": 70,
                            "minValue": 30,
                            "maxValue": 200,
                            "description": "患者体重（千克）"
                          },
                          {
                            "name": "age",
                            "displayName": "年龄",
                            "dataType": "number",
                            "unit": "岁",
                            "isRequired": true,
                            "defaultValue": 35,
                            "minValue": 18,
                            "maxValue": 100,
                            "description": "患者年龄（岁）"
                          },
                          {
                            "name": "severity",
                            "displayName": "感染严重程度",
                            "dataType": "select",
                            "unit": "",
                            "isRequired": true,
                            "defaultValue": "中度",
                            "options": ["轻度", "中度", "重度"],
                            "description": "根据感染严重程度选择"
                          }
                        ],
                        "calculationCode": "// 获取参数\nweight = parseFloat(weight) || 0;\nage = parseFloat(age) || 0;\nseverity = severity || '中度';\n\n// 输入验证\nif (weight < 30 || weight > 200) {\n    addWarning('体重范围', 0, 'mg', '', '体重应在30-200kg之间');\n    return;\n}\n\n// 基础剂量计算\nvar baseDose = weight * 25; // 25mg/kg/日\n\n// 严重程度调整\nif (severity === '重度') {\n    baseDose *= 1.5;\n} else if (severity === '轻度') {\n    baseDose *= 0.8;\n}\n\n// 年龄调整\nif (age > 65) {\n    baseDose *= 0.85;\n}\n\n// 计算单次剂量\nvar singleDose = round(baseDose / 3, 1);\n\n// 输出结果\naddNormalResult('推荐剂量', singleDose, 'mg', '每日3次', '7-10天', '餐后服用');"
                      }
                      """);

        sb.AppendLine();
        sb.AppendLine("请根据药物的具体特性和临床使用情况，生成合适的参数和计算逻辑。");
        sb.AppendLine("确保代码符合临床实际，包含必要的安全检查。");
        sb.AppendLine("只输出JSON格式的配置，不要包含其他说明文字。");

        return sb.ToString();
    }

    /// <summary>
    /// 调用Deepseek API
    /// </summary>
    private async Task<string> CallDeepSeekApiAsync(string prompt)
    {
        var request = new
        {
            model = "deepseek-reasoner",
            messages = new[]
            {
                new { role = "system", content = "你是一个专业的药物计算器开发专家，精通药物剂量计算和JavaScript编程。你需要根据药物信息生成完整的剂量计算器配置。" },
                new { role = "user", content = prompt }
            },
            max_tokens = 8192,
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {ApiKey}");

        var response = await _httpClient.PostAsync(ApiEndpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Deepseek API调用失败: {response.StatusCode} - {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var apiResponse = JsonSerializer.Deserialize<DeepseekResponse>(responseContent);

        return apiResponse?.Choices?.FirstOrDefault()?.Message?.Content ??
               throw new Exception("Deepseek API返回空响应");
    }

    /// <summary>
    /// 解析AI响应
    /// </summary>
    private DosageCalculatorGenerationResult ParseAiResponse(string aiResponse, BaseDrugInfo drugInfo)
    {
        try
        {
            // 由于使用了response_format，返回的应该直接是JSON格式
            var config = JsonSerializer.Deserialize<CalculatorConfig>(aiResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new Exception("无法解析AI响应");

            // 创建计算器对象
            var calculator = new DosageCalculator
            {
                DrugIdentifier = DosageCalculator.GenerateDrugIdentifier(drugInfo.DataSource, drugInfo.Id),
                DataSource = drugInfo.DataSource,
                OriginalDrugId = drugInfo.Id,
                DrugName = drugInfo.DrugName,
                CalculatorName = config.CalculatorName,
                Description = config.Description,
                CalculationCode = config.CalculationCode,
                ParameterDefinitions = JsonSerializer.Serialize(config.Parameters),
                CreatedBy = "taigongzhaihua",
                CreatedAt = DateTime.Now, // 使用本地时间
                UpdatedAt = DateTime.Now, // 使用本地时间
                IsActive = true
            };

            return new DosageCalculatorGenerationResult
            {
                Success = true,
                Calculator = calculator,
                Parameters = config.Parameters
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "解析AI响应失败: {Response}", aiResponse);
            return new DosageCalculatorGenerationResult
            {
                Success = false,
                ErrorMessage = $"解析AI响应失败: {ex.Message}",
                RawResponse = aiResponse
            };
        }
    }
}

/// <summary>
/// 计算器配置（移除category）
/// </summary>
public class CalculatorConfig
{
    public string CalculatorName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<DosageParameter> Parameters { get; set; } = [];
    public string CalculationCode { get; set; } = string.Empty;
}

/// <summary>
/// Deepseek API响应
/// </summary>
public class DeepseekResponse
{
    public List<DeepseekChoice>? Choices { get; set; }
}

public class DeepseekChoice
{
    public DeepseekMessage? Message { get; set; }
}

public class DeepseekMessage
{
    public string? Content { get; set; }
}

/// <summary>
/// 计算器生成结果
/// </summary>
public class DosageCalculatorGenerationResult
{
    public bool Success { get; set; }
    public DosageCalculator? Calculator { get; set; }
    public List<DosageParameter> Parameters { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public string? RawResponse { get; set; }
}