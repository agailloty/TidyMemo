using MetadataExtractor;

namespace TidyMemo.ViewModels;

public class ExifTokenItemViewModel : ViewModelBase
{
    private bool _isSelected;
    private bool _isEnabled;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    public required string TagName { get; set; }
    public required string TagKey { get; set; }
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
}