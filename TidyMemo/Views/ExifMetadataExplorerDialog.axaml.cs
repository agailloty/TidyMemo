using Avalonia.Controls;
using TidyMemo.ViewModels;

namespace TidyMemo.Views;

public partial class ExifMetadataExplorerDialog : Window
{
    public ExifMetadataExplorerDialog()
    {
        InitializeComponent();
    }

    public ExifMetadataExplorerDialog(ExifInput parameter)
    {
        var screen = Screens.Primary;
        InitializeComponent();
        if (screen is not null)
        {
            Width = screen.WorkingArea.Width * 0.4;
            Height = screen.WorkingArea.Height * 0.5;
        }
        
        var vm = new ExifMetadataExplorerDialogViewModel(parameter);
        vm.RequestClose += (_, result) => Close(result);
        DataContext = vm;
    }
}