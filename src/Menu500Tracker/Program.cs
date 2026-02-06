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
            // Initialize common controls (required for tooltips)
            var icc = new Native.INITCOMMONCONTROLSEX
            {
                dwSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.INITCOMMONCONTROLSEX>(),
                dwICC = Native.ICC_WIN95_CLASSES
            };
            var iccResult = Native.InitCommonControlsEx(ref icc);
            Log($"InitCommonControlsEx: {iccResult}");

            var menuService = new MenuFetchService();
            var widget = new Menu500Widget(menuService, Log);
            widget.Initialize();
            menuService.Start();

            Log("Entering message loop");

            // Win32 message loop
            while (Native.GetMessageW(out var msg, IntPtr.Zero, 0, 0))
            {
                Native.TranslateMessage(ref msg);
                Native.DispatchMessageW(ref msg);
            }

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
