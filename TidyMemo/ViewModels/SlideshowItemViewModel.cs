using CommunityToolkit.Mvvm.ComponentModel;

namespace TidyMemo.ViewModels;

public partial class SlideshowItemViewModel(string path, System.Guid? id = null) : ViewModelBase
{
    public System.Guid Id { get; } = id ?? System.Guid.NewGuid();
    public string Path { get; } = path;
    public string FileName { get; } = System.IO.Path.GetFileName(path);

    [ObservableProperty]
    private int _position;
}
