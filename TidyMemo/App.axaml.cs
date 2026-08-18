using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TidyMemo.Services;
using TidyMemo.ViewModels;
using TidyMemo.Views;

namespace TidyMemo;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;
            desktop.MainWindow.DataContext =
                new MainWindowViewModel(new DialogService(mainWindow));
        }

        base.OnFrameworkInitializationCompleted();
    }
}