using Microsoft.Win32;

namespace Menu500Tracker.Services;

public static class StartupService
{
    private const string AppName = "Menu500Tracker";
    private const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetStartupEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
        catch
        {
            // Ignore registry errors
        }
    }

    /// <summary>
    /// Ensures startup is enabled with the current exe path. Call this on app startup.
    /// Always writes the path to handle the app being moved to a new location.
    /// </summary>
    public static void EnsureStartupEnabled()
    {
        SetStartupEnabled(true);
    }
}
