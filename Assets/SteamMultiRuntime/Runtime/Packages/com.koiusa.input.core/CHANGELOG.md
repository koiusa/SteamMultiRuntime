# Changelog

All notable changes to this package will be documented in this file.

## [0.3.0] - 2026-08-28

### Changed

- Replaced the positional `UiNavigationInputSession` constructors with input-source, `UiNavigationInputHandlers`, and `UiNavigationInputOptions` arguments.

### Fixed

- Cleared held UI navigation repeat when the Navigate action is canceled by action disable, binding resolution, or device reset.

## [0.2.0] - 2026-08-21

### Added

- Added `InputActionPerformedTrigger` for connecting an Input Action to a serialized UnityEvent.
- Added an Editor action-path selector and validation for the trigger.

## [0.1.0] - 2026-08-21

### Added

- Added reusable Input System configuration, action lease, and UI navigation utilities.
