# Changelog

All notable changes to this package will be documented in this file.

## [0.1.0] - 2026-08-21

### Added

- Added input-independent `GameQuitter.RequestQuit()` and `QuitRequested` APIs.
- Added an Editor bridge that exits Play Mode for quit requests.
- Added duplicate quit-request suppression and `GameQuitter.IsQuitRequested`.
- Added `ApplicationLifecycle` focus, pause, and quitting state notifications.
