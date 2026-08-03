# Steam Multi Runtime Targeting System Integration

This package connects the generic `com.koiusa.targetingsystem` runtime to Steam Multi Runtime's production input, local-player ownership, camera composition, and UI lifecycle. Generic selection, candidate policies, target indicators, Cinemachine group framing, and sample movement remain in the base package.

## Runtime composition

```text
System
└─ Gameplay System
   └─ Targeting System
      ├─ TargetMarkerRegistry
      ├─ LocalTargetingIndicatorPresenter
      └─ Target Indicator UI (inactive until a local controller is registered)

Local / Network Camera System
└─ Targeting Camera System
   ├─ LocalTargetingCameraConnector
   ├─ TargetingCameraGroupPresenter
   ├─ Targeting Target Group
   └─ Primary Centered Target Group
```

`PlayerTargetingOwner` registers only the resolved local owner's controller with `LocalTargetingControllerRegistry`. Camera and UI presenters subscribe to that registry and to `TargetingController.StateChanged`; they do not poll targeting state. The indicator samples screen positions only while selected moving targets are visible.

Targeting UI is prebuilt in `Targeting System.prefab` with serialized Panel Settings, UXML, and USS references. Runtime code activates the existing UI and does not assemble another UIDocument.

## Player and target setup

Production player prefabs contain:

1. `TargetingContextProvider`
2. `RegistryTargetCandidateSource`
3. `ViewportTargetPolicy`
4. `TargetingController`
5. `TargetingCommandInput`
6. `PlayerTargetingOwner`

Player and NPC target prefabs contain `TargetMarker`. Its `AimPoint` references the existing `ForcusTarget` object with `CameraTrackMarker`, so selection, camera framing, and UI markers use the same designer-adjustable point instead of the character root origin.

## Input

`SteamMultiRuntimeTargetingInputActions` adapts the shared production Input Actions configuration; it does not duplicate an `InputActionAsset`.

- Lock On toggles Single targeting.
- Previous / Next changes the primary target.
- Holding Strafe while Single targeting promotes it to temporary Multi targeting and adds targets progressively; releasing Strafe returns to the same primary Single target.
- Pressing Lock On while Multi targeting clears targeting.

Target membership, mode changes, and Showcase camera weight switches are callback-driven. `TargetingCameraPresenter` has no `Update` or interpolation coroutine. Continuous sampling is limited to movement, camera axes, marker position following, active camera anchors/groups, and held-action repeat where elapsed time is required. Camera anchors and primary-centered groups disable their player-loop work while targeting is inactive.

## Camera behavior

`CameraMixerWeightControllerBase` remains responsible for Default, Follow, Single, and Multi camera weights. `TargetingCameraGroupPresenter` updates LookAt and group members from state callbacks. The framing strategy can be changed between Primary Centered, Group Centered, and a custom `ITargetingCameraFramingGroup` without rewriting camera state logic.

Production Single and Multi cameras keep the player and selected targets inside the framed group. Primary Centered mode biases composition toward the primary target while retaining all selected targets. Camera poses are handed over during transitions to avoid abrupt jumps when entering or leaving targeting.

## Production assets and setup

```text
Assets/SteamMultiRuntime/Runtime/Prefabs/System/Gameplay System.prefab
Assets/SteamMultiRuntime/Runtime/Prefabs/Targeting/Targeting System.prefab
Assets/SteamMultiRuntime/Runtime/Prefabs/Camera/Targeting Camera System.prefab
```

```text
Tools/SteamMultiRuntime/Maintenance/Targeting/Install Production Setup
Tools/SteamMultiRuntime/Validation/Targeting/Validate Production Setup
```

The installer updates Local and Network player, NPC, camera, Gameplay System, and shared System prefabs through Unity's public Prefab API.

## Samples

Import `Targeting Showcase` for a prebuilt Cinemachine, production-input, UI Toolkit, random-spawner, and random-mover example. Regenerate it after changing the sample composition:

```text
Tools/SteamMultiRuntime/Maintenance/Targeting/Rebuild Showcase Sample
```

The base package's Basic sample remains a smaller standalone example without Steam Multi Runtime ownership or production input.
