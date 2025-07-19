using Color = System.Windows.Media.Color;

namespace DrugSearcher.Services;

/// <summary>
/// JavaScript语言定义中心
/// </summary>
public static class JavaScriptLanguageDefinition
{
    /// <summary>
    /// 关键字定义
    /// </summary>
    public static class Keywords
    {
        public static readonly HashSet<string> ControlFlow = ["if", "else", "switch", "case", "default"];

        public static readonly HashSet<string> Loops = ["for", "while", "do", "break", "continue"];

        public static readonly HashSet<string> Declarations = ["var", "let", "const", "function", "class"];

        public static readonly HashSet<string> Exceptions = ["try", "catch", "finally", "throw"];

        public static readonly HashSet<string> Values = ["null", "undefined", "true", "false"];

        public static readonly HashSet<string> Operators =
            ["new", "delete", "typeof", "instanceof", "in", "of", "void"];

        public static readonly HashSet<string> Others =
        [
            "return", "this", "super", "extends", "static", "async", "await",
            "yield", "debugger", "with", "import", "export"
        ];

        // 所有关键字
        public static readonly HashSet<string?> All =
        [

            ..ControlFlow.Concat(Loops).Concat(Declarations).Concat(Exceptions)
                .Concat(Values).Concat(Operators).Concat(Others)

        ];
    }

    /// <summary>
    /// 操作符定义
    /// </summary>
    public static class Operators
    {
        // 算术操作符
        public static readonly HashSet<string> Arithmetic = ["+", "-", "*", "/", "%", "**", "++", "--"];

        // 赋值操作符
        public static readonly HashSet<string> Assignment = ["=", "+=", "-=", "*=", "/=", "%=", "**=",
            "<<=", ">>=", ">>>=", "&=", "^=", "|="];

        // 比较操作符
        public static readonly HashSet<string> Comparison = ["==", "!=", "===", "!==", ">", ">=", "<", "<="];

        // 逻辑操作符
        public static readonly HashSet<string> Logical = ["&&", "||", "!", "??"];

        // 位操作符
        public static readonly HashSet<string> Bitwise = ["&", "|", "^", "~", "<<", ">>", ">>>"];

        // 其他操作符
        public static readonly HashSet<string> Other = ["?", ":", ",", ".", "?."];

        // 所有操作符字符（用于语法高亮）
        public static readonly string AllCharacters = @"[+\-*/%=!<>&|^~?:;,.\[\](){}]";

        // 所有操作符
        public static readonly HashSet<string> All = [
            ..Arithmetic.Concat(Assignment).Concat(Comparison)
                .Concat(Logical).Concat(Bitwise).Concat(Other)
        ];
    }

    /// <summary>
    /// 内置对象和函数
    /// </summary>
    public static class BuiltIns
    {
        public static readonly Dictionary<string, string> GlobalFunctions = new()
        {
            // 数值相关
            { "parseInt", "解析字符串并返回整数" },
            { "parseFloat", "解析字符串并返回浮点数" },
            { "Number", "将值转换为数字" },
            { "isNaN", "检查值是否为 NaN" },
            { "isFinite", "检查值是否为有限数" },
        
            // 字符串相关
            { "String", "将值转换为字符串" },
            { "encodeURI", "编码 URI" },
            { "encodeURIComponent", "编码 URI 组件" },
            { "decodeURI", "解码 URI" },
            { "decodeURIComponent", "解码 URI 组件" },
            { "escape", "编码字符串（已废弃）" },
            { "unescape", "解码字符串（已废弃）" },
        
            // 其他
            { "eval", "执行 JavaScript 代码字符串" },
            { "Boolean", "将值转换为布尔值" },
            { "Array", "创建数组" },
            { "Object", "创建对象" },
            { "Function", "创建函数" },
            { "Symbol", "创建符号" },
        
            // 定时器
            { "setTimeout", "设置定时器" },
            { "clearTimeout", "清除定时器" },
            { "setInterval", "设置间隔定时器" },
            { "clearInterval", "清除间隔定时器" },
        
            // Promise 相关
            { "Promise", "创建 Promise" },
        
            // 控制台
            { "alert", "显示警告框" },
            { "confirm", "显示确认框" },
            { "prompt", "显示输入框" }
        };

        public static readonly Dictionary<string, string> GlobalObjects = new()
        {
            ["Object"] = "对象构造函数",
            ["Array"] = "数组构造函数",
            ["String"] = "字符串构造函数",
            ["Number"] = "数字构造函数",
            ["Boolean"] = "布尔值构造函数",
            ["Date"] = "日期构造函数",
            ["RegExp"] = "正则表达式构造函数",
            ["Error"] = "错误构造函数",
            ["Math"] = "数学工具对象",
            ["JSON"] = "JSON工具对象",
            ["console"] = "控制台对象"
        };

        public static readonly Dictionary<string, List<string?>> ObjectMethods = new()
        {
            ["Array"] =
            [
                "push", "pop", "shift", "unshift", "slice", "splice",
                "join", "reverse", "sort", "filter", "map", "forEach", "reduce",
                "find", "findIndex", "some", "every", "includes", "indexOf", "length"
            ],

            ["String"] =
            [
                "charAt", "charCodeAt", "concat", "indexOf",
                "lastIndexOf", "match", "replace", "search", "slice", "split",
                "substring", "toLowerCase", "toUpperCase", "trim", "length"
            ],

            ["Math"] =
            [
                "abs", "ceil", "floor", "round", "max", "min",
                "pow", "sqrt", "random", "sin", "cos", "tan", "log", "exp", "PI", "E"
            ]
        };

        // 在 BuiltIns 类中添加数组原型方法
        public static readonly Dictionary<string, string> ArrayPrototypeMethods = new()
        {
            ["includes"] = "判断数组是否包含某个元素",
            ["indexOf"] = "返回元素在数组中的索引",
            ["lastIndexOf"] = "返回元素在数组中最后一次出现的索引",
            ["push"] = "向数组末尾添加元素",
            ["pop"] = "删除并返回数组最后一个元素",
            ["shift"] = "删除并返回数组第一个元素",
            ["unshift"] = "向数组开头添加元素",
            ["slice"] = "返回数组的一部分",
            ["splice"] = "删除/替换/添加数组元素",
            ["join"] = "将数组元素连接成字符串",
            ["reverse"] = "反转数组",
            ["sort"] = "对数组排序",
            ["filter"] = "过滤数组元素",
            ["map"] = "映射数组元素",
            ["forEach"] = "遍历数组",
            ["reduce"] = "累积数组元素",
            ["reduceRight"] = "从右到左累积数组元素",
            ["find"] = "查找符合条件的第一个元素",
            ["findIndex"] = "查找符合条件的第一个元素的索引",
            ["some"] = "检查是否有元素符合条件",
            ["every"] = "检查是否所有元素都符合条件",
            ["concat"] = "连接数组",
            ["flat"] = "扁平化数组",
            ["flatMap"] = "映射并扁平化数组"
        };

        public static readonly Dictionary<string, string> Properties = new()
        {
            ["length"] = "长度属性",
            ["prototype"] = "原型属性",
            ["constructor"] = "构造函数",
            ["__proto__"] = "原型链"
        };
    }

    /// <summary>
    /// 自定义函数（项目特定）
    /// </summary>
    public static class CustomFunctions
    {
        public static readonly Dictionary<string, FunctionDefinition> Functions = new()
        {
            ["addResult"] = new FunctionDefinition
            {
                Name = "addResult",
                Signature = "addResult(description, dose, unit, frequency, duration, notes, isWarning, warningMessage)",
                Description = "添加计算结果",
                Parameters =
                [
                    new ParameterDefinition("description", "string", "结果描述"),
                    new ParameterDefinition("dose", "number", "剂量"),
                    new ParameterDefinition("unit", "string", "单位"),
                    new ParameterDefinition("frequency", "string", "频率"),
                    new ParameterDefinition("duration", "string", "持续时间"),
                    new ParameterDefinition("notes", "string", "备注"),
                    new ParameterDefinition("isWarning", "boolean", "是否为警告"),
                    new ParameterDefinition("warningMessage", "string", "警告信息")
                ]
            },
            ["addWarning"] = new FunctionDefinition
            {
                Name = "addWarning",
                Signature = "addWarning(description, dose, unit, frequency, warningMessage)",
                Description = "添加警告结果",
                Parameters =
                [
                    new ParameterDefinition("description", "string", "结果描述"),
                    new ParameterDefinition("dose", "number", "剂量"),
                    new ParameterDefinition("unit", "string", "单位"),
                    new ParameterDefinition("frequency", "string", "频率"),
                    new ParameterDefinition("warningMessage", "string", "警告信息")
                ]
            },
            ["addNormalResult"] = new FunctionDefinition
            {
                Name = "addNormalResult",
                Signature = "addNormalResult(description, dose, unit, frequency, duration, notes)",
                Description = "添加正常结果",
                Parameters =
                [
                    new ParameterDefinition("description", "string", "结果描述"),
                    new ParameterDefinition("dose", "number", "剂量"),
                    new ParameterDefinition("unit", "string", "单位"),
                    new ParameterDefinition("frequency", "string", "频率"),
                    new ParameterDefinition("duration", "string", "持续时间"),
                    new ParameterDefinition("notes", "string", "备注")
                ]
            },
            ["round"] = new FunctionDefinition
            {
                Name = "round",
                Signature = "round(value, decimals)",
                Description = "四舍五入到指定小数位",
                Parameters =
                [
                    new ParameterDefinition("value", "number", "要四舍五入的值"),
                    new ParameterDefinition("decimals", "number", "小数位数")
                ]
            },
            ["clamp"] = new FunctionDefinition
            {
                Name = "clamp",
                Signature = "clamp(value, min, max)",
                Description = "限制数值在指定范围内",
                Parameters =
                [
                    new ParameterDefinition("value", "number", "要限制的值"),
                    new ParameterDefinition("min", "number", "最小值"),
                    new ParameterDefinition("max", "number", "最大值")
                ]
            },
            ["isValidNumber"] = new FunctionDefinition
            {
                Name = "isValidNumber",
                Signature = "isValidNumber(value)",
                Description = "检查是否为有效数字",
                Parameters =
                [
                    new ParameterDefinition("value", "any", "要检查的值")
                ]
            },
            ["safeParseFloat"] = new FunctionDefinition
            {
                Name = "safeParseFloat",
                Signature = "safeParseFloat(value, defaultValue)",
                Description = "安全解析浮点数",
                Parameters =
                [
                    new ParameterDefinition("value", "any", "要解析的值"),
                    new ParameterDefinition("defaultValue", "number", "默认值")
                ]
            },
            ["safeParseInt"] = new FunctionDefinition
            {
                Name = "safeParseInt",
                Signature = "safeParseInt(value, defaultValue)",
                Description = "安全解析整数",
                Parameters =
                [
                    new ParameterDefinition("value", "any", "要解析的值"),
                    new ParameterDefinition("defaultValue", "number", "默认值")
                ]
            }
        };

        public static readonly HashSet<string?> Names = [.. Functions.Keys];
    }

    /// <summary>
    /// 全局变量
    /// </summary>
    public static class GlobalVariables
    {
        public static readonly Dictionary<string, string> Variables = new()
        {
            ["results"] = "结果数组"
        };
    }

    /// <summary>
    /// 语法高亮颜色定义
    /// </summary>
    public static class SyntaxColors
    {
        public static readonly Dictionary<string, Color> DarkTheme = new()
        {
            ["Comment"] = Color.FromRgb(106, 153, 85),      // #6A9955
            ["String"] = Color.FromRgb(206, 145, 120),      // #CE9178
            ["Number"] = Color.FromRgb(181, 206, 168),      // #B5CEA8
            ["Keyword"] = Color.FromRgb(86, 156, 214),      // #569CD6
            ["Operator"] = Color.FromRgb(212, 212, 212),    // #D4D4D4
            ["Variable"] = Color.FromRgb(156, 220, 254),    // #9CDCFE
            ["Function"] = Color.FromRgb(220, 220, 170),    // #DCDCAA
            ["BuiltInFunction"] = Color.FromRgb(78, 201, 176), // #4EC9B0
            ["CustomFunction"] = Color.FromRgb(220, 220, 170), // #DCDCAA
            ["Property"] = Color.FromRgb(156, 220, 254)     // #9CDCFE
        };

        public static readonly Dictionary<string, Color> LightTheme = new()
        {
            ["Comment"] = Color.FromRgb(0, 128, 0),         // Green
            ["String"] = Color.FromRgb(163, 21, 21),        // Dark Red
            ["Number"] = Color.FromRgb(9, 136, 90),         // Dark Green
            ["Keyword"] = Color.FromRgb(0, 0, 255),         // Blue
            ["Operator"] = Color.FromRgb(0, 0, 0),          // Black
            ["Variable"] = Color.FromRgb(0, 0, 0),          // Black
            ["Function"] = Color.FromRgb(121, 94, 38),      // Brown
            ["BuiltInFunction"] = Color.FromRgb(0, 128, 128), // Teal
            ["CustomFunction"] = Color.FromRgb(121, 94, 38),  // Brown
            ["Property"] = Color.FromRgb(0, 0, 0)           // Black
        };
    }

    /// <summary>
    /// 获取所有已知标识符
    /// </summary>
    public static HashSet<string?> GetAllKnownIdentifiers()
    {
        var identifiers = new HashSet<string?>();

        // 添加关键字
        identifiers.UnionWith(Keywords.All);

        // 添加内置函数和对象
        identifiers.UnionWith(BuiltIns.GlobalFunctions.Keys);
        identifiers.UnionWith(BuiltIns.GlobalObjects.Keys);

        // 添加自定义函数
        identifiers.UnionWith(CustomFunctions.Names);

        // 添加全局变量
        identifiers.UnionWith(GlobalVariables.Variables.Keys);

        // 添加常用属性
        identifiers.UnionWith(BuiltIns.Properties.Keys);

        // 添加对象方法
        foreach (var methods in BuiltIns.ObjectMethods.Values)
        {
            identifiers.UnionWith(methods);
        }

        return identifiers;
    }

    public static bool IsArrayMethod(string? methodName) => methodName != null && BuiltIns.ArrayPrototypeMethods.ContainsKey(methodName);

    /// <summary>
    /// 检查是否为关键字
    /// </summary>
    public static bool IsKeyword(string? word) => Keywords.All.Contains(word);

    public static bool IsBuiltInFunction(string? functionName)
    {
        if (string.IsNullOrEmpty(functionName))
            return false;

        // 检查是否在全局函数列表中
        if (BuiltIns.GlobalFunctions.ContainsKey(functionName))
            return true;

        // 检查是否在全局对象列表中（有些对象也可以作为函数调用，如 Array(), Object() 等）
        if (!BuiltIns.GlobalObjects.ContainsKey(functionName)) return false;
        var callableObjects = new[] { "Array", "Object", "String", "Number", "Boolean", "Date", "RegExp", "Error", "Function", "Promise" };
        return callableObjects.Contains(functionName);

    }

    /// <summary>
    /// 检查是否为自定义函数
    /// </summary>
    public static bool IsCustomFunction(string? name) => CustomFunctions.Names.Contains(name);


    /// <summary>
    /// 检查是否是字符串方法
    /// </summary>
    public static bool IsStringMethod(string? methodName)
    {
        if (string.IsNullOrEmpty(methodName))
            return false;

        var stringMethods = new HashSet<string>
        {
            "charAt", "charCodeAt", "concat", "includes", "indexOf", "lastIndexOf",
            "match", "padEnd", "padStart", "repeat", "replace", "replaceAll",
            "search", "slice", "split", "startsWith", "endsWith", "substring",
            "toLowerCase", "toUpperCase", "trim", "trimEnd", "trimStart",
            "valueOf", "toString", "toLocaleLowerCase", "toLocaleUpperCase"
        };

        return stringMethods.Contains(methodName);
    }

    /// <summary>
    /// 检查是否是对象方法
    /// </summary>
    public static bool IsObjectMethod(string? methodName)
    {
        if (string.IsNullOrEmpty(methodName))
            return false;

        var objectMethods = new HashSet<string>
        {
            "hasOwnProperty", "isPrototypeOf", "propertyIsEnumerable",
            "toLocaleString", "toString", "valueOf"
        };

        return objectMethods.Contains(methodName);
    }

    /// <summary>
    /// 检查是否是任何内置类型的方法
    /// </summary>
    public static bool IsBuiltInMethod(string? methodName)
    {
        return IsArrayMethod(methodName) || IsStringMethod(methodName) || IsObjectMethod(methodName);
    }

    /// <summary>
    /// 获取函数定义
    /// </summary>
    public static FunctionDefinition? GetFunctionDefinition(string? name) => name != null && CustomFunctions.Functions.TryGetValue(name, out var def) ? def : null;
}

/// <summary>
/// 函数定义
/// </summary>
public class FunctionDefinition
{
    public string? Name { get; set; }
    public string? Signature { get; set; }
    public string? Description { get; set; }
    public ParameterDefinition[] Parameters { get; set; } = [];
    public string ReturnType { get; set; } = "void";
}

/// <summary>
/// 参数定义
/// </summary>
public class ParameterDefinition(string name, string type, string description, bool optional = false)
{
    public string Name { get; set; } = name;
    public string Type { get; set; } = type;
    public string Description { get; set; } = description;
    public bool Optional { get; set; } = optional;
}