using Microsoft.UI;
using Microsoft.UI.Content;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Menu500Tracker.Services;
using TaskbarWidget;

namespace Menu500Tracker.Widget;

public class Menu500Widget : IDisposable
{
    private const string WidgetClassName = "Menu500TrackerTaskbarWidget";
    private const int WidgetWidthDip = 40;

    private readonly MenuFetchService _menuService;
    private TaskbarInjectionHelper? _injectionHelper;
    private DesktopWindowXamlSource? _xamlSource;
    private Menu500WidgetContent? _content;
    private bool _disposed;

    public Menu500Widget(MenuFetchService menuService)
    {
        _menuService = menuService;
        _menuService.MenuUpdated += OnMenuUpdated;
    }

    public void Initialize()
    {
        var config = new TaskbarInjectionConfig
        {
            ClassName = WidgetClassName,
            WindowTitle = "Menu500Tracker",
            WidthDip = WidgetWidthDip,
            DeferInjection = true
        };

        _injectionHelper = new TaskbarInjectionHelper(config);
        var result = _injectionHelper.Initialize();

        if (!result.Success || result.WindowHandle == IntPtr.Zero)
        {
            return;
        }

        var windowId = Win32Interop.GetWindowIdFromWindow(result.WindowHandle);
        _xamlSource = new DesktopWindowXamlSource();
        _xamlSource.Initialize(windowId);
        _xamlSource.SiteBridge.ResizePolicy = ContentSizePolicy.ResizeContentToParentWindow;

        _content = new Menu500WidgetContent();

        // Update with current menu if already fetched
        if (_menuService.CurrentMenu != null)
        {
            _content.UpdateMenu(_menuService.CurrentMenu);
        }

        var rootGrid = new Microsoft.UI.Xaml.Controls.Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0))
        };
        rootGrid.Children.Add(_content);

        _xamlSource.Content = rootGrid;
        _injectionHelper.Inject();
        _injectionHelper.Show();
    }

    private void OnMenuUpdated(object? sender, Models.DailyMenu menu)
    {
        _content?.DispatcherQueue?.TryEnqueue(() =>
        {
            _content?.UpdateMenu(menu);
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _menuService.MenuUpdated -= OnMenuUpdated;

        _xamlSource?.Dispose();
        _injectionHelper?.Dispose();
    }
}
