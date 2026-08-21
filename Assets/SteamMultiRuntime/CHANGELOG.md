# Changelog

All notable changes to Steam Multi Runtime will be documented in this file.

## [0.11.0] - 2026-08-21

### Added

- Added the independently publishable `com.koiusa.application` package for quit requests and application lifecycle notifications.
- Added the generic `InputActionPerformedTrigger` to `com.koiusa.input.core`.
- Added bundled, non-public `com.koiusa.editor-tools` for reusable Unity Editor diagnostics.

### Changed

- Connected the production System prefab's `System/GameQuit` action to `GameQuitter.RequestQuit()` through a serialized UnityEvent.
- Updated bundled package manifests to consume `com.koiusa.input.core` 0.2.0.
