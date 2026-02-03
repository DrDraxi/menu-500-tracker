using Microsoft.UI.Xaml;
using Menu500Tracker.Services;
using Menu500Tracker.Widget;

namespace Menu500Tracker;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private Menu500Widget? _widget;
    private MenuFetchService? _menuService;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Create hidden window (required for WinUI lifecycle)
        _mainWindow = new MainWindow();
        // Don't call Activate() - keep window hidden

        // Create the menu fetch service
        _menuService = new MenuFetchService();

        // Create and initialize the widget
        _widget = new Menu500Widget(_menuService);
        _widget.Initialize();

        // Start fetching the menu
        _menuService.Start();
    }
}
