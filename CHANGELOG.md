# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed
- Widget text color now adapts to Windows system theme using ShouldSystemUseDarkMode() API (white in dark mode, black in light mode)

## [v1.0.0] - 2026-02-03

### Added
- Initial release
- Taskbar widget displaying "500"
- Hourly menu fetch from 500restaurant.cz/denni-menu/
- Tooltip showing today's soup and main dish
- Weekend detection (shows "Restaurant closed on weekends")
- Error handling for network and parse failures
