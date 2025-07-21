using DrugSearcher.Models;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DrugSearcher.Services;

/// <summary>
/// AI药物计算器生成服务
/// </summary>
public class DosageCalculatorAiService
{
    private readonly ILogger<DosageCalculatorAiService> _logger;
    private readonly HttpClient _httpClient;

    public DosageCalculatorAiService(ILogger<DosageCalculatorAiService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        httpClient.Timeout = TimeSpan.FromMinutes(10);
    }

    private const string ApiKey = "sk-dfb77b5548ee436598c26e39c2c75432";
    private const string ApiEndpoint = "https://api.deepseek.com/v1/chat/completions";

    /// <summary>
    /// 根据药物信息生成计算器（流式版本，包含思维链）
    /// </summary>
    public async IAsyncEnumerable<DosageCalculatorGenerationProgress> GenerateCalculatorStreamAsync(
        BaseDrugInfo drugInfo,
        string calculatorType = "通用剂量计算器",
        string additionalRequirements = "",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始为药物 {DrugName} 生成计算器（流式）", drugInfo.DrugName);

        yield return new DosageCalculatorGenerationProgress
        {
            Status = GenerationStatus.Preparing,
            Message = "正在准备生成计算器...",
            Progress = 0
        };

        var prompt = BuildPrompt(drugInfo, calculatorType, additionalRequirements);

        yield return new DosageCalculatorGenerationProgress
        {
            Status = GenerationStatus.Generating,
            Message = "正在调用AI生成计算器配置...",
            Progress = 10
        };

        var completeResponse = new StringBuilder();
        var reasoningContent = new StringBuilder();
        var startTime = DateTime.Now;
        var chunkCount = 0;
        const int totalEstimatedChunks = 8000; // 估计的chunk数量

        await foreach (var chunk in CallDeepSeekApiStreamAsync(prompt, cancellationToken).ConfigureAwait(false))
        {
            chunkCount++;

            // 处理推理内容
            if (!string.IsNullOrEmpty(chunk.ReasoningContent))
            {
                reasoningContent.Append(chunk.ReasoningContent);
            }

            // 处理响应内容
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                completeResponse.Append(chunk.Content);
            }

            var progress = Math.Min(10 + (chunkCount * 80 / totalEstimatedChunks), 90);

            yield return new DosageCalculatorGenerationProgress
            {
                Status = GenerationStatus.Generating,
                Message = !string.IsNullOrEmpty(chunk.ReasoningContent) ? "AI正在思考..." : "正在生成计算器配置...",
                Progress = progress,
                PartialContent = completeResponse.ToString(),
                StreamChunk = chunk.Content,
                ReasoningContent = reasoningContent.ToString(),
                ElapsedTime = DateTime.Now - startTime
            };
        }

        yield return new DosageCalculatorGenerationProgress
        {
            Status = GenerationStatus.Parsing,
            Message = "正在解析生成的配置...",
            Progress = 95,
            ReasoningContent = reasoningContent.ToString()
        };

        var result = ParseAiResponse(completeResponse.ToString(), drugInfo);

        yield return new DosageCalculatorGenerationProgress
        {
            Status = result.Success ? GenerationStatus.Completed : GenerationStatus.Failed,
            Message = result.Success ? "计算器生成成功！" : $"生成失败: {result.ErrorMessage}",
            Progress = 100,
            Result = result,
            ReasoningContent = reasoningContent.ToString(),
            ElapsedTime = DateTime.Now - startTime
        };

        _logger.LogInformation("成功为药物 {DrugName} 生成计算器", drugInfo.DrugName);
    }

    /// <summary>
    /// 调用Deepseek API（流式版本，支持推理模型）
    /// </summary>
    private async IAsyncEnumerable<DeepseekStreamChunk> CallDeepSeekApiStreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new
        {
            model = "deepseek-reasoner",
            messages = new[]
            {
                new {
                    role = "system",
                    content = "你是一个专业的药物计算器开发专家，精通药物剂量计算和JavaScript编程。" +
                             "你需要根据药物信息生成完整的剂量计算器配置。"
                },
                new { role = "user", content = prompt }
            },
            max_tokens = 8192,
            temperature = 0.2,
            response_format = new { type = "json_object" },
            stream = true
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, ApiEndpoint);
        requestMessage.Content = content;
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var response = await _httpClient
            .SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"Deepseek API调用失败: {response.StatusCode} - {errorContent}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        var buffer = new char[1024];
        var lineBuilder = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var charsRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

            if (charsRead == 0)
                break;

            for (var i = 0; i < charsRead; i++)
            {
                if (buffer[i] == '\n')
                {
                    var line = lineBuilder.ToString();
                    lineBuilder.Clear();

                    if (!line.StartsWith("data: "))
                        continue;

                    var data = line[6..];

                    if (data == "[DONE]")
                        yield break;

                    if (!TryDeserializeStreamResponse(data, out var streamResponse)) continue;

                    var choice = streamResponse?.Choices?.FirstOrDefault();
                    if (choice == null) continue;

                    var chunk = new DeepseekStreamChunk();

                    // 处理响应内容
                    if (!string.IsNullOrEmpty(choice.Delta?.Content))
                    {
                        chunk.Content = choice.Delta.Content;
                    }

                    // 处理推理内容
                    if (!string.IsNullOrEmpty(choice.Delta?.ReasoningContent))
                    {
                        chunk.ReasoningContent = choice.Delta.ReasoningContent;
                    }

                    if (!string.IsNullOrEmpty(chunk.Content) || !string.IsNullOrEmpty(chunk.ReasoningContent))
                    {
                        yield return chunk;
                    }
                }
                else if (buffer[i] != '\r')
                {
                    lineBuilder.Append(buffer[i]);
                }
            }
        }

        // 处理最后一行（如果有）
        if (lineBuilder.Length <= 0) yield break;
        {
            var lastLine = lineBuilder.ToString();
            if (!lastLine.StartsWith("data: ") || lastLine.Length <= 6) yield break;
            var data = lastLine[6..];
            if (data == "[DONE]" || !TryDeserializeStreamResponse(data, out var streamResponse)) yield break;
            var choice = streamResponse?.Choices?.FirstOrDefault();
            if (choice == null) yield break;
            var chunk = new DeepseekStreamChunk();

            if (!string.IsNullOrEmpty(choice.Delta?.Content))
            {
                chunk.Content = choice.Delta.Content;
            }

            if (!string.IsNullOrEmpty(choice.Delta?.ReasoningContent))
            {
                chunk.ReasoningContent = choice.Delta.ReasoningContent;
            }

            if (!string.IsNullOrEmpty(chunk.Content) || !string.IsNullOrEmpty(chunk.ReasoningContent))
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// 安全地尝试反序列化流响应
    /// </summary>
    private static bool TryDeserializeStreamResponse(string data, out DeepseekStreamingResponse? response)
    {
        response = null;

        if (string.IsNullOrWhiteSpace(data))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(data);

            response = new DeepseekStreamingResponse();

            if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array) return false;

            response.Choices = [];

            foreach (var choice in choices.EnumerateArray())
            {
                var streamingChoice = new StreamingChoice();

                if (choice.TryGetProperty("index", out var index))
                    streamingChoice.Index = index.GetInt32();

                if (choice.TryGetProperty("delta", out var delta))
                {
                    streamingChoice.Delta = new StreamingDelta();

                    if (delta.TryGetProperty("content", out var content))
                        streamingChoice.Delta.Content = content.GetString();

                    if (delta.TryGetProperty("role", out var role))
                        streamingChoice.Delta.Role = role.GetString();

                    // 处理推理内容
                    if (delta.TryGetProperty("reasoning_content", out var reasoningContent))
                        streamingChoice.Delta.ReasoningContent = reasoningContent.GetString();
                }

                response.Choices.Add(streamingChoice);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 构建AI提示词
    /// </summary>
    private static string BuildPrompt(BaseDrugInfo drugInfo, string calculatorType, string additionalRequirements)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# 药物计算器生成任务");
        sb.AppendLine();
        sb.AppendLine("请根据以下药物信息生成一个完整的剂量计算器配置。");
        sb.AppendLine();
        sb.AppendLine("## 思考步骤：");
        sb.AppendLine("1. 分析药物的主要特性和适应症");
        sb.AppendLine("2. 确定计算所需的关键参数（如年龄、体重、病情等）");
        sb.AppendLine("3. 根据药物说明书设计剂量计算逻辑");
        sb.AppendLine("4. 考虑特殊人群（儿童、老人、肝肾功能不全等）的剂量调整");
        sb.AppendLine("5. 添加必要的安全限制和警告");
        sb.AppendLine();

        // 药物基本信息
        sb.AppendLine("## 药物信息:");
        sb.AppendLine($"- 药品名称: {drugInfo.DrugName}");
        sb.AppendLine($"- 规格: {drugInfo.Specification}");
        sb.AppendLine($"- 生产厂家: {drugInfo.Manufacturer}");
        sb.AppendLine($"- 批准文号: {drugInfo.ApprovalNumber}");

        switch (drugInfo)
        {
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
        sb.AppendLine("- 注：dose 参数必须为数字类型，否则会直接转换成 0");
        sb.AppendLine("- 使用 round(value, decimals) 四舍五入");
        sb.AppendLine("- 使用 clamp(value, min, max) 限制数值范围");
        sb.AppendLine("- 包含详细的输入验证");
        sb.AppendLine("- 包含年龄、体重相关的剂量调整");
        sb.AppendLine("- 包含安全上限检查");
        sb.AppendLine("- 若有多种用药方案，可以全部列举出来");
        sb.AppendLine("- 注意年龄单位和剂量单位的转换");
        sb.AppendLine("- 注意：如无特别声明，医学上儿童是指12岁以下年龄段");
        sb.AppendLine();

        sb.AppendLine("### 4. 输出格式示例");
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
                        "calculationCode": "// 推理过程已在AI思考中完成\n// 以下是基于药物说明书的剂量计算实现\n\n// 输入验证\nif (weight < 30 || weight > 200) {\n    addWarning('体重范围', 0, 'mg', '', '体重应在30-200kg之间');\n    return;\n}\n\n// 基础剂量计算\nvar baseDose = weight * 25; // 25mg/kg/日\n\n// 严重程度调整\nif (severity === '重度') {\n    baseDose *= 1.5;\n} else if (severity === '轻度') {\n    baseDose *= 0.8;\n}\n\n// 计算单次剂量\nvar singleDose = round(baseDose / 3, 1);\n\n// 输出结果\naddNormalResult('推荐剂量', singleDose, 'mg', '每日3次', '7-10天', '餐后服用');"
                      }
                      """);

        sb.AppendLine();
        sb.AppendLine("请根据药物的具体特性和临床使用情况，生成合适的参数和计算逻辑。");
        sb.AppendLine("确保代码符合临床实际，包含必要的安全检查。");

        return sb.ToString();
    }

    private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private DosageCalculatorGenerationResult ParseAiResponse(string aiResponse, BaseDrugInfo drugInfo)
    {
        try
        {
            var config = JsonSerializer.Deserialize<CalculatorConfig>(aiResponse, CachedJsonSerializerOptions)
                         ?? throw new Exception("无法解析AI响应");

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
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
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
/// Deepseek流式响应块
/// </summary>
public class DeepseekStreamChunk
{
    public string? Content { get; set; }
    public string? ReasoningContent { get; set; }
}

/// <summary>
/// 计算器生成进度
/// </summary>
public class DosageCalculatorGenerationProgress
{
    public GenerationStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? PartialContent { get; set; }
    public string? StreamChunk { get; set; }
    public string? ReasoningContent { get; set; }
    public TimeSpan? ElapsedTime { get; set; }
    public DosageCalculatorGenerationResult? Result { get; set; }
}

/// <summary>
/// 生成状态枚举
/// </summary>
public enum GenerationStatus
{
    Preparing,
    Generating,
    Parsing,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Deepseek 流式响应模型（支持推理内容）
/// </summary>
public class DeepseekStreamingResponse
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("object")] public string? Object { get; set; }
    [JsonPropertyName("created")] public long Created { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("choices")] public List<StreamingChoice>? Choices { get; set; }
}

public class StreamingChoice
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("delta")] public StreamingDelta? Delta { get; set; }
    [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
}

public class StreamingDelta
{
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("reasoning_content")] public string? ReasoningContent { get; set; }
}

/// <summary>
/// 计算器配置
/// </summary>
public class CalculatorConfig
{
    public string CalculatorName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<DosageParameter> Parameters { get; set; } = [];
    public string CalculationCode { get; set; } = string.Empty;
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