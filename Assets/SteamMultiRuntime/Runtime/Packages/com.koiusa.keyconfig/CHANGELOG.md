# Changelog

All notable changes to this package will be documented in this file.

## [0.1.10] - 2026-08-21

### Fixed

- Preserved the binding-list scroll position when a row action rebuilds the current Action Map; switching Action Maps still starts at the top.

## [0.1.9] - 2026-08-21

### Fixed

- Prevented a control chosen for an earlier modifier-composite part from being captured again by the next sequential rebind step while it is still held.

## [0.1.8] - 2026-08-21

### Added

- Added per-row conversion between single bindings, `ButtonWithOneModifier`, and `ButtonWithTwoModifiers`.
- Added backward-compatible structural persistence so modifier additions and removals survive restart.
- Reset now restores both the original paths and the original binding structure.
- Added Japanese and English text for modifier controls and status messages; the SteamMultiRuntime localization catalog includes the same keys.
- Shortened modifier controls to `＋` / `－`, halved their width, added localized tooltips, and ordered them before Change and Reset.

## [0.1.7] - 2026-08-21

### Added

- Added first-class `ButtonWithOneModifier` display, sequential rebinding, persistence, reset, cancellation, and whole-combination conflict detection.
- Added modifier-composite support to the input guide operation list.

### Compatibility

- Existing single bindings and public APIs remain supported.
- Left/right Ctrl, Shift, and Alt paths remain unchanged in override JSON, while display and conflict comparison normalize their side.

## [0.1.6] - 2026-08-21

### Changed

- Updated the `com.koiusa.input.core` dependency to 0.2.0.

## [0.1.5] - 2026-08-21

### Added

- Added the reusable runtime key-rebinding UI, persistence, localization, and input-guide assets.
