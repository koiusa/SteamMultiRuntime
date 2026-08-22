# Current implementation state

## Input and menus

- `System/DebugInputGuideToggle`: F1 or DualShock touchpad.
- `System/CharacterDebugToggle`: F2 or L3 double-click. The L3 binding uses `MultiTap(tapCount=2)`.
- `System/DebugSessionMenuToggle`: F3 or Select/Share. Local mode opens Stage Select; Network mode opens Steam Lobby through the manager that exists in the active runtime.
- F1 Input Guide uses `InputActionBinding`; `InputGuideOverlay` owns live input/device visualization and `InputGuideOperationPanel` owns the binding-filtered operation lists. F2 Character Debug input is owned by `CharacterDebugToggleController`, while `NetworkNpcRandomSpawnManager` owns only its NPC group's visibility state.
- `UI/MenuToggle`: Tab or Start/Options. It opens `PauseMenuController`, whose choices are Key Config and Character Select.
- `UI/CharacterMenuToggle`: Backquote or C remains the direct Character Select keyboard shortcut. It has no gamepad Select/Share binding.
- `Adventure/CharacterSelectModifier`: gamepad button east (B/○).
- `Adventure/CharacterSelectDirection`: D-pad left/right. Hold B/○ and press left/right to cycle character models with wraparound.
- `Player/Next`: Keyboard 2, D-pad right, or R3. R3 prioritizes a Primary Target in the held right-stick screen direction and falls back to the next target when neutral or no directional candidate exists.

The pause controller is on the root `System` GameObject in `Assets/SteamMultiRuntime/Runtime/Resources/System/System.prefab`. Its runtime children include `PauseMenu` and `KeyConfigUiDocument`. `KeyConfigMenuToggle` is controlled by `PauseMenuController`, not directly by `UI/MenuToggle`.
The same root owns `ApplicationLifecycle` for push-based focus, application pause, and quitting notifications. `GameQuitter` owns the input-independent, duplicate-safe quit request. The generic `InputActionPerformedTrigger` in `com.koiusa.input.core` connects `System/GameQuit` to `GameQuitter.RequestQuit()` through a serialized UnityEvent without adding Input dependencies to `com.koiusa.application`.

## UI layout

- Steam Lobby uses three columns: left for stage/lobby management, center for lobby search/list, and right for lobby details/members.
- Refresh and Leave belong in the left management section with stage selection and lobby creation.
- The lobby list's flex chain must stretch to the bottom even when it has no items.
- Key Config attaches `KeyConfigDropdownPopup.uss` to the panel root so the dropdown popup list receives styling, and removes it when the view is disposed.
- Key ConfigのPanelは画面内の利用可能な高さを満たし、Binding Listは項目数に関係なくHeaderとButton Rowの間の残り領域を埋めます。
- Key Config shows the protected UI Action Map as a navigable tab and read-only row list; its Change and Reset controls remain disabled.
- Key Config PreviousSection/NextSection navigation cycles through Binding Group and every Action Map tab.
- Pause Menu, Character Select, Stage Select, Key Config, and Steam Lobby use `UiNavigationInputSession` for exclusive frontmost input, event-driven direction changes, held-input repeat, cursor visibility, and Input Action lifetime. Screen-specific controllers own focus transitions only and do not poll navigation in `Update`.
- `UiNavigationInputSession` leases UI Navigate／Submit／Cancelに加えてPoint／Click、Cursor visibility、Cursor lock stateを所有します。Menu表示中はPointer Actionを明示的に有効化してCursorを表示・解放し、最後のMenu closeで開始前のCursor状態を復元します。
- Wire aim cursorはgameplay中だけIMGUI描画し、Menuがsystem cursorを表示している間は`OnGUI`処理へ参加しません。
- Target Indicatorは表示専用で、Document Rootから動的Markerまで`PickingMode.Ignore`を使用し、前面MenuへのPointer入力を遮断しません。
- Those menus implement `IUiMenu`; `UiMenuNavigator` owns root opening, child Push, Back restoration, and CloseAll. Pause pushes Key Config and Character Select so closing either child restores Pause automatically.
- When a prior binding override path is null or empty, rebind reset must call `RemoveBindingOverride`; only non-empty paths may be passed to `ApplyBindingOverride`.
- Key ConfigのInteractive RebindはDualShock／DualSense固有の`leftTriggerButton`／`rightTriggerButton`を候補から除外し、L2／R2を対応する`leftTrigger`／`rightTrigger`として登録します。
- Key ConfigはPanelSettings、PanelTextSettings、Dynamic Font Assetを実行時に複製し、日本語Glyph／Atlas生成で配布元`Noto Sans JP SDF.asset`を変更しません。Font生成テストも一時Font Assetだけを変更します。

## Player, ownership, and guard

- Local Cameraの`LocalFocusMarkerContext`は`LocalPlayerProviderRegistry.CurrentChanged`を購読し、Cameraが`LocalManager`より先に有効化されてもProvider登録後にPlayer追従を開始します。
- Local and Network player-facing UI uses the typed `ILocalPlayerOwnership` snapshot contract. Consumers that react to transitions use `ILocalPlayerOwnershipNotifier.OwnershipChanged` and must not poll ownership every frame.
- `CharacterSelectShortcutController` is attached to both Local and Network user runtime profiles and applies the selected model through `PlayerModelProfileBase`.
- The Character Select shortcut is categorized under the Adventure action map; button east is also Dash in the Player map, so respect the active-map/input-routing behavior when changing it.
- `GuardShieldVisual` generates an Icosphere and uses `Koiusa/Effects/GuardShield` HLSL for a spherical, evenly sized grid.
- Recompute the shield center when Guard activates after the character model is available. Exclude the shield renderer itself from bounds calculation and avoid per-frame center recomputation, which causes jitter.
- Guard hit feedback uses `PlayerDamageRequest.Point`; environment intersection uses URP Scene Depth. Depth Texture must remain enabled for the relevant URP assets.

## NPC movement and presentation

- `NpcNavMeshController` selects the movement backend at startup. Crowd ON uses the shared Burst `NpcCrowdSimulation` / `NpcCrowdMotor` path; Crowd OFF uses the conventional Dynamic Rigidbody `ActorCompositeMotor` path. Runtime hot swap is unsupported.
- Crowd movement is evaluated centrally at 30 Hz, at most once per rendered frame. Ground and wall probes run at 15 Hz, while AI command and NavMesh observations use camera-distance LOD (10 / 5 / 2 Hz); Dedicated Server keeps 10 Hz.
- Local NPCs and authoritative Network Server NPCs run the selected movement backend. Remote Network Clients do not run NavMesh, AI, physics, or Crowd simulation and only present synchronized state.
- Crowd NPCs follow `IGroundMotionPhysicsPoseSource` platforms through typed push notifications while bound. Do not add per-NPC platform polling, execution-order attributes, or reflection.
- Crowd ON keeps its Rigidbody kinematic and its Collider as a query/attack trigger. Crowd OFF may opt into NPC-to-NPC PhysX collisions with `Enable Npc Rigidbody Collisions`; the default is OFF and changes apply on the next Play start.
- NPC spring rigs are registered with the central Burst `NpcCrowdSpringSimulation` for both backends. Its camera-distance update rates are 30 / 15 / 5 Hz, and only successfully registered rigs have their original Spring Manager updates disabled.
- Local and Network Server NPCs share `PhysicsPresentationSmoother` with Player presentation. `GroundMotionPresentationScheduler` applies platform presentation before actor presentation; remote Network Clients present `NetworkTransform` interpolation instead.

## Architecture and removals

- Local Stage Selectの切替処理はUIの自動Closeではキャンセルせず、新StageをActiveにした後で旧StageをUnloadするまで継続します。
- Player skills are coordinated through `PlayerCharacterCoordinator`; Network requests cross RPC boundaries and resolve authoritatively on the server.
- Player and local/authoritative NPC presentation share the same interpolation contract. Moving-platform work must preserve the typed push path and must not add execution-order attributes or per-NPC platform polling that scales poorly.
- `Documentation/PackageArchitecture.md` is the source of truth for typed package connections and the project's no-reflection policy.
- The Build Profile creation/editor tool and its sample, presets, and documentation are removed. Do not reference it as a supported tool.
- `AnimationEventReceiverVisualizerWindow` may enumerate Animation Event receivers via reflection because the Unity feature itself resolves named receiver methods dynamically.

Use `Documentation/InputBindings.md`, `Documentation/PlayerGameplayArchitecture.md`, `Documentation/CharacterArchitecture.md`, and `Documentation/SessionArchitecture.md` as the maintained human-facing sources.
