namespace DrugSearcher.Models
{
    /// <summary>
    /// 语法验证结果
    /// </summary>
    public class SyntaxValidationResult
    {
        public bool IsValid { get; set; }
        public List<SyntaxError> Errors { get; set; } = [];
        public DateTime ValidationTime { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 语法错误
    /// </summary>
    public class SyntaxError
    {
        public string Message { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public SyntaxErrorSeverity Severity { get; set; }
    }

    /// <summary>
    /// 语法错误严重程度
    /// </summary>
    public enum SyntaxErrorSeverity
    {
        Error,
        Warning,
        Info
    }
}