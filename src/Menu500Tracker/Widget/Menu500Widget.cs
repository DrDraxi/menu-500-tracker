using System.Globalization;
using System.Runtime.InteropServices;
using Menu500Tracker.Models;
using Menu500Tracker.Services;
using TaskbarWidget;

namespace Menu500Tracker.Widget;

public class Menu500Widget : IDisposable
{
    private const string WidgetClassName = "Menu500TrackerTaskbarWidget";
    private const int WidgetWidthDip = 40;
    private const string DisplayText = "500";
    private const int ANTIALIASED_QUALITY = 4;

    // Widget hover styling
    private const int MarginTop = 4;
    private const int MarginBottom = 4;
    private const int MarginLeft = 2;
    private const int MarginRight = 2;
    private const int CornerRadius = 4;

    // Custom tooltip
    private const string TooltipClassName = "Menu500TrackerTooltip";
    private const int TooltipTimerId = 2;
    private const int TooltipShowDelayMs = 400;
    private const int TooltipMaxWidthDip = 360;
    private const int TooltipPaddingDip = 16;
    private const int TooltipCornerRadiusDip = 4;
    private const int TooltipGapDip = 8;
    private const int TooltipTitleBodyGapDip = 6;

    // Fade animation
    private const int FadeTimerId = 3;
    private const int FadeIntervalMs = 16;
    private const int FadeInStep = 30;   // 0→255 in ~130ms
    private const int FadeOutStep = 45;  // 255→0 in ~90ms

    private const uint WM_TIMER = 0x0113;

    private readonly MenuFetchService _menuService;
    private readonly Action<string>? _log;
    private TaskbarInjectionHelper? _injectionHelper;

    // Widget window
    private IntPtr _hwnd;
    private IntPtr _hwndTaskbar;
    private int _width;
    private int _height;
    private bool _isHovering;
    private bool _trackingMouse;
    private bool _disposed;
    private readonly WndProcDelegate _wndProc;

    // Custom tooltip window
    private IntPtr _hwndTooltip;
    private WndProcDelegate? _tooltipWndProc;
    private bool _tooltipVisible;
    private string _tooltipTitle = "";
    private string _tooltipBody = "Loading menu...";

    // Tooltip fade state
    private IntPtr _tooltipMemDc;
    private IntPtr _tooltipBitmap;
    private IntPtr _tooltipOldBitmap;
    private int _tooltipPosX, _tooltipPosY;
    private int _tooltipRenderW, _tooltipRenderH;
    private byte _tooltipAlpha;
    private byte _tooltipTargetAlpha;

    public Menu500Widget(MenuFetchService menuService, Action<string>? log = null)
    {
        _menuService = menuService;
        _log = log;
        _menuService.MenuUpdated += OnMenuUpdated;
        _wndProc = WndProc;
    }

    public void Initialize()
    {
        _log?.Invoke("Widget.Initialize starting");

        var config = new TaskbarInjectionConfig
        {
            ClassName = WidgetClassName,
            WindowTitle = "Menu500Tracker",
            WidthDip = WidgetWidthDip,
            DeferInjection = true,
            WndProc = _wndProc,
            ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST
        };

        _injectionHelper = new TaskbarInjectionHelper(config, _log);
        var result = _injectionHelper.Initialize();

        if (!result.Success || result.WindowHandle == IntPtr.Zero)
            return;

        _hwnd = result.WindowHandle;
        _hwndTaskbar = result.TaskbarHandle;
        _width = result.Width;
        _height = result.Height;

        CreateTooltipWindow();

        if (_menuService.CurrentMenu != null)
            UpdateTooltipText(_menuService.CurrentMenu);

        PositionOverTaskbar();
        _injectionHelper.Show();
        RenderWidget();

        _log?.Invoke("Widget.Initialize done");
    }

    private void PositionOverTaskbar()
    {
        Native.GetWindowRect(_hwndTaskbar, out var taskbarRect);
        var slot = new TaskbarSlotFinder().FindSlot(_width, _hwnd, 4, _log);
        int screenX = taskbarRect.Left + slot.X;
        int screenY = taskbarRect.Top + slot.Y;

        Native.SetWindowPos(_hwnd, IntPtr.Zero, screenX, screenY, _width, _height,
            Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    private void CreateTooltipWindow()
    {
        _tooltipWndProc = (hwnd, msg, wParam, lParam) =>
            Native.DefWindowProcW(hwnd, msg, wParam, lParam);

        var wndClass = new Native.WNDCLASS
        {
            lpszClassName = TooltipClassName,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_tooltipWndProc),
            hInstance = Native.GetModuleHandleW(null)
        };
        Native.RegisterClassW(ref wndClass);

        _hwndTooltip = Native.CreateWindowExW(
            Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE,
            TooltipClassName,
            string.Empty,
            Native.WS_POPUP,
            0, 0, 0, 0,
            IntPtr.Zero, IntPtr.Zero, Native.GetModuleHandleW(null), IntPtr.Zero);

        _log?.Invoke($"Custom tooltip created: {_hwndTooltip:X}");
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

        if (_tooltipVisible)
            ShowTooltip();
    }

    private void OnMenuUpdated(object? sender, DailyMenu menu)
    {
        UpdateTooltipText(menu);
    }

    private void ShowTooltip()
    {
        if (_hwndTooltip == IntPtr.Zero || _hwnd == IntPtr.Zero) return;
        if (string.IsNullOrEmpty(_tooltipTitle) && string.IsNullOrEmpty(_tooltipBody)) return;

        RenderTooltipBitmap();

        if (!_tooltipVisible)
        {
            _tooltipAlpha = 0;
            Native.ShowWindow(_hwndTooltip, Native.SW_SHOW);
            _tooltipVisible = true;
        }

        _tooltipTargetAlpha = 255;
        ApplyTooltipAlpha();

        if (_tooltipAlpha < 255)
            Native.SetTimer(_hwnd, (IntPtr)FadeTimerId, FadeIntervalMs, IntPtr.Zero);
    }

    private void HideTooltip()
    {
        Native.KillTimer(_hwnd, (IntPtr)TooltipTimerId);

        if (_tooltipVisible)
        {
            _tooltipTargetAlpha = 0;
            Native.SetTimer(_hwnd, (IntPtr)FadeTimerId, FadeIntervalMs, IntPtr.Zero);
        }
    }

    private void OnFadeTimer()
    {
        if (_tooltipAlpha < _tooltipTargetAlpha)
            _tooltipAlpha = (byte)Math.Min(255, _tooltipAlpha + FadeInStep);
        else if (_tooltipAlpha > _tooltipTargetAlpha)
            _tooltipAlpha = (byte)Math.Max(0, _tooltipAlpha - FadeOutStep);

        ApplyTooltipAlpha();

        if (_tooltipAlpha == _tooltipTargetAlpha)
        {
            Native.KillTimer(_hwnd, (IntPtr)FadeTimerId);
            if (_tooltipAlpha == 0)
            {
                Native.ShowWindow(_hwndTooltip, Native.SW_HIDE);
                _tooltipVisible = false;
                FreeTooltipBitmap();
            }
        }
    }

    private void ApplyTooltipAlpha()
    {
        if (_tooltipMemDc == IntPtr.Zero) return;

        var screenDc = Native.GetDC(IntPtr.Zero);
        var ptDst = new Native.POINT { X = _tooltipPosX, Y = _tooltipPosY };
        var ptSrc = new Native.POINT { X = 0, Y = 0 };
        var size = new Native.SIZE { cx = _tooltipRenderW, cy = _tooltipRenderH };
        var blend = new Native.BLENDFUNCTION
        {
            BlendOp = Native.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = _tooltipAlpha,
            AlphaFormat = Native.AC_SRC_ALPHA
        };

        Native.UpdateLayeredWindow(_hwndTooltip, screenDc, ref ptDst, ref size,
            _tooltipMemDc, ref ptSrc, 0, ref blend, Native.ULW_ALPHA);
        Native.ReleaseDC(IntPtr.Zero, screenDc);
    }

    private void FreeTooltipBitmap()
    {
        if (_tooltipMemDc != IntPtr.Zero)
        {
            if (_tooltipOldBitmap != IntPtr.Zero)
                Native.SelectObject(_tooltipMemDc, _tooltipOldBitmap);
            Native.DeleteDC(_tooltipMemDc);
            _tooltipMemDc = IntPtr.Zero;
            _tooltipOldBitmap = IntPtr.Zero;
        }
        if (_tooltipBitmap != IntPtr.Zero)
        {
            Native.DeleteObject(_tooltipBitmap);
            _tooltipBitmap = IntPtr.Zero;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Native.WM_MOUSEMOVE:
                if (!_trackingMouse)
                {
                    var tme = new Native.TRACKMOUSEEVENT
                    {
                        cbSize = (uint)Marshal.SizeOf<Native.TRACKMOUSEEVENT>(),
                        dwFlags = Native.TME_LEAVE,
                        hwndTrack = hwnd
                    };
                    Native.TrackMouseEvent(ref tme);
                    _trackingMouse = true;
                }
                if (!_isHovering)
                {
                    _isHovering = true;
                    RenderWidget();

                    if (_tooltipVisible && _tooltipTargetAlpha == 0)
                    {
                        // Tooltip fading out - reverse to fade in
                        _tooltipTargetAlpha = 255;
                        Native.SetTimer(_hwnd, (IntPtr)FadeTimerId, FadeIntervalMs, IntPtr.Zero);
                    }
                    else if (!_tooltipVisible)
                    {
                        Native.SetTimer(_hwnd, (IntPtr)TooltipTimerId, TooltipShowDelayMs, IntPtr.Zero);
                    }
                }
                return IntPtr.Zero;

            case Native.WM_MOUSELEAVE:
                _isHovering = false;
                _trackingMouse = false;
                RenderWidget();
                HideTooltip();
                return IntPtr.Zero;

            case WM_TIMER:
                if (wParam == (IntPtr)TooltipTimerId)
                {
                    Native.KillTimer(_hwnd, (IntPtr)TooltipTimerId);
                    ShowTooltip();
                    return IntPtr.Zero;
                }
                if (wParam == (IntPtr)FadeTimerId)
                {
                    OnFadeTimer();
                    return IntPtr.Zero;
                }
                break;
        }

        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private static bool IsInsideRoundedRect(int x, int y, int left, int top, int right, int bottom, int radius)
    {
        if (x < left || x >= right || y < top || y >= bottom)
            return false;

        int innerLeft = left + radius;
        int innerRight = right - radius;
        int innerTop = top + radius;
        int innerBottom = bottom - radius;

        if ((x >= innerLeft && x < innerRight) || (y >= innerTop && y < innerBottom))
            return true;

        int cx, cy;
        if (x < innerLeft && y < innerTop) { cx = innerLeft; cy = innerTop; }
        else if (x >= innerRight && y < innerTop) { cx = innerRight - 1; cy = innerTop; }
        else if (x < innerLeft && y >= innerBottom) { cx = innerLeft; cy = innerBottom - 1; }
        else { cx = innerRight - 1; cy = innerBottom - 1; }

        int dx = x - cx;
        int dy = y - cy;
        return (dx * dx + dy * dy) <= (radius * radius);
    }

    private unsafe void RenderWidget()
    {
        if (_hwnd == IntPtr.Zero || _width == 0 || _height == 0) return;

        bool isDark;
        try { isDark = Native.ShouldSystemUseDarkMode(); }
        catch { isDark = true; }

        var bmi = new Native.BITMAPINFO
        {
            bmiHeader = new Native.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<Native.BITMAPINFOHEADER>(),
                biWidth = _width,
                biHeight = -_height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Native.BI_RGB
            }
        };

        var screenDc = Native.GetDC(IntPtr.Zero);
        var memDc = Native.CreateCompatibleDC(screenDc);
        var hBitmap = Native.CreateDIBSection(memDc, ref bmi, Native.DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
        var oldBitmap = Native.SelectObject(memDc, hBitmap);

        int pixelCount = _width * _height;
        var pixelPtr = (uint*)bits;

        // Alpha=1 on all pixels for full-area hit testing (invisible but mouse-responsive)
        for (int i = 0; i < pixelCount; i++)
            pixelPtr[i] = 0x01000000;

        if (_isHovering)
        {
            int rectLeft = MarginLeft;
            int rectTop = MarginTop;
            int rectRight = _width - MarginRight;
            int rectBottom = _height - MarginBottom;
            uint hoverPixel = isDark ? 0x14141414u : 0x14000000u;

            for (int y = 0; y < _height; y++)
                for (int x = 0; x < _width; x++)
                    if (IsInsideRoundedRect(x, y, rectLeft, rectTop, rectRight, rectBottom, CornerRadius))
                        pixelPtr[y * _width + x] = hoverPixel;
        }

        var dpiScale = _injectionHelper?.DpiScale ?? 1.0;
        var fontSize = -(int)(14 * dpiScale);
        var hFont = Native.CreateFontW(
            fontSize, 0, 0, 0, Native.FW_BOLD,
            0, 0, 0, Native.DEFAULT_CHARSET,
            Native.OUT_DEFAULT_PRECIS, Native.CLIP_DEFAULT_PRECIS,
            ANTIALIASED_QUALITY, Native.DEFAULT_PITCH,
            "Segoe UI");

        var oldFont = Native.SelectObject(memDc, hFont);
        Native.SetBkMode(memDc, Native.TRANSPARENT);
        Native.SetTextColor(memDc, Native.RGB(255, 255, 255));

        var textRect = new Native.RECT { Left = 0, Top = 0, Right = _width, Bottom = _height };
        Native.DrawTextW(memDc, DisplayText, -1, ref textRect,
            Native.DT_CENTER | Native.DT_VCENTER | Native.DT_SINGLELINE);

        Native.SelectObject(memDc, oldFont);
        Native.DeleteObject(hFont);

        const byte hoverThreshold = 24;
        for (int i = 0; i < pixelCount; i++)
        {
            uint pixel = pixelPtr[i];
            byte b = (byte)(pixel & 0xFF);
            byte g = (byte)((pixel >> 8) & 0xFF);
            byte r = (byte)((pixel >> 16) & 0xFF);

            byte coverage = Math.Max(r, Math.Max(g, b));
            if (coverage > hoverThreshold)
            {
                if (isDark)
                    pixelPtr[i] = ((uint)coverage << 24) | ((uint)coverage << 16) | ((uint)coverage << 8) | coverage;
                else
                    pixelPtr[i] = (uint)coverage << 24;
            }
        }

        Native.GetWindowRect(_hwnd, out var windowRect);
        var ptDst = new Native.POINT { X = windowRect.Left, Y = windowRect.Top };
        var ptSrc = new Native.POINT { X = 0, Y = 0 };
        var size = new Native.SIZE { cx = _width, cy = _height };
        var blend = new Native.BLENDFUNCTION
        {
            BlendOp = Native.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = Native.AC_SRC_ALPHA
        };

        Native.UpdateLayeredWindow(_hwnd, screenDc, ref ptDst, ref size, memDc, ref ptSrc, 0, ref blend, Native.ULW_ALPHA);

        Native.SelectObject(memDc, oldBitmap);
        Native.DeleteObject(hBitmap);
        Native.DeleteDC(memDc);
        Native.ReleaseDC(IntPtr.Zero, screenDc);
    }

    private unsafe void RenderTooltipBitmap()
    {
        if (_hwndTooltip == IntPtr.Zero) return;

        FreeTooltipBitmap();

        bool isDark;
        try { isDark = Native.ShouldSystemUseDarkMode(); }
        catch { isDark = true; }

        var dpiScale = _injectionHelper?.DpiScale ?? 1.0;
        int padding = (int)(TooltipPaddingDip * dpiScale);
        int maxWidth = (int)(TooltipMaxWidthDip * dpiScale);
        int cr = (int)(TooltipCornerRadiusDip * dpiScale);
        int titleBodyGap = (int)(TooltipTitleBodyGapDip * dpiScale);

        int titleFontSize = -(int)(13 * dpiScale);
        int bodyFontSize = -(int)(12 * dpiScale);

        var hTitleFont = Native.CreateFontW(
            titleFontSize, 0, 0, 0, 600,
            0, 0, 0, Native.DEFAULT_CHARSET,
            Native.OUT_DEFAULT_PRECIS, Native.CLIP_DEFAULT_PRECIS,
            Native.CLEARTYPE_QUALITY, Native.DEFAULT_PITCH,
            "Segoe UI");

        var hBodyFont = Native.CreateFontW(
            bodyFontSize, 0, 0, 0, Native.FW_NORMAL,
            0, 0, 0, Native.DEFAULT_CHARSET,
            Native.OUT_DEFAULT_PRECIS, Native.CLIP_DEFAULT_PRECIS,
            Native.CLEARTYPE_QUALITY, Native.DEFAULT_PITCH,
            "Segoe UI");

        // Measure text
        var screenDc = Native.GetDC(IntPtr.Zero);
        int maxTextWidth = maxWidth - 2 * padding;

        int titleWidth = 0, titleHeight = 0;
        if (!string.IsNullOrEmpty(_tooltipTitle))
        {
            var oldMF = Native.SelectObject(screenDc, hTitleFont);
            var rect = new Native.RECT { Right = maxTextWidth };
            Native.DrawTextW(screenDc, _tooltipTitle, -1, ref rect,
                Native.DT_CALCRECT | Native.DT_NOPREFIX | Native.DT_WORDBREAK);
            titleWidth = rect.Right;
            titleHeight = rect.Bottom;
            Native.SelectObject(screenDc, oldMF);
        }

        int bodyWidth = 0, bodyHeight = 0;
        if (!string.IsNullOrEmpty(_tooltipBody))
        {
            var oldMF = Native.SelectObject(screenDc, hBodyFont);
            var rect = new Native.RECT { Right = maxTextWidth };
            Native.DrawTextW(screenDc, _tooltipBody, -1, ref rect,
                Native.DT_CALCRECT | Native.DT_NOPREFIX | Native.DT_WORDBREAK);
            bodyWidth = rect.Right;
            bodyHeight = rect.Bottom;
            Native.SelectObject(screenDc, oldMF);
        }

        int gap = (titleHeight > 0 && bodyHeight > 0) ? titleBodyGap : 0;
        int contentWidth = Math.Max(titleWidth, bodyWidth);
        int contentHeight = titleHeight + gap + bodyHeight;

        int tw = contentWidth + 2 * padding;
        int th = contentHeight + 2 * padding;

        // Position above widget, centered
        Native.GetWindowRect(_hwnd, out var widgetRect);
        _tooltipPosX = widgetRect.Left + (_width - tw) / 2;
        _tooltipPosY = widgetRect.Top - th - (int)(TooltipGapDip * dpiScale);
        if (_tooltipPosX < 0) _tooltipPosX = 0;
        if (_tooltipPosY < 0) _tooltipPosY = 0;
        _tooltipRenderW = tw;
        _tooltipRenderH = th;

        // Create DIB
        var bmi = new Native.BITMAPINFO
        {
            bmiHeader = new Native.BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<Native.BITMAPINFOHEADER>(),
                biWidth = tw,
                biHeight = -th,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Native.BI_RGB
            }
        };

        var memDc = Native.CreateCompatibleDC(screenDc);
        var hBitmap = Native.CreateDIBSection(memDc, ref bmi, Native.DIB_RGB_COLORS, out var bits, IntPtr.Zero, 0);
        var oldBitmap = Native.SelectObject(memDc, hBitmap);

        int pixelCount = tw * th;
        var pixelPtr = (uint*)bits;

        for (int i = 0; i < pixelCount; i++)
            pixelPtr[i] = 0x00000000;

        // Fill rounded rect with background
        uint bgColor = isDark ? 0xFF2C2C2Cu : 0xFFF9F9F9u;
        for (int i = 0; i < pixelCount; i++)
        {
            int x = i % tw;
            int y = i / tw;
            if (IsInsideRoundedRect(x, y, 0, 0, tw, th, cr))
                pixelPtr[i] = bgColor;
        }

        // Draw text
        Native.SetBkMode(memDc, Native.TRANSPARENT);
        var oldFont = Native.SelectObject(memDc, hTitleFont);

        if (!string.IsNullOrEmpty(_tooltipTitle))
        {
            uint titleColor = isDark ? Native.RGB(255, 255, 255) : Native.RGB(26, 26, 26);
            Native.SetTextColor(memDc, titleColor);
            var drawRect = new Native.RECT
            {
                Left = padding, Top = padding,
                Right = padding + contentWidth, Bottom = padding + titleHeight
            };
            Native.DrawTextW(memDc, _tooltipTitle, -1, ref drawRect,
                Native.DT_NOPREFIX | Native.DT_WORDBREAK);
        }

        if (!string.IsNullOrEmpty(_tooltipBody))
        {
            Native.SelectObject(memDc, hBodyFont);
            uint bodyColor = isDark ? Native.RGB(200, 200, 200) : Native.RGB(64, 64, 64);
            Native.SetTextColor(memDc, bodyColor);
            int bodyTop = padding + titleHeight + gap;
            var drawRect = new Native.RECT
            {
                Left = padding, Top = bodyTop,
                Right = padding + contentWidth, Bottom = bodyTop + bodyHeight
            };
            Native.DrawTextW(memDc, _tooltipBody, -1, ref drawRect,
                Native.DT_NOPREFIX | Native.DT_WORDBREAK);
        }

        Native.SelectObject(memDc, oldFont);
        Native.DeleteObject(hTitleFont);
        Native.DeleteObject(hBodyFont);

        // Fix alpha: GDI zeroes alpha on 32-bit DIBs.
        // Force opaque inside, draw 1px border on edge.
        uint borderColor = isDark ? 0xFF464646u : 0xFFDCDCDCu;
        for (int i = 0; i < pixelCount; i++)
        {
            int x = i % tw;
            int y = i / tw;
            if (!IsInsideRoundedRect(x, y, 0, 0, tw, th, cr))
                continue;

            if (!IsInsideRoundedRect(x, y, 1, 1, tw - 1, th - 1, Math.Max(cr - 1, 0)))
                pixelPtr[i] = borderColor;
            else
                pixelPtr[i] |= 0xFF000000;
        }

        // Store bitmap for fade animation (don't free yet)
        _tooltipMemDc = memDc;
        _tooltipBitmap = hBitmap;
        _tooltipOldBitmap = oldBitmap;

        Native.ReleaseDC(IntPtr.Zero, screenDc);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _menuService.MenuUpdated -= OnMenuUpdated;

        if (_hwnd != IntPtr.Zero)
        {
            Native.KillTimer(_hwnd, (IntPtr)TooltipTimerId);
            Native.KillTimer(_hwnd, (IntPtr)FadeTimerId);
        }

        FreeTooltipBitmap();

        if (_hwndTooltip != IntPtr.Zero)
        {
            Native.DestroyWindow(_hwndTooltip);
            _hwndTooltip = IntPtr.Zero;
        }

        _injectionHelper?.Dispose();
    }
}
