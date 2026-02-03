using System.Globalization;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Menu500Tracker.Models;

namespace Menu500Tracker.Widget;

public sealed partial class Menu500WidgetContent : UserControl
{
    public Menu500WidgetContent()
    {
        InitializeComponent();
        HoverBorder.PointerEntered += OnPointerEntered;
        HoverBorder.PointerExited += OnPointerExited;
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
