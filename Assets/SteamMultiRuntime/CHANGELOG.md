# Changelog

All notable changes to Steam Multi Runtime will be documented in this file.

## [0.12.3] - 2026-08-22

### Added

- Updated `com.koiusa.inputguide` to 0.2.0 with the official compact operations panel, Action Map tabs, selection controls, and input navigation.
- Added `InputGuideSelectionController` and `InputGuideNavigationController` to the official Input Guide Prefab.

### Changed

- Updated the application version to 0.10.5 and Android version code to 4.
- Updated `com.koiusa.steammultiruntime.keyconfig` to 0.1.5 and aligned its Keyconfig dependency to 0.2.1.
- Reused `UI/PreviousSection`, `UI/NextSection`, and `UI/Navigate` for compact Input Guide navigation.

### Fixed

- Fixed compact operation-row overlap and `All` displaying only the Player map.
- Added compact scrollbar styling and shortcut scrolling.
- Preserved caller-owned `UIDocument.sortingOrder` and applied Overlay Inspector changes during Play Mode.
- Stopped Input Guide binding refresh callbacks at UI Toolkit Panel detachment so Play Mode teardown can release navigation actions without an Overlay exception.
- Kept Keyconfig text Materials persistent across Play Mode teardown so deferred UI Toolkit text jobs cannot observe a destroyed runtime Material.

## [0.12.2] - 2026-08-22

### Changed

- Updated `com.koiusa.keyconfig` to 0.2.1.
- Made `Documentation/KeyConfigArchitecture.md` the single source of truth for Keyconfig class and public-interface diagrams, with package versions sourced from their manifests.

### Fixed

- Excluded DualShock/DualSense `leftTriggerButton` and `rightTriggerButton` HID aliases from interactive rebinding so L2/R2 resolve to the corresponding analog trigger.
- Isolated UI Toolkit dynamic Japanese font generation in runtime-created assets so opening or testing Keyconfig no longer dirties the packaged `Noto Sans JP SDF.asset`.

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
