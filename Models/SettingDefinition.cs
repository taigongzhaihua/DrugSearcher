namespace DrugSearcher.Models;

public class SettingDefinition
{
    public string Key { get; set; } = string.Empty;

    public Type ValueType { get; set; } = typeof(string);

    public object? DefaultValue { get; set; }

    public string? Description { get; set; }

    public string? Category { get; set; }

    public bool IsReadOnly { get; set; } = false;

    public Func<object?, bool>? Validator { get; set; }
}