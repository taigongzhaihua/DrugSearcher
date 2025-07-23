using CommunityToolkit.Mvvm.ComponentModel;

namespace DrugSearcher.ViewModels;

/// <summary>
/// Tab项视图模型
/// </summary>
public partial class TabItemViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Header { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Content { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    [ObservableProperty]
    public partial bool IsSpecialTab { get; set; }

    public TabItemViewModel(string header, string key, bool isSpecialTab = false)
    {
        Header = header;
        Key = key;
        IsSpecialTab = isSpecialTab;
    }
}