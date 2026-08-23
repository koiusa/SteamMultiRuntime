# Changelog

## Unreleased

## [0.3.0] - 2026-08-23

- Updated the `com.koiusa.keyconfig` dependency to 0.2.3 and declared the existing direct `com.koiusa.ui.core` dependency.
- Renamed `InputGuidePanelKind` to `InputGuidePanelSlot` to distinguish visual panel slots from extensible device layout IDs.
- Merged the Mouse layout into the Device panel and reduced the fixed panel slots to Device and Operations.
- Kept the primary device card and additional Mouse/XR panels visually separate with independent component anchors.
- Removed Mouse-specific runtime routing; every device layout now selects its destination host through serialized configuration.
- Fixed the initial operation list using Keyboard presentation until the first input when only a Gamepad is available.
- Added optional Input System usage matching and simultaneous activation of all matching non-exclusive device layouts for paired XR controllers.

- Added ordinary `InputGuidePanelLayout` components for the primary Device view, Mouse view, and Operations panel. Multiple components may share the Device slot and its runtime-switchable nine-direction anchor.
- Applied Play Mode Inspector anchor changes immediately without reopening or toggling the target panel.
- Added `InputGuidePanelCollection` as the validated panel list referenced by `InputGuideOverlay`.
- Added an intuitive 3-by-3 anchor picker to the `InputGuideOverlay` Inspector.
- Removed the redundant presentation component, interface, and profile; panel layouts now own all replaceable UXML assets directly.
- Reduced the public runtime surface to `IInputGuideOverlay`; panel build, refresh, collection lookup, and mutable component setters are internal.
- Added extensible string-ID device layouts with target panels, default/override UXML, Input System layout matching, visibility, and exclusive groups.
- Fixed rebuilt operation lists showing keyboard and gamepad sections simultaneously.

All notable changes to this package are documented in this file.

## [0.2.1] - 2026-08-22

### Changed

- Updated the `com.koiusa.keyconfig` dependency to 0.2.2.
- Bound Input Action change observation and deferred binding refreshes to the UI Toolkit Panel attach/detach lifecycle.

### Fixed

- Fixed Play Mode exit exceptions when navigation bindings were released after the Input Guide Panel had detached.

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
- Split presentation, selection, and navigation responsibilities between `InputGuideOverlay`, `InputGuideSelectionController`, and `InputGuideNavigationController`.
- `All` now enumerates every Action Map; compact mode presents multiple maps one at a time as tabs.
- The compact preset hides the fixed `F1 / TOUCH PAD` hint by default.
- The library no longer assigns `UIDocument.sortingOrder`.
- Replaced the previous Input Guide public configuration surface with `IInputGuideOverlay`, `InputGuideConfiguration`, and top-level enums.

### Removed

- Removed the legacy selection-provider components and legacy nested display/configuration APIs.

### Breaking changes

- The 0.1.x public API is not source-compatible with 0.2.0. Migrate presentation changes to `IInputGuideOverlay.ApplyConfiguration()` and map selection to `InputGuideSelectionController.ApplySelection()`.
