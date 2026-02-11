using Menu500Tracker.Services;
using Menu500Tracker.Widget;
using TaskbarWidget;

namespace Menu500Tracker;

public static class Program
{
    private static readonly string LogPath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? ".", "menu500-debug.log");

    public static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); }
        catch { }
    }

    [STAThread]
    public static void Main(string[] args)
    {
        Log("=== Starting Menu500Tracker ===");

        try
        {
            // Ensure app starts with Windows
            StartupService.EnsureStartupEnabled();

            var menuService = new MenuFetchService();
            var widget = new Menu500Widget(menuService, Log);
            widget.Initialize();
            menuService.Start();

            Log("Entering message loop");
            TaskbarWidget.Widget.RunMessageLoop();

            Log("Message loop exited");
            widget.Dispose();
            menuService.Dispose();
        }
        catch (Exception ex)
        {
            Log($"FATAL: {ex}");
        }
    }
}
