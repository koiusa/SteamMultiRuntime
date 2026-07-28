# Steam Multi Runtime Targeting System Integration

This package owns Steam Multi Runtime-specific targeting adapters. Generic target detection,
lock-on, camera, input, UI, and sample code remain in `com.koiusa.targetingsystem`.

`SteamMultiRuntimeTargetingInputActions` adapts the project's shared `InputActionsConfig` to the
generic targeting input contract. Add only Steam Multi Runtime-specific adapters here; do not
duplicate generic runtime code.

Create the adapter from `Koiusa > Steam Multi Runtime > Targeting > Input Actions`, assign the
project's shared `InputActionsConfig`, and reference the adapter from the generic targeting input
components. The SteamMultiRuntime project provides `GameplayTargetingInputActions` as its default
configured asset.

This adapter does not contain or duplicate an `InputActionAsset`. Production input remains defined
only once in `SteamMultiRuntime_InputActions.inputactions` through `GameplayInputActionsConfig`.

The default project asset maps solo lock-on, previous/next selection, and look input to the current
gameplay actions. Multi-lock, clear, bulk-lock, and focus remain unassigned until dedicated actions
are added to the project's Input Action Asset; leaving a path empty disables that command safely.

Use `Koiusa > Steam Multi Runtime > Targeting > Validate Production Input` to verify every configured
action path and select the production adapter asset. To exercise targeting behavior, open the generic
TargetingSystem sample scene, replace its Targeting Camera Rig input config references with the
production adapter, and enter Play Mode. Do not save that override back into the generic sample.
