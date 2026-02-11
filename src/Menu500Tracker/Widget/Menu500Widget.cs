using System.Globalization;
using Menu500Tracker.Models;
using Menu500Tracker.Services;
using TaskbarWidget;
using TaskbarWidget.Rendering;

namespace Menu500Tracker.Widget;

public class Menu500Widget : IDisposable
{
    private const string DisplayText = "500";

    private readonly MenuFetchService _menuService;
    private readonly Action<string>? _log;
    private TaskbarWidget.Widget? _widget;

    private string _tooltipTitle = "";
    private string _tooltipBody = "Loading menu...";
    private bool _disposed;

    public Menu500Widget(MenuFetchService menuService, Action<string>? log = null)
    {
        _menuService = menuService;
        _log = log;
        _menuService.MenuUpdated += OnMenuUpdated;
    }

    public void Initialize()
    {
        _log?.Invoke("Widget.Initialize starting");

        if (_menuService.CurrentMenu != null)
            UpdateTooltipText(_menuService.CurrentMenu);

        _widget = new TaskbarWidget.Widget("Menu500", render: ctx =>
        {
            ctx.DrawText(DisplayText, new TextStyle { FontSizeDip = 14, FontWeight = 600 });
            ctx.Tooltip(_tooltipTitle, _tooltipBody);
        }, new WidgetOptions { Log = _log });

        _widget.Show();
        _log?.Invoke("Widget.Initialize done");
    }

    private void UpdateTooltipText(DailyMenu menu)
    {
        if (menu.IsError)
        {
            _tooltipTitle = "";
            _tooltipBody = menu.ErrorMessage ?? "Error loading menu";
        }
        else
        {
            _tooltipTitle = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(menu.DayName);
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(menu.Soup))
                lines.Add(menu.Soup);
            if (!string.IsNullOrWhiteSpace(menu.MainDish))
                lines.Add(menu.MainDish);
            _tooltipBody = string.Join("\n", lines);
        }

        _widget?.Invalidate();
    }

    private void OnMenuUpdated(object? sender, DailyMenu menu)
    {
        UpdateTooltipText(menu);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _menuService.MenuUpdated -= OnMenuUpdated;
        _widget?.Dispose();
    }
}
