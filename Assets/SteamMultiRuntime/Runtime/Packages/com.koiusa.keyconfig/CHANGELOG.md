# Changelog

## [0.2.0]

- Replaced the implementation-oriented public surface with `KeyConfigPanel`, `KeyConfigController`, stable GUID-based binding DTOs, `KeyConfigSettings`, and `KeyConfigIconSet` under `Koiusa.KeyConfig`.
- Internalized rebinding, persistence, input monitoring, and UI implementation services.
- Merged the menu toggle responsibility into `KeyConfigPanel` and changed programmatic persistence to JSON import/export.

All notable changes to this package will be documented in this file.

## [Unreleased]

### Changed

- Reduced `KeyConfigView` to the UI facade and moved binding-group state, binding catalog rendering, cross-section navigation, and dropdown popup stylesheet lifetime into focused internal components without changing the existing `KeyConfigPanel` API.

## [0.1.40] - 2026-08-22

### Fixed

- Displays every modifier and button icon for one- and two-modifier composite bindings instead of showing only the representative button icon.

## [0.1.39] - 2026-08-22

### Fixed

- Keeps section navigation blocked through the remainder of the Input System update that completes an L1/R1 rebind, then evaluates release state from the UI scheduler so the completing press cannot move tabs.
- Replaced parallel pending-rebind fields, edit-session flags, and scheduler flags with nullable state objects and owned UI schedule items.

## [0.1.38] - 2026-08-22

### Changed

- Alias suppression now records every control changed by the observed state event without relying on Gamepad, Joystick, button, axis, or `trigger` control-name heuristics.
- Replaced `KeyConfigPanel.Update` polling with Input Action release callbacks and coalesced State／DeltaState event-driven input-monitor refreshes while retaining the L1/R1 completion-press guard.

### Tests

- Exercises alias collection through queued Input System state events and verifies that `TEXT` events are ignored safely.

## [0.1.37] - 2026-08-21

### Fixed

- Blocks PreviousSection and NextSection callbacks from reusing the L1/R1 press that completed rebinding, then restores tab navigation after those actions are released.

## [0.1.36] - 2026-08-21

### Fixed

- Removed control-release waiting between composite parts. Alias paths are collected from the state event that completes the part, observation ends immediately, and the next part starts without depending on Gamepad or Joystick release values.

## [0.1.35] - 2026-08-21

### Changed

- Restored the five-second interactive rebind timeout for periods where no input is received. Alias matching and composite-part transitions remain event-driven and have no timer.

## [0.1.34] - 2026-08-21

### Fixed

- Ignores non-state Input System events such as `TEXT` before enumerating changed controls, preventing `ArgumentException` during text input.

## [0.1.33] - 2026-08-21

### Changed

- Removed the timed alias-collection window. Alias collection now follows Input System events from the selected press until that selected control's release event, then immediately starts the next composite part.
- Removed the interactive rebind timeout; rebinding now ends only through completion, Escape, or explicit cancellation.

## [0.1.32] - 2026-08-21

### Changed

- Simplified alias suppression to consume Input System changed-control events directly. Removed device scans, baseline snapshots, default-value comparisons, analog-release checks, and their related branches.

## [0.1.31] - 2026-08-21

### Fixed

- Replaced the inter-part release gate with a bounded alias-collection window. It records changed button and trigger paths for 150 ms, excludes them from later parts, and then always starts the next part without waiting for analog controls to report a released state.

## [0.1.30] - 2026-08-21

### Fixed

- During a composite-part press, records every button or trigger control that changed from its pre-part baseline and excludes only those paths from later parts.
- Waits only for the control selected by the completed part to be released. Other controls and device families no longer block registration, while aliases such as L2 and `leftTriggerButton` from the same physical press cannot fill separate parts.

## [0.1.29] - 2026-08-21

### Changed

- Consolidated duplicate Submit-release checks in `KeyConfigPanel` into one state transition.
- Consolidated the repeated rebind completion, cancellation, and failure UI cleanup path.

## [0.1.28] - 2026-08-21

### Changed

- The inter-part gate now snapshots button and trigger values immediately before each part starts, then waits for those controls to return to their captured baseline. This handles non-zero Joystick defaults without device names or hard-coded aliases and does not let unrelated persistent HID state block rebinding.

## [0.1.27] - 2026-08-21

### Changed

- Replaced device-family locks, hard-coded trigger aliases, and forced release-timeout continuation with one neutral-state gate between composite parts.
- The gate waits until all button and trigger controls are neutral for a stable interval, using each axis control's configured default value. This handles duplicate logical controls, delayed HID reports, Joystick `-1` defaults, and analog noise through one rule.
- Release-wait timeout and Escape both cancel and restore the original composite binding.

## [0.1.26] - 2026-08-21

### Fixed

- Uses a tolerance around an axis control's configured default value when waiting for release, supporting DualShock trigger noise instead of requiring an exact state match.
- Limits inter-part release waiting to one second and continues to the next part on release-wait timeout rather than canceling the entire composite rebind. Escape still cancels normally.

## [0.1.25] - 2026-08-21

### Fixed

- Removed whole-device-family locking during composite rebinding. Some controllers expose different physical buttons through different `Gamepad` and `Joystick` logical devices, so later parts now exclude only the previously selected control and its same-side trigger aliases.

## [0.1.24] - 2026-08-21

### Fixed

- Waits only for the control actually selected by the completed rebind part to return to its Input System default state. This avoids treating idle Joystick trigger axes whose default value is `-1` as permanently held.
- Explicitly excludes the selected control's left/right trigger aliases from later composite parts.

## [0.1.23] - 2026-08-21

### Fixed

- Locks sequential gamepad composite rebinding to the Input System device family selected by its first part. A `Gamepad` selection excludes duplicate `Joystick` controls from later parts and vice versa, preventing delayed L2/R2 aliases from being registered as separate keys.

## [0.1.22] - 2026-08-21

### Fixed

- Defers alias-control capture until Input System has processed every event in the completion frame, preventing a second logical `Joystick` trigger notification from filling the next composite part after a `Gamepad` L2/R2 notification.

## [0.1.21] - 2026-08-21

### Changed

- Extracted physical-control alias collection and inter-part release waiting from `InputRebindController` into `RebindControlReleaseGate` without changing rebinding behavior.
- Limited inter-part release tracking to buttons and trigger axes, so resting stick drift cannot block the next gamepad rebind part.

## [0.1.20] - 2026-08-21

### Fixed

- Waits for every control actuated by the previous physical press to be released before starting the next composite-rebind part, preventing HID aliases such as `rightTriggerButton` from following R2 into the next part.
- Escape and timeout cancellation remain active while waiting for release between parts.

## [0.1.19] - 2026-08-21

### Fixed

- Excluded every control actuated by the previous physical press from later composite-rebind parts, preventing one L2/R2 press exposed as both Gamepad trigger and Joystick Trigger from filling two parts.

## [0.1.18] - 2026-08-21

### Fixed

- Preserved the modifier-button column after adding or removing a modifier instead of forcing focus to Change.
- Blocked UI Submit until the physical Submit input is released after rebinding, preventing Enter or gamepad A from moving focus out of the binding list when chosen as the new binding.

## [0.1.17] - 2026-08-21

### Fixed

- Horizontal gamepad navigation now skips disabled modifier buttons in the requested direction and wraps across the row instead of returning focus to the current button.

## [0.1.16] - 2026-08-21

### Fixed

- Kept Change available for every rebindable row even when its current device is disconnected, allowing migration to another device.
- Made deferred rebinding reliably start after UI Submit is released instead of depending solely on a potentially missed `canceled` callback.

## [0.1.15] - 2026-08-21

### Fixed

- Based Change and modifier-button availability on the connected device layout rather than requiring an exact control-path match, including derived gamepad layouts and non-standard controls.
- Refreshes row availability when Input System devices are connected, disconnected, enabled, or disabled while Key Config is open.

## [0.1.14] - 2026-08-21

### Fixed

- Disabled modifier add/remove controls together with Change when the binding's required device is not connected.

## [0.1.13] - 2026-08-21

### Changed

- Exposed the logical modifier count on binding entries so row controls no longer infer structure by parsing localized display text.
- Isolated ordered logical-binding replacement construction from the mutation step.

## [0.1.12] - 2026-08-21

### Fixed

- Preserved binding order and IDs when adding or removing modifiers from one of several bindings on the same action, so the clicked row remains the changed row.

## [0.1.11] - 2026-08-21

### Changed

- Split input activity monitoring, dynamic row localization, and fallback UI construction out of `KeyConfigView` without changing its API or behavior.

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
