# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Menu 500 Tracker is a Windows taskbar widget that displays "500" and shows today's daily menu from Restaurant 500 (500restaurant.cz) in a tooltip on hover. The menu is fetched hourly from the restaurant's website. Uses pure Win32 GDI for rendering (no WinUI/XAML framework).

## Build Commands

```bash
# Build the solution
dotnet build -p:Platform=x64

# Build release
dotnet build --configuration Release -p:Platform=x64

# Run the app (x64)
dotnet run --project src/Menu500Tracker/Menu500Tracker.csproj -p:Platform=x64

# Publish single-file exe (x64)
dotnet publish src/Menu500Tracker/Menu500Tracker.csproj --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=true -p:PublishTrimmed=true -o publish
```

## Architecture

### Solution Structure

- **Menu500Tracker** (`src/Menu500Tracker/`) - Win32 GDI app with taskbar widget
- **TaskbarWidget** (`lib/taskbar-widget/`) - Git submodule for taskbar widget injection

After cloning, initialize the submodule:
```bash
git submodule update --init --recursive
```

### Key Components

```
src/Menu500Tracker/
├── Program.cs              # Entry point, calls Widget.RunMessageLoop()
├── Widget/
│   └── Menu500Widget.cs    # ~70 lines, uses TaskbarWidget.Widget API
├── Services/
│   ├── MenuFetchService.cs # HTTP fetch and HTML parsing
│   └── StartupService.cs   # Windows startup registry
└── Models/
    └── DailyMenu.cs        # Menu data model
```

### Widget Toolkit (lib/taskbar-widget/)

The submodule is an immediate-mode Win32 GDI widget toolkit. Key files:

```
lib/taskbar-widget/src/TaskbarWidget/
├── Widget.cs                # Main entry point, orchestrates everything
├── WidgetOptions.cs         # Configuration
├── Color.cs                 # RGBA color struct
├── Rendering/               # RenderContext, layout engine, GDI renderer
├── Theming/                 # Dark/light theme detection
├── Interaction/             # Mouse tracking, tooltips, drop target
├── Ordering/                # Cross-process widget ordering
└── Timing/                  # SetInterval/SetTimeout via Win32 timers
```

### Widget System

Menu500Widget uses the high-level `Widget` API:
1. Creates `new Widget("Menu500", render: ctx => { ... })` with a render callback
2. Render callback draws "500" text and sets tooltip content
3. `widget.Show()` handles injection, positioning, and rendering
4. `Widget.RunMessageLoop()` runs the Win32 message loop
5. `widget.Invalidate()` triggers re-render when menu data updates

### Menu Fetching

- **URL**: https://www.500restaurant.cz/denni-menu/
- **Refresh interval**: 1 hour (via `System.Threading.Timer`)
- **Parsing**: Finds `<h4>` with Czech day name, extracts following `<p>` elements
- **Czech days**: pondělí, úterý, středa, čtvrtek, pátek

### Error Handling

- **Network failure**: Tooltip shows "Could not fetch menu: [error]"
- **Parse failure**: Tooltip shows "Could not parse menu: [error]"
- **Weekend**: Tooltip shows "Restaurant closed on weekends"
- Widget always displays "500" regardless of errors

## Gotchas

- **Platform required**: Use `-p:Platform=x64` for all build commands.
- **Submodule**: Must initialize submodule before building.
- **WndProc delegate**: The Widget class keeps a reference to its WndProc delegate to prevent GC collection.
- **Theme detection**: `ShouldSystemUseDarkMode()` is an undocumented uxtheme.dll export (#138). Wrapped in try/catch in ThemeDetector.
- **Namespace conflict**: In Menu500Tracker, use `TaskbarWidget.Widget.RunMessageLoop()` (fully qualified) because `Widget` also matches the local `Menu500Tracker.Widget` namespace.

## Releases

Version is derived from git tags. The GitHub Actions workflow automatically creates releases when a tag is pushed.

### How to Release

1. **Update CHANGELOG.md** with the new version section:
   ```markdown
   ## [v1.1.0] - YYYY-MM-DD

   ### Added
   - New feature description

   ### Changed
   - Changed behavior description

   ### Fixed
   - Bug fix description
   ```

2. **Commit the changelog**:
   ```bash
   git add CHANGELOG.md
   git commit -m "docs: update changelog for v1.1.0"
   git push
   ```

3. **Create and push the tag**:
   ```bash
   git tag v1.1.0
   git push origin v1.1.0
   ```

4. The workflow will automatically:
   - Build the exe and zip artifacts
   - Extract release notes from CHANGELOG.md for this version
   - Create a GitHub release with the artifacts and notes

### Changelog Format

Follow [Keep a Changelog](https://keepachangelog.com/) format:
- `### Added` - New features
- `### Changed` - Changes in existing functionality
- `### Deprecated` - Soon-to-be removed features
- `### Removed` - Removed features
- `### Fixed` - Bug fixes
- `### Security` - Security fixes

### Version Numbering

Follow [Semantic Versioning](https://semver.org/):
- **Major** (v2.0.0): Breaking changes
- **Minor** (v1.1.0): New features, backwards compatible
- **Patch** (v1.0.1): Bug fixes, backwards compatible

## CI/CD

### Workflows

- **CI** (`.github/workflows/ci.yml`): Runs on all pushes and PRs
  - Builds debug and release
  - Uploads portable exe artifact

- **Release** (`.github/workflows/release.yml`): Runs on version tags
  - Builds single-file exe and zip
  - Extracts changelog notes
  - Creates GitHub release with artifacts

## Commit Guidelines

Do not add `Co-Authored-By: Claude` or similar co-author lines to commits.
