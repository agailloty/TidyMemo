using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using TidyMemo.Services;
using TidyMemo.ViewModels;

namespace TidyMemo.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var dialogService = new DialogService(this);
        DataContext = new MainWindowViewModel(dialogService);
        var screen = Screens.Primary;
        // Set width and height as percentages of the screen
        if (screen is not null)
            Width = screen.WorkingArea.Width * 0.5;
    }

    private Grid? OverlayGrid => this.FindControl<Grid>("MainWindowOverlay");
    public MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void ShowOverlay()
    {
        if (OverlayGrid is null) return;
        OverlayGrid.IsVisible = true;
        OverlayGrid.ZIndex = 1000;
    }

    public void HideOverlay()
    {
        if (OverlayGrid is null) return;
        OverlayGrid.IsVisible = false;
        OverlayGrid.ZIndex = -1;
    }
}