# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Menu 500 Tracker is a Windows taskbar widget that displays "500" and shows today's daily menu from Restaurant 500 (500restaurant.cz) in a tooltip on hover. The menu is fetched hourly from the restaurant's website.

## Build Commands

```bash
# Build the solution
dotnet build -p:Platform=x64

# Build release
dotnet build --configuration Release -p:Platform=x64

# Run the app (x64)
dotnet run --project src/Menu500Tracker/Menu500Tracker.csproj -p:Platform=x64

# Publish single-file exe (x64)
dotnet publish src/Menu500Tracker/Menu500Tracker.csproj --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:PublishSingleFile=true -p:WindowsPackageType=None -o publish
```

## Architecture

### Solution Structure

- **Menu500Tracker** (`src/Menu500Tracker/`) - WinUI 3 app with taskbar widget
- **TaskbarWidget** (`lib/taskbar-widget/`) - Git submodule for taskbar widget injection

After cloning, initialize the submodule:
```bash
git submodule update --init --recursive
```

### Key Components

```
src/Menu500Tracker/
├── Program.cs              # Entry point with ComWrappers init
├── App.xaml.cs             # Creates widget and menu service
├── MainWindow.xaml.cs      # Hidden window (WinUI lifecycle requirement)
├── Widget/
│   ├── Menu500Widget.cs           # Injection orchestrator
│   └── Menu500WidgetContent.xaml  # Visual UI showing "500" text
├── Services/
│   └── MenuFetchService.cs        # HTTP fetch and HTML parsing
└── Models/
    └── DailyMenu.cs               # Menu data model
```

### Widget System

The widget uses `TaskbarInjectionHelper` from the submodule:
1. Creates a host window with `DeferInjection=true`
2. Sets up `DesktopWindowXamlSource` for WinUI content
3. Injects into taskbar after XAML setup
4. Displays "500" with tooltip showing today's menu

### Menu Fetching

- **URL**: https://www.500restaurant.cz/denni-menu/
- **Refresh interval**: 1 hour
- **Parsing**: Finds `<h4>` with Czech day name, extracts following `<p>` elements
- **Czech days**: pondělí, úterý, středa, čtvrtek, pátek

### Error Handling

- **Network failure**: Tooltip shows "Could not fetch menu: [error]"
- **Parse failure**: Tooltip shows "Could not parse menu: [error]"
- **Weekend**: Tooltip shows "Restaurant closed on weekends"
- Widget always displays "500" regardless of errors

## Gotchas

- **Platform required**: WinUI 3 requires explicit platform. Use `-p:Platform=x64` for all commands.
- **Hidden MainWindow**: Don't call `Activate()` on MainWindow - it must stay hidden for widget-only mode.
- **Submodule**: Must initialize submodule before building.

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
