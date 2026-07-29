# Current implementation state

## Input and menus

- `System/DebugInputGuideToggle`: F1 or DualShock touchpad.
- `System/CharacterDebugToggle`: F2 or L3 double-click. The L3 binding uses `MultiTap(tapCount=2)`.
- `System/DebugSessionMenuToggle`: F3 or Select/Share. Local mode opens Stage Select; Network mode opens Steam Lobby through the manager that exists in the active runtime.
- `UI/MenuToggle`: Tab or Start/Options. It opens `PauseMenuController`, whose choices are Key Config and Character Select.
- `UI/CharacterMenuToggle`: Backquote or C remains the direct Character Select keyboard shortcut. It has no gamepad Select/Share binding.
- `Adventure/CharacterSelectModifier`: gamepad button east (B/○).
- `Adventure/CharacterSelectDirection`: D-pad left/right. Hold B/○ and press left/right to cycle character models with wraparound.

The pause controller is on the root `System` GameObject in `Assets/SteamMultiRuntime/Runtime/Resources/System/System.prefab`. Its runtime children include `PauseMenu` and `KeyConfigUiDocument`. `KeyConfigMenuToggle` is controlled by `PauseMenuController`, not directly by `UI/MenuToggle`.

## UI layout

- Steam Lobby uses three columns: left for stage/lobby management, center for lobby search/list, and right for lobby details/members.
- Refresh and Leave belong in the left management section with stage selection and lobby creation.
- The lobby list's flex chain must stretch to the bottom even when it has no items.
- Key Config attaches `KeyConfigDropdownPopup.uss` to the panel root so the dropdown popup list receives styling, and removes it when the view is disposed.
- Key Config shows the protected UI Action Map as a navigable tab and read-only row list; its Change and Reset controls remain disabled.
- Pause Menu, Character Select, Stage Select, Key Config, and Steam Lobby use `UiNavigationInputSession` for exclusive frontmost input, event-driven direction changes, held-input repeat, cursor visibility, and Input Action lifetime. Screen-specific controllers own focus transitions only and do not poll navigation in `Update`.
- When a prior binding override path is null or empty, rebind reset must call `RemoveBindingOverride`; only non-empty paths may be passed to `ApplyBindingOverride`.

## Player, ownership, and guard

- Local and Network player-facing UI uses the typed `ILocalPlayerOwnership` contract.
- `CharacterSelectShortcutController` is attached to both Local and Network user runtime profiles and applies the selected model through `PlayerModelProfileBase`.
- The Character Select shortcut is categorized under the Adventure action map; button east is also Dash in the Player map, so respect the active-map/input-routing behavior when changing it.
- `GuardShieldVisual` generates an Icosphere and uses `Koiusa/Effects/GuardShield` HLSL for a spherical, evenly sized grid.
- Recompute the shield center when Guard activates after the character model is available. Exclude the shield renderer itself from bounds calculation and avoid per-frame center recomputation, which causes jitter.
- Guard hit feedback uses `PlayerDamageRequest.Point`; environment intersection uses URP Scene Depth. Depth Texture must remain enabled for the relevant URP assets.

## Architecture and removals

- Player skills are coordinated through `PlayerCharacterCoordinator`; Network requests cross RPC boundaries and resolve authoritatively on the server.
- Local and NPC presentation should share the same interpolation contract, but moving-platform work must not add execution-order attributes or per-NPC platform polling that scales poorly.
- `Documentation/PackageArchitecture.md` is the source of truth for typed package connections and the project's no-reflection policy.
- The Build Profile creation/editor tool and its sample, presets, and documentation are removed. Do not reference it as a supported tool.
- `AnimationEventReceiverVisualizerWindow` may enumerate Animation Event receivers via reflection because the Unity feature itself resolves named receiver methods dynamically.

Use `Documentation/InputBindings.md`, `Documentation/PlayerGameplayArchitecture.md`, `Documentation/CharacterArchitecture.md`, and `Documentation/SessionArchitecture.md` as the maintained human-facing sources.
