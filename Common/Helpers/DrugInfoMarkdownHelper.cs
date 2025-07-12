using DrugSearcher.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace DrugSearcher.Helpers;

/// <summary>
/// 药物信息 Markdown 格式化工具类
/// </summary>
public static partial class DrugInfoMarkdownHelper
{
    /// <summary>
    /// 将药物信息转换为 Markdown 格式的字典
    /// </summary>
    public static Dictionary<string, string> ConvertToMarkdownDictionary(BaseDrugInfo drugInfo)
    {
        var markdownDict = new Dictionary<string, string>();
        InitDictonary(markdownDict);

        if (drugInfo is LocalDrugInfo localDrug)
        {
            ConvertLocalDrugInfo(localDrug, markdownDict);
        }
        else if (drugInfo is OnlineDrugInfo onlineDrug)
        {
            ConvertOnlineDrugInfo(onlineDrug, markdownDict);
        }

        // 生成全部详情
        GenerateFullDetails(markdownDict);

        return markdownDict;
    }

    private static void InitDictonary(Dictionary<string, string> dict)
    {
        var list = new List<string>
        {
            "MainIngredients", "Appearance", "DrugDescription", "Indications", "Dosage",
            "SideEffects", "Precautions", "Contraindications", "PregnancyAndLactation",
            "PediatricUse", "GeriatricUse", "DrugInteractions", "Pharmacology",
            "Pharmacokinetics", "Storage", "TcmRemarks", "FullDetails"
        };
        foreach (var key in list)
        {
            if (!dict.ContainsKey(key))
            {
                dict[key] = string.Empty;
            }
        }
    }

    /// <summary>
    /// 转换本地药物信息
    /// </summary>
    private static void ConvertLocalDrugInfo(LocalDrugInfo localDrug, Dictionary<string, string> markdownDict)
    {
        // 药物说明
        if (!string.IsNullOrEmpty(localDrug.Description))
        {
            markdownDict["DrugDescription"] = FormatTextContent(localDrug.Description);
        }

        // 适应症
        if (!string.IsNullOrEmpty(localDrug.Indications))
        {
            markdownDict["Indications"] = FormatTextContent(localDrug.Indications);
        }

        // 用法用量
        if (!string.IsNullOrEmpty(localDrug.Dosage))
        {
            markdownDict["Dosage"] = FormatDosageContent(localDrug.Dosage);
        }

        // 不良反应
        if (!string.IsNullOrEmpty(localDrug.AdverseReactions))
        {
            markdownDict["SideEffects"] = FormatTextContent(localDrug.AdverseReactions);
        }

        // 注意事项
        if (!string.IsNullOrEmpty(localDrug.Precautions))
        {
            markdownDict["Precautions"] = FormatPrecautionsContent(localDrug.Precautions);
        }

        // 备注（中医信息会包含在这里）
        if (!string.IsNullOrEmpty(localDrug.Remarks))
        {
            markdownDict["TcmRemarks"] = FormatTextContent(localDrug.Remarks);
        }
    }

    /// <summary>
    /// 转换在线药物信息
    /// </summary>
    private static void ConvertOnlineDrugInfo(OnlineDrugInfo onlineDrug, Dictionary<string, string> markdownDict)
    {
        // 主要成分
        if (!string.IsNullOrEmpty(onlineDrug.MainIngredients))
        {
            markdownDict["MainIngredients"] = FormatIngredientsContent(onlineDrug.MainIngredients);
        }

        // 性状
        if (!string.IsNullOrEmpty(onlineDrug.Appearance))
        {
            markdownDict["Appearance"] = FormatTextContent(onlineDrug.Appearance);
        }

        // 适应症
        if (!string.IsNullOrEmpty(onlineDrug.Indications))
        {
            markdownDict["Indications"] = FormatTextContent(onlineDrug.Indications);
        }

        // 用法用量
        if (!string.IsNullOrEmpty(onlineDrug.Dosage))
        {
            markdownDict["Dosage"] = FormatDosageContent(onlineDrug.Dosage);
        }

        // 不良反应
        if (!string.IsNullOrEmpty(onlineDrug.AdverseReactions))
        {
            markdownDict["SideEffects"] = FormatTextContent(onlineDrug.AdverseReactions);
        }

        // 注意事项
        if (!string.IsNullOrEmpty(onlineDrug.Precautions))
        {
            markdownDict["Precautions"] = FormatPrecautionsContent(onlineDrug.Precautions);
        }

        // 禁忌
        if (!string.IsNullOrEmpty(onlineDrug.Contraindications))
        {
            markdownDict["Contraindications"] = FormatWarningContent(onlineDrug.Contraindications);
        }

        // 孕妇及哺乳期妇女用药
        if (!string.IsNullOrEmpty(onlineDrug.PregnancyAndLactation))
        {
            markdownDict["PregnancyAndLactation"] = FormatWarningContent(onlineDrug.PregnancyAndLactation);
        }

        // 儿童用药
        if (!string.IsNullOrEmpty(onlineDrug.PediatricUse))
        {
            markdownDict["PediatricUse"] = FormatTextContent(onlineDrug.PediatricUse);
        }

        // 老人用药
        if (!string.IsNullOrEmpty(onlineDrug.GeriatricUse))
        {
            markdownDict["GeriatricUse"] = FormatTextContent(onlineDrug.GeriatricUse);
        }

        // 药物相互作用
        if (!string.IsNullOrEmpty(onlineDrug.DrugInteractions))
        {
            markdownDict["DrugInteractions"] = FormatInteractionsContent(onlineDrug.DrugInteractions);
        }

        // 药理毒理
        if (!string.IsNullOrEmpty(onlineDrug.PharmacologyToxicology))
        {
            markdownDict["Pharmacology"] = FormatTextContent(onlineDrug.PharmacologyToxicology);
        }

        // 药代动力学
        if (!string.IsNullOrEmpty(onlineDrug.Pharmacokinetics))
        {
            markdownDict["Pharmacokinetics"] = FormatTextContent(onlineDrug.Pharmacokinetics);
        }

        // 储存信息
        if (!string.IsNullOrEmpty(onlineDrug.Storage) || !string.IsNullOrEmpty(onlineDrug.ShelfLife))
        {
            markdownDict["Storage"] = FormatStorageContent(onlineDrug.Storage, onlineDrug.ShelfLife);
        }
    }

    /// <summary>
    /// 生成全部详情 Markdown
    /// </summary>
    private static void GenerateFullDetails(Dictionary<string, string> markdownDict)
    {
        var sb = new StringBuilder();

        // 定义显示顺序和标题
        var sectionOrder = new List<(string Key, string Title, string Icon)>
        {
            ("MainIngredients", "主要成分", "🧪"),
            ("Appearance", "性状", "👁️"),
            ("DrugDescription", "药物说明", "📋"),
            ("Indications", "适应症", "🎯"),
            ("Dosage", "用法用量", "💊"),
            ("SideEffects", "不良反应", "⚠️"),
            ("Precautions", "注意事项", "⚡"),
            ("Contraindications", "禁忌", "🚫"),
            ("PregnancyAndLactation", "孕妇及哺乳期妇女用药", "🤱"),
            ("PediatricUse", "儿童用药", "👶"),
            ("GeriatricUse", "老人用药", "👴"),
            ("DrugInteractions", "药物相互作用", "🔄"),
            ("Pharmacology", "药理毒理", "🔬"),
            ("Pharmacokinetics", "药代动力学", "📈"),
            ("Storage", "储存信息", "📦"),
            ("TcmRemarks", "中医备注", "🏥")
        };

        foreach (var (key, title, icon) in sectionOrder)
        {
            if (markdownDict.TryGetValue(key, out var content) && !string.IsNullOrWhiteSpace(content))
            {
                sb.AppendLine($"## {icon} {title}");
                sb.AppendLine();
                sb.AppendLine(content);
                sb.AppendLine();
            }
        }

        // 移除最后的分隔线
        var fullContent = sb.ToString().TrimEnd();
        if (fullContent.EndsWith("---"))
        {
            fullContent = fullContent.Substring(0, fullContent.Length - 3).TrimEnd();
        }

        markdownDict["FullDetails"] = fullContent;
    }

    #region 格式化方法

    /// <summary>
    /// 预处理文本内容 - 统一的文本清理和格式化
    /// </summary>
    private static string PreprocessText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        // 统一换行符
        var text = content.Replace("\r\n", "\n").Replace("\r", "\n");


        // 处理数字列表：将 |数字. 或 数字. 开头的内容转为新行
        text = NumberRegex().Replace(text, "\n$1 ");// 处理数字列表
        text = Number2Regex().Replace(text, "\n\t+ ");// 处理括号内的数字列表
        text = Number3Regex().Replace(text, "\n\t\t- ");// 处理括号内的数字列表

        // 处理多余的空白字符
        text = SpaceLineRegex().Replace(text, "\n").Replace("。|", "。\n"); // 移除多余的空行

        return text.Trim();
    }

    /// <summary>
    /// 将预处理后的文本转换为 Markdown 列表格式
    /// </summary>
    private static string ConvertToMarkdownList(string preprocessedText)
    {
        if (string.IsNullOrWhiteSpace(preprocessedText))
            return string.Empty;

        var lines = preprocessedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrEmpty(trimmedLine))
            {
                // 如果行以数字开头，格式化为列表
                if (Regex.IsMatch(trimmedLine, @"^\d+[\.\)、]"))
                {
                    var formattedLine = Regex.Replace(trimmedLine, @"^(\d+)[\.\)、]\s*", "$1. ");
                    sb.AppendLine(formattedLine);
                }
                // 如果行以符号开头，保持原样
                else if (trimmedLine.StartsWith("-") || trimmedLine.StartsWith("•") || trimmedLine.StartsWith("*"))
                {
                    sb.AppendLine(trimmedLine);
                }
                else
                {
                    sb.AppendLine(trimmedLine);
                }
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// 格式化普通文本内容
    /// </summary>
    private static string FormatTextContent(string content)
    {
        var preprocessedText = PreprocessText(content);
        return ConvertToMarkdownList(preprocessedText);
    }

    /// <summary>
    /// 格式化用法用量内容
    /// </summary>
    private static string FormatDosageContent(string dosage)
    {
        if (string.IsNullOrWhiteSpace(dosage))
            return string.Empty;

        var preprocessedText = PreprocessText(dosage).Replace("。", "。\n").Replace("；", "；\n").Replace(";", "；\n");

        var lines = preprocessedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrEmpty(trimmedLine))
            {
                // 处理数字列表
                if (Regex.IsMatch(trimmedLine, @"^\d+[\.\)、]"))
                {
                    var formattedLine = Regex.Replace(trimmedLine, @"^\(?(\d+)[\.\)、]\s*", "$1. ");
                    sb.AppendLine($"> {formattedLine}");
                }
                else
                {
                    sb.AppendLine($"> - {trimmedLine}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 格式化注意事项内容
    /// </summary>
    private static string FormatPrecautionsContent(string precautions)
    {
        if (string.IsNullOrWhiteSpace(precautions))
            return string.Empty;

        var preprocessedText = Regex.Replace(PreprocessText(precautions), @"(。)\s*(\d+)[\.\)、]\s*", "$1\n$2. ");
        var sb = new StringBuilder();
        sb.AppendLine("> ⚡ **注意**");
        sb.AppendLine("> ---");

        var lines = preprocessedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrEmpty(trimmedLine))
            {
                // 处理数字列表
                if (Regex.IsMatch(trimmedLine, @"^\d+[\.\)、]"))
                {
                    var formattedLine = Regex.Replace(trimmedLine, @"^(\d+)[\.\)、]\s*", "$1. ");
                    sb.AppendLine($"> {formattedLine}");
                }
                else
                {
                    sb.AppendLine($"> {trimmedLine}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 格式化警告内容（禁忌、孕妇用药等）
    /// </summary>
    private static string FormatWarningContent(string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
            return string.Empty;

        var preprocessedText = PreprocessText(warning);
        var sb = new StringBuilder();
        sb.AppendLine("> 🚨 **重要警告**");
        sb.AppendLine("> ---");

        var lines = preprocessedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (!string.IsNullOrEmpty(trimmedLine))
            {
                // 处理数字列表
                if (Regex.IsMatch(trimmedLine, @"^\d+[\.\)、]"))
                {
                    var formattedLine = Regex.Replace(trimmedLine, @"(\d+)[\.\)、]\s*", "$1. ");
                    sb.AppendLine($"> {formattedLine}");
                }
                else
                {
                    sb.AppendLine($"> - {trimmedLine}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 格式化主要成分内容
    /// </summary>
    private static string FormatIngredientsContent(string ingredients)
    {
        if (string.IsNullOrWhiteSpace(ingredients))
            return string.Empty;

        var preprocessedText = PreprocessText(ingredients);
        var lines = preprocessedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        // 检查是否可以构造表格
        var hasColonSeparator = lines.Any(line => line.Contains(':') || line.Contains('：'));

        if (hasColonSeparator && lines.Length > 1)
        {
            // 构造表格格式

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (!string.IsNullOrEmpty(trimmedLine))
                {
                    sb.AppendLine(trimmedLine);
                }
            }
        }
        else
        {
            // 使用列表格式
            sb.Append(ConvertToMarkdownList(preprocessedText));
        }

        return sb.ToString();
    }

    /// <summary>
    /// 格式化药物相互作用内容
    /// </summary>
    private static string FormatInteractionsContent(string interactions)
    {
        if (string.IsNullOrWhiteSpace(interactions))
            return string.Empty;

        var preprocessedText = PreprocessText(interactions);
        var lines = preprocessedText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;
            // 处理数字列表
            if (Regex.IsMatch(trimmedLine, @"^\d+[\.\)、]"))
            {
                var formattedLine = Regex.Replace(trimmedLine, @"^(\d+)[\.\)、]\s*", "");

                if (formattedLine.Contains("禁止") || formattedLine.Contains("避免") || formattedLine.Contains("不可"))
                {
                    sb.AppendLine($"- 🚫 **{formattedLine}**");
                }
                else if (formattedLine.Contains("注意") || formattedLine.Contains("小心"))
                {
                    sb.AppendLine($"- ⚠️ {formattedLine}");
                }
                else
                {
                    sb.AppendLine($"- {formattedLine}");
                }
            }
            else
            {
                if (trimmedLine.Contains("禁止") || trimmedLine.Contains("避免") || trimmedLine.Contains("不可"))
                {
                    sb.AppendLine($"- 🚫 **{trimmedLine}**");
                }
                else if (trimmedLine.Contains("注意") || trimmedLine.Contains("小心"))
                {
                    sb.AppendLine($"- ⚠️ {trimmedLine}");
                }
                else
                {
                    sb.AppendLine($"- {trimmedLine}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 格式化储存信息内容
    /// </summary>
    private static string FormatStorageContent(string? storage, string? shelfLife)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(storage))
        {
            var preprocessedStorage = PreprocessText(storage);
            sb.AppendLine("### 📦 储存条件");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(preprocessedStorage);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        if (string.IsNullOrEmpty(shelfLife)) return sb.ToString().Trim();
        var preprocessedShelfLife = PreprocessText(shelfLife);
        sb.AppendLine("### ⏰ 有效期");
        sb.AppendLine();
        sb.AppendLine($"**{preprocessedShelfLife}**");

        return sb.ToString().Trim();
    }

    [GeneratedRegex(@"(?:[。；;]\s*|\s+|^)[\t\s\|]*(\d+[\.、])(?<!\d)\s*")]
    private static partial Regex NumberRegex();
    [GeneratedRegex(@"\|?\((\d+)\)")]
    private static partial Regex Number2Regex();
    [GeneratedRegex(@"\|?[①②③④⑤⑥⑦⑧⑨⑩⑾⑿⒀⒁⒂⒃⒄⒅⒆⒇㈠㈡㈢㈣㈤㈥㈦㈧㈨㈩⒈⒉⒊⒋⒌⒍⒎⒏⒐⒑⒒⒓⒔⒕⒖⒗⒘⒙⒚⒛⑴⑵⑶⑷⑸⑹⑺⑻⑼⑽]")]
    private static partial Regex Number3Regex();
    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex SpaceLineRegex();

    #endregion
}