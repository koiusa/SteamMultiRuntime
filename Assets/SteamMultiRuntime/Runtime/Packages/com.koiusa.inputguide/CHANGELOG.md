# Changelog

All notable changes to this package are documented in this file.

## [0.2.0] - 2026-08-22

### Added

- Added the official `CompactOperations` layout preset: a dark translucent, bordered and rounded operation panel aligned to the upper right at approximately 440px wide.
- Added compact Action Map tabs, vertical scrolling, and a package-owned compact scrollbar style.
- Added `InputGuideSelection`, `InputGuideSelectionController`, and an Inspector mask for selecting multiple Action Maps by stable map name.
- Added Binding Group selection sourced from the referenced Overlay's Input Actions Config.
- Added `InputGuideNavigationController` for previous/next Map navigation and operation-list scrolling. The official Prefab defaults to `UI/PreviousSection`, `UI/NextSection`, and `UI/Navigate`.
- Added public configuration capture/apply APIs, toggle-hint visibility control, Map-tab navigation, and operation-list scrolling.
- Added Play Mode Inspector refresh for Overlay presentation and Input Actions changes.
- Added Editor tests covering presentation presets, selection normalization, map filtering, tab navigation, and `UIDocument.sortingOrder` ownership.

### Changed

- Updated the `com.koiusa.keyconfig` dependency to 0.2.1.
- Bound Input Action change observation and deferred binding refreshes to the UI Toolkit Panel attach/detach lifecycle, preventing teardown notifications from targeting a detached `UIDocument`.
- Split presentation, selection, and navigation responsibilities between `InputGuideOverlay`, `InputGuideSelectionController`, and `InputGuideNavigationController`.
- `All` now enumerates every Action Map; compact mode presents multiple maps one at a time as tabs.
- The compact preset hides the fixed `F1 / TOUCH PAD` hint by default.
- The library no longer assigns `UIDocument.sortingOrder`.
- Replaced the previous Input Guide public configuration surface with `IInputGuideOverlay`, `InputGuideConfiguration`, and top-level enums.

### Fixed

- Fixed Play Mode exit exceptions when navigation bindings were released after the Input Guide Panel had detached.

### Removed

- Removed the legacy selection-provider components and legacy nested display/configuration APIs.

### Breaking changes

- The 0.1.x public API is not source-compatible with 0.2.0. Migrate presentation changes to `IInputGuideOverlay.ApplyConfiguration()` and map selection to `InputGuideSelectionController.ApplySelection()`.
