# Changelog

All notable changes to Steam Multi Runtime will be documented in this file.

## [0.12.1] - 2026-08-22

### Fixed

- Updated `com.koiusa.input.icons` to 0.1.1 and removed invalid `L-nan -nan` segments from the bundled Steam Gamepad SVG so Unity Vector Graphics imports it successfully.

## [0.12.0] - 2026-08-22

### Added

- Added the independently publishable `com.koiusa.input.icons` package for Input System control-path icons.
- Added the independently publishable `com.koiusa.inputguide` package for operation guides, device visualization, and live input highlighting.

### Changed

- Reorganized `com.koiusa.keyconfig` around the public `KeyConfigPanel` and `KeyConfigController` APIs with stable GUID-based binding identifiers.
- Moved key-binding persistence into the SteamMultiRuntime integration package instead of fixing storage policy in the reusable package.
- Updated reusable-package validation and publication order for the new input packages.

### Fixed

- Prevented a single physical trigger event from advancing multiple composite rebind parts by immediately committing the first matching control event and excluding aliases observed in that event.
- Preserved whole-composite restoration for Escape and timeout cancellation.

## [0.11.0] - 2026-08-21

### Added

- Added the independently publishable `com.koiusa.application` package for quit requests and application lifecycle notifications.
- Added the generic `InputActionPerformedTrigger` to `com.koiusa.input.core`.
- Added bundled, non-public `com.koiusa.editor-tools` for reusable Unity Editor diagnostics.

### Changed

- Connected the production System prefab's `System/GameQuit` action to `GameQuitter.RequestQuit()` through a serialized UnityEvent.
- Updated bundled package manifests to consume `com.koiusa.input.core` 0.2.0.
- Made `main` pushes publish in dependency order, while retaining manual dry-run and release options; releases are idempotent and use Node.js 22.
- Restricted npm and UPM archives to distributable package content so local IDE state is excluded.
- Switched automated npm publication entirely to OIDC Trusted Publishing and removed `NPM_TOKEN` usage.
- Moved reusable-package publishing into a local composite action shared by `release.yml` and the dry-run workflow, while keeping `release.yml` as the trusted publishing identity.
