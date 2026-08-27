using CommunityToolkit.Mvvm.ComponentModel;

namespace TidyMemo.ViewModels;

public partial class SlideshowItemViewModel(string path) : ViewModelBase
{
    public string Path { get; } = path;
    public string FileName { get; } = System.IO.Path.GetFileName(path);

    [ObservableProperty]
    private int _position;
}
