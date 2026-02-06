using System.Net.Http;
using System.Text.RegularExpressions;
using Menu500Tracker.Models;

namespace Menu500Tracker.Services;

public class MenuFetchService : IDisposable
{
    private const string MenuUrl = "https://www.500restaurant.cz/denni-menu/";
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(1);

    private static readonly Dictionary<DayOfWeek, string> CzechDayNames = new()
    {
        { DayOfWeek.Monday, "pondělí" },
        { DayOfWeek.Tuesday, "úterý" },
        { DayOfWeek.Wednesday, "středa" },
        { DayOfWeek.Thursday, "čtvrtek" },
        { DayOfWeek.Friday, "pátek" }
    };

    private readonly HttpClient _httpClient;
    private Timer? _timer;
    private bool _disposed;

    public event EventHandler<DailyMenu>? MenuUpdated;

    public DailyMenu? CurrentMenu { get; private set; }

    public MenuFetchService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Menu500Tracker/1.0");
    }

    public void Start()
    {
        // Fetch immediately, then repeat hourly
        _timer = new Timer(_ => _ = FetchMenuAsync(), null, TimeSpan.Zero, RefreshInterval);
    }

    public async Task FetchMenuAsync()
    {
        var today = DateTime.Now.DayOfWeek;

        // Check for weekend
        if (today == DayOfWeek.Saturday || today == DayOfWeek.Sunday)
        {
            CurrentMenu = DailyMenu.CreateWeekend();
            MenuUpdated?.Invoke(this, CurrentMenu);
            return;
        }

        try
        {
            var html = await _httpClient.GetStringAsync(MenuUrl);
            var menu = ParseMenu(html, today);
            CurrentMenu = menu;
            MenuUpdated?.Invoke(this, CurrentMenu);
        }
        catch (HttpRequestException ex)
        {
            CurrentMenu = DailyMenu.CreateError($"Could not fetch menu: {ex.Message}");
            MenuUpdated?.Invoke(this, CurrentMenu);
        }
        catch (Exception ex)
        {
            CurrentMenu = DailyMenu.CreateError($"Unexpected error: {ex.Message}");
            MenuUpdated?.Invoke(this, CurrentMenu);
        }
    }

    private DailyMenu ParseMenu(string html, DayOfWeek dayOfWeek)
    {
        if (!CzechDayNames.TryGetValue(dayOfWeek, out var czechDay))
        {
            return DailyMenu.CreateError("Could not determine day name");
        }

        try
        {
            // Find the section for today's menu
            // Website structure: <h4>pondělí</h4> followed by text content until next <h4>
            var dayPattern = $@"<h4[^>]*>\s*{Regex.Escape(czechDay)}\s*</h4>(.*?)(?=<h4|$)";
            var dayMatch = Regex.Match(html, dayPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (!dayMatch.Success)
            {
                return DailyMenu.CreateError("Could not find today's menu section");
            }

            var sectionHtml = dayMatch.Groups[1].Value;

            // Try to extract from <p> elements first
            var paragraphs = Regex.Matches(sectionHtml, @"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)
                .Select(m => StripHtml(m.Groups[1].Value).Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            // If no <p> elements found, try to extract text content directly
            if (paragraphs.Count == 0)
            {
                // Strip HTML and split by newlines/br tags
                var text = StripHtml(sectionHtml);
                paragraphs = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .ToList();
            }

            if (paragraphs.Count == 0)
            {
                return DailyMenu.CreateError("Could not parse menu items");
            }

            // First line is soup, second line is main dish
            string? soup = null;
            string mainDish;

            if (paragraphs.Count >= 2)
            {
                soup = paragraphs[0];
                mainDish = paragraphs[1];
            }
            else
            {
                // Only one item - assume it's the main dish
                mainDish = paragraphs[0];
            }

            return new DailyMenu
            {
                DayName = czechDay,
                Soup = soup,
                MainDish = mainDish,
                FetchedAt = DateTime.Now,
                IsError = false
            };
        }
        catch (Exception ex)
        {
            return DailyMenu.CreateError($"Could not parse menu: {ex.Message}");
        }
    }

    private static string StripHtml(string html)
    {
        // Remove HTML tags
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        // Decode common HTML entities
        text = text.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'");
        // Normalize whitespace
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer?.Dispose();
        _httpClient.Dispose();
    }
}
