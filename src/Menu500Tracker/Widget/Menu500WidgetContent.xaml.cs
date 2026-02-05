using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Menu500Tracker.Models;
using Windows.UI.ViewManagement;

namespace Menu500Tracker.Widget;

public sealed partial class Menu500WidgetContent : UserControl
{
    [DllImport("uxtheme.dll", EntryPoint = "#138", SetLastError = true)]
    private static extern bool ShouldSystemUseDarkMode();

    private readonly UISettings _uiSettings = new();

    public Menu500WidgetContent()
    {
        InitializeComponent();
        HoverBorder.PointerEntered += OnPointerEntered;
        HoverBorder.PointerExited += OnPointerExited;

        // Set initial theme
        UpdateTheme();

        // Listen for system theme changes
        _uiSettings.ColorValuesChanged += (sender, args) =>
        {
            DispatcherQueue.TryEnqueue(UpdateTheme);
        };
    }

    private void UpdateTheme()
    {
        // Use system dark mode (for taskbar) instead of app dark mode
        bool isDarkMode = false;
        try
        {
            isDarkMode = ShouldSystemUseDarkMode();
        }
        catch
        {
            // Fallback to UISettings if the undocumented API fails
            var foreground = _uiSettings.GetColorValue(UIColorType.Foreground);
            isDarkMode = ((foreground.R + foreground.G + foreground.B) / 3) > 128;
        }

        // Dark mode = white text, Light mode = black text
        var textColor = isDarkMode
            ? Windows.UI.Color.FromArgb(255, 255, 255, 255) // White
            : Windows.UI.Color.FromArgb(255, 0, 0, 0);       // Black

        WidgetText.Foreground = new SolidColorBrush(textColor);
    }

    public void UpdateMenu(DailyMenu menu)
    {
        string tooltipContent;

        if (menu.IsError)
        {
            tooltipContent = menu.ErrorMessage ?? "Error loading menu";
        }
        else
        {
            // Capitalize the day name
            var dayName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(menu.DayName);

            var lines = new List<string> { dayName, "" };

            if (!string.IsNullOrWhiteSpace(menu.Soup))
            {
                lines.Add(menu.Soup);
            }

            if (!string.IsNullOrWhiteSpace(menu.MainDish))
            {
                lines.Add(menu.MainDish);
            }

            tooltipContent = string.Join("\n", lines);
        }

        MenuToolTip.Content = tooltipContent;
    }

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        HoverBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 255, 255));
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        HoverBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }
}
