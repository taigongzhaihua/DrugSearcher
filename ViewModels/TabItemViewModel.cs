using CommunityToolkit.Mvvm.ComponentModel;

namespace DrugSearcher.ViewModels;

/// <summary>
/// Tab项视图模型
/// </summary>
public partial class TabItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoaded;

    [ObservableProperty]
    private bool _isSpecialTab;

    public TabItemViewModel(string header, string key, bool isSpecialTab = false)
    {
        Header = header;
        Key = key;
        IsSpecialTab = isSpecialTab;
    }
}