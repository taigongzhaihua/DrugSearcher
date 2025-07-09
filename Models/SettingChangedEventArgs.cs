namespace DrugSearcher.Models;

public class SettingChangedEventArgs(string key, object? oldValue, object? newValue, Type valueType)
    : EventArgs
{
    public string Key { get; } = key;
    public object? OldValue { get; } = oldValue;
    public object? NewValue { get; } = newValue;
    public Type ValueType { get; } = valueType;
}