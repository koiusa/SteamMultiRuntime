# Current Class Structure

この文書は、現在のSteamMultiRuntime固有Runtime実装について、クラスの配置、責務、所有関係、処理の流れをまとめたものです。

詳細仕様は領域別の文書へ分離しています。

- Traversalの責務、状態、設定、依存規則: [TraversalArchitecture.md](TraversalArchitecture.md)
- NPCのModule構成、Local／Network駆動: [NpcArchitecture.md](NpcArchitecture.md)
- Character Model、Profile、選択UI: [CharacterArchitecture.md](CharacterArchitecture.md)
- Steam Lobby、Stage選択、Scene遷移: [SessionArchitecture.md](SessionArchitecture.md)
- Editor拡張: [EditorSpecification.md](EditorSpecification.md)

Unity、Netcode for GameObjects、Input Systemなどの外部実装、Editor専用クラス、Sample、Thirdpartyの詳細は対象外です。

## 全体構成

```text
SteamMultiRuntime
├─ Input
│  ├─ InputActionsConfig
│  ├─ PlayerGameplayInputReader
│  ├─ PlayerInputState
│  └─ IPlayerInputSource
│
├─ Player Locomotion
│  ├─ Controller
│  │  ├─ LocalPlayerController
│  │  └─ ServerDrivenPlayerController
│  ├─ PlayerCompositeMotor
│  ├─ PlayerMotor
│  └─ PlayerTraversalCoordinator
│
├─ Traversal
│  ├─ Wall
│  ├─ Ladder
│  └─ Wire
│
├─ NPC
│  ├─ NpcNavMeshController
│  ├─ NavMesh機能モジュール
│  └─ AiPlayerInputSource
│
├─ Character
│  ├─ Runtime User Profile
│  ├─ Model Sync
│  └─ Character Prefab Loader
│
├─ Presentation
│  ├─ PlayerAnimatorStateDriver
│  ├─ Camera Mixer / Focus Marker
│  └─ Player Name / Loading UI
│
└─ Session
   ├─ Steam Lobby
   └─ Scene Flow
```

## Player Locomotion

### クラス構成

```text
IPlayerController
├─ LocalPlayerController
│  ├─ PlayerGameplayInputReader : IPlayerInputSource
│  └─ PlayerCompositeMotor
│
├─ ServerDrivenPlayerController : NetworkBehaviour
│  ├─ IPlayerInputSource
│  │  ├─ PlayerGameplayInputReader（Network Player）
│  │  └─ AiPlayerInputSource（Network NPC）
│  ├─ PlayerCompositeMotor
│  └─ Network同期状態
│     ├─ PlayerInputSyncState
│     ├─ PlayerKinematicState
│     ├─ PlayerMovementFlagsState
│     └─ WireSwingNetworkState
│
└─ NpcPlayerControllerAdapter（Local NPCのみ）
   └─ NpcNavMeshController : INpcLocomotionState

Network NPCではServerDrivenPlayerControllerだけがIPlayerControllerを実装し、
NpcNavMeshControllerはNPC固有の状態契約だけを公開する。

PlayerCompositeMotor : IPlayerMoveInputReceiver
├─ PlayerMotor : IPlayerMotor
└─ PlayerTraversalCoordinator
   ├─ IPlayerTraversalCoordinator
   └─ ITraversalIntentContext
```

ControllerからTraversalまでの入力経路と各クラスの詳細責務は、[TraversalArchitecture.md](TraversalArchitecture.md)に集約しています。

## Traversal

Traversalは`Wall`、`Ladder`、`Wire`の3領域で構成され、共有状態を持つ`Feature`と具体動作を行う`Action`に分かれます。クラス階層、各Actionの責務、状態、設定値、Netcode、Prefab規則は[TraversalArchitecture.md](TraversalArchitecture.md)を正本とします。

## NPC

NPCはNavMesh Moduleの判断を`AiPlayerInputSource`へ変換し、Player用Motorを再利用します。Moduleの責務、Local／Network経路、Server所有契約は[NpcArchitecture.md](NpcArchitecture.md)を正本とします。

## Character ModelとProfile

Profile、Model Sync、Prefab Loader、Character選択UIの詳細は[CharacterArchitecture.md](CharacterArchitecture.md)を正本とします。

## LobbyとScene Flow

Steam Lobby、Local Stage選択、Dedicated Server、Scene Loader、Loading Splashの詳細は[SessionArchitecture.md](SessionArchitecture.md)を正本とします。

## Animatorと表示

```text
PlayerAnimatorStateDriver : IPlayerAnimatorStateDriver
├─ IPlayerController
├─ IPlayerTraversalCoordinator
└─ Animator

Camera
├─ CameraMixerWeightControllerBase
│  ├─ LocalCameraMixerWeightController
│  └─ LobbyCameraMixerWeightController
├─ IFocusMarkerContext
│  ├─ LocalFocusMarkerContext
│  └─ NetworkFocusMarkerContext
├─ ForcusMerker
├─ CameraTrackMarker
└─ FocusMarkerUtility
```

`PlayerAnimatorStateDriver`は入力を再判定せず、ControllerとCoordinatorが公開する確定済み状態をAnimatorパラメータへ変換します。

## Runtime全体の境界

現在の構成では、以下を境界として扱います。

1. Input Systemを扱うのは入力Readerまで
2. NPCはPlayer用入力・Motor・Animator経路を再利用する
3. Network物理はServer Authorityとする
4. AnimatorとUIは確定済み状態を表示し、ゲーム状態を決定しない

Controller、Coordinator、Feature、Action間の詳細な依存規則はTraversal設計側で管理します。
