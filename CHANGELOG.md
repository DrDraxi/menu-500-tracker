# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v2.1.0] - 2026-02-12

### Added
- Drag-to-reorder widgets — click and drag any widget to change its position
- Live preview during drag — other widgets slide in real-time to show the new order
- Atomic widget repositioning — widgets push neighbors aside when resizing

### Changed
- Migrated from manual Win32 rendering to high-level `TaskbarWidget.Widget` API
- Widget code reduced from ~500 lines to ~70 lines
- Tooltip, hover, and theme handling now delegated to the widget toolkit

### Removed
- Unused `EditModeWidget` (replaced by direct drag-to-reorder)

## [v2.0.1] - 2026-02-11

### Fixed
- Startup registry path now updates when app is moved to a new location

## [v2.0.0] - 2026-02-11

### Added
- Windows startup support — app can auto-launch on Windows login via registry
- `startWithWindows` configuration option (default: true)

### Changed
- Replaced WinUI 3 / Windows App SDK with pure Win32 GDI rendering (~10 MB exe vs ~153 MB)
- Widget text rendered via GDI `DrawTextW` instead of XAML `TextBlock`
- Tooltip now uses native Win32 `TOOLTIPS_CLASS` instead of WinUI `ToolTip`
- Menu refresh timer changed from `DispatcherQueueTimer` to `System.Threading.Timer`
- Taskbar injection uses `SetWindowPos` instead of `AppWindow` API

### Removed
- All XAML files (`App.xaml`, `MainWindow.xaml`, `Menu500WidgetContent.xaml`)
- `Microsoft.WindowsAppSDK` and `Microsoft.Windows.SDK.BuildTools` NuGet dependencies
- WinUI 3 framework dependency (no runtime prerequisites needed)

## [v1.1.0] - 2026-02-05

### Changed
- Widget text color now adapts to Windows light/dark theme automatically (white in dark mode, black in light mode)

## [v1.0.0] - 2026-02-03

### Added
- Initial release
- Taskbar widget displaying "500"
- Hourly menu fetch from 500restaurant.cz/denni-menu/
- Tooltip showing today's soup and main dish
- Weekend detection (shows "Restaurant closed on weekends")
- Error handling for network and parse failures
