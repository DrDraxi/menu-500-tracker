namespace Menu500Tracker.Models;

public class DailyMenu
{
    public string DayName { get; init; } = string.Empty;
    public string? Soup { get; init; }
    public string MainDish { get; init; } = string.Empty;
    public DateTime FetchedAt { get; init; }
    public bool IsError { get; init; }
    public string? ErrorMessage { get; init; }

    public static DailyMenu CreateError(string errorMessage) => new()
    {
        IsError = true,
        ErrorMessage = errorMessage,
        FetchedAt = DateTime.Now
    };

    public static DailyMenu CreateWeekend() => new()
    {
        IsError = true,
        ErrorMessage = "Restaurant closed on weekends",
        FetchedAt = DateTime.Now
    };
}
