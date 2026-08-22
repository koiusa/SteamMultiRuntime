# Current Class Structure

PlayerのSkill／Combatを含む論理階層と依存規則は[PlayerGameplayArchitecture.md](PlayerGameplayArchitecture.md)を参照してください。パッケージ境界、ドメイン間の型付き接続、リフレクション方針は[PackageArchitecture.md](PackageArchitecture.md)を正本とします。

この文書は、現在のSteamMultiRuntime Runtime実装と、それを支える汎用パッケージ境界について、クラスの配置、責務、所有関係、処理の流れをまとめたものです。

詳細仕様は領域別の文書へ分離しています。

- Traversalの責務、状態、設定、依存規則: [TraversalArchitecture.md](TraversalArchitecture.md)
- Player Skill／Combatの現在構成と予定クラス: [PlayerGameplayArchitecture.md](PlayerGameplayArchitecture.md)
- NPCのModule構成、Local／Network駆動: [NpcArchitecture.md](NpcArchitecture.md)
- Character Model、Profile、選択UI: [CharacterArchitecture.md](CharacterArchitecture.md)
- Steam Lobby、Stage選択、Scene遷移: [SessionArchitecture.md](SessionArchitecture.md)
- Camera切替、入力、障害物回避: [CameraArchitecture.md](CameraArchitecture.md)
- Editor拡張: [EditorSpecification.md](EditorSpecification.md)

Unity、Netcode for GameObjects、Input Systemなどの外部実装、Editor専用クラス、Sample、Thirdpartyの詳細は対象外です。

## ドメインと所有パッケージ

| ドメイン | 非Network | Network／Backend | UI／表示 |
|---|---|---|---|
| Character | `character`, `resourceloader` | `player.netcode`のModel Sync | `character.ui` |
| Player | `player` | `player.netcode` | `player.ui` |
| Locomotion／Traversal | `locomoter` | `locomoter.netcode` | `animationdriver` |
| Lobby／Scene Flow | `lobby` | `lobby.netcode`, `lobby.steam` | 各Lobbyパッケージ内の対応UI |
| NPC | `prototype` | `prototype` | Debug表示も`prototype` |
| Localization | `localization` | なし | 各UIから共通APIを利用 |
| Audio | `audio` | Network状態を所有しない | `IFootstepReceiver`で接続 |
| Application | `com.koiusa.application` | なし | `input.core`の汎用Triggerから任意接続 |

複数ドメインを組み立てる`LocalManager`、`LocalRuntimeUserProfile`、Spawn Coordinatorなどは`integration`が所有します。共通契約の配置規則は[PackageArchitecture.md](PackageArchitecture.md)を参照してください。

## ドメイン間ブリッジ

```text
LocalManager : ILocalPlayerProvider
  → LocalPlayerProviderRegistry（core）
  → LocalLoadingSplash（lobby）
  → LoadingSplashPresenter（resourceloader）

FootstepCollider
  → IFootstepReceiver
  → FootstepColliderSpawner

TargetingCommandInput
  → TargetingController
  → TargetingStateChange
      ├─ TargetingCameraPresenter
      └─ TargetIndicatorController

InputActionPerformedTrigger（input.core）
  → serialized UnityEvent（System.prefab）
  → GameQuitter.RequestQuit（application）

InputGuideSelectionController
  → InputGuideOverlay（Map／Binding Group）

InputGuideNavigationController
  → InputGuideOverlay（Map tabs／scroll）
```

ドメイン間の接続に型名文字列、Reflection、`SendMessage`を使いません。インターフェースの所有先は、その契約を定義するドメインまたはSteam Multi Runtime共通の`core`です。例外条件とレビュー方法は[Package Architectureのリフレクション方針](PackageArchitecture.md#ドメイン間の接続方法とリフレクション方針)に従います。

Local専用UIは`ILocalPlayerOwnership`を通じて所有状態を参照します。Local実装は常に解決済みOwnerを返し、Network実装は`NetworkObject`のSpawn／Owner状態を型付き契約へ変換します。UI側はNetcode型やReflectionへ依存しません。

## 全体構成

```text
SteamMultiRuntime
├─ Application
│  ├─ GameQuitter
│  └─ ApplicationLifecycle
│
├─ Input
│  ├─ InputActionsConfig
│  ├─ InputActionLease
│  ├─ InputActionPerformedTrigger
│  ├─ PlayerGameplayInputReader
│  ├─ ActorInputState
│  └─ IActorInputSource
│
├─ Input Guide
│  ├─ InputGuideOverlay
│  ├─ InputGuideSelectionController
│  ├─ InputGuideNavigationController
│  └─ InputGuideOperationPanel
│
├─ Player Locomotion
│  ├─ Controller
│  │  ├─ LocalPlayerController
│  │  └─ ServerDrivenActorController
│  ├─ ActorCompositeMotor
│  ├─ ActorMotor
│  └─ ActorTraversalCoordinator
│
├─ Traversal
│  ├─ Wall
│  ├─ Ladder
│  └─ Wire
│
├─ NPC
│  ├─ NpcNavMeshController
│  ├─ NavMesh機能モジュール
│  ├─ AiActorInputSource
│  ├─ NpcCrowdAgent
│  ├─ NpcCrowdMotor
│  └─ NpcCrowdSimulation
│
├─ Character
│  ├─ Runtime User Profile
│  ├─ Model Sync
│  └─ Character Prefab Loader
│
├─ Presentation
│  ├─ GroundMotionPresentationScheduler
│  ├─ ActorAnimatorStateDriver
│  ├─ PhysicsPresentationSmoother
│  ├─ GuardShieldVisual
│  ├─ Camera Mixer / Focus Marker
│  ├─ PlayerCompassHud
│  └─ Player Name / Loading UI
│
└─ Session
   ├─ Steam Lobby
   └─ Scene Flow
```

## Player Locomotion

### クラス構成

```text
IActorController
├─ LocalPlayerController（Local Player）
│  ├─ PlayerGameplayInputReader : IActorInputSource
│  └─ ActorCompositeMotor
│
└─ ActorControllerAdapter（Network Player / 全NPC）
   └─ IActorLocomotionState
      ├─ ServerDrivenActorController : NetworkBehaviour
      └─ NpcNavMeshController : INpcLocomotionState

ServerDrivenActorController : NetworkBehaviour, IActorLocomotionState
│  ├─ IActorInputSource
│  │  ├─ PlayerGameplayInputReader（Network Player）
│  │  └─ AiActorInputSource（Network NPC）
│  ├─ ActorCompositeMotor（Network Player）
│  ├─ NpcCrowdAgent / NpcCrowdMotor（Network Server NPC）
│  └─ Network同期状態
│     ├─ ActorInputSyncState
│     ├─ ActorKinematicState
│     ├─ ActorMovementFlagsState
│     └─ WireSwingNetworkState

Local／Network NPCはいずれもActorControllerAdapterだけがIActorControllerを実装する。
Adapterの状態SourceはLocalではNpcNavMeshController、NetworkではServerDrivenActorControllerになる。

ActorCompositeMotor : IActorMoveInputReceiver
├─ ActorMotor : IActorMotor
└─ ActorTraversalCoordinator
   ├─ IActorTraversalCoordinator
   └─ ITraversalIntentContext
```

ControllerからTraversalまでの入力経路と各クラスの詳細責務は、[TraversalArchitecture.md](TraversalArchitecture.md)に集約しています。

## Traversal

Traversalは`Wall`、`Ladder`、`Wire`の3領域で構成され、共有状態を持つ`Feature`と具体動作を行う`Action`に分かれます。クラス階層、各Actionの責務、状態、設定値、Netcode、Prefab規則は[TraversalArchitecture.md](TraversalArchitecture.md)を正本とします。

## NPC

NPCはAI判断、共通疑似入力、移動Backendを分離します。`NpcNavMeshController`はNavMesh Module、FutureAction、テストドライバの指示を`NpcControllerInputCommand`へ一度だけ集約します。起動時に選択したBackend AdapterがこれをCrowd CommandまたはPlayer共通Actor入力へ変換します。`NpcCrowdAgent`がCrowd側のCommand、Probe、Snapshot、結果適用の境界となり、`NpcCrowdSimulation`と`NpcCrowdMotor`はAI判断を参照しません。

```text
NpcNavMeshController : INpcLocomotionState
├─ NpcNavMeshMovementModule
├─ NpcNavMeshSpeedModule
├─ NpcNavMeshSteeringModule
├─ NpcNavMeshAvoidanceModule
├─ NpcNavMeshJumpModule
├─ AiActorInputSource
├─ NpcCrowdTraversalInput
│  └─ FutureAction／テストドライバ疑似入力
└─ BuildNpcInputCommand
   └─ NpcControllerInputCommand
      ├─ Move／Jump
      └─ Wire Aim／Fire／Hold／Reel
             │
             ├─ Use Crowd Simulation = ON
             │  └─ NpcCrowdCommand
             │     └─ NpcCrowdAgent
             │        ├─ NpcCrowdSimulationへの登録／解除
             │        ├─ Capsulecast／Overlap Queryの受け渡し
             │        ├─ Agent／Movement Snapshot
             │        └─ NpcCrowdMotor
             │           ├─ Kinematic移動、接地、壁、移動床
             │           └─ Player／Physics Objectとの接触
             │
             └─ Use Crowd Simulation = OFF
                ├─ Local: ActorCompositeMotor
                └─ Network Server: ServerDrivenActorController
                   └─ ActorCompositeMotor
                      └─ Dynamic Rigidbody／ActorTraversalCoordinator

NpcCrowdSimulation
├─ NpcCrowdAgentだけを登録
├─ 30Hzの共有Player Loop
├─ Burst Spatial Grid／Boid／RVO／Movement Job
└─ 一括Capsulecast／Overlap Query
```

Wall接触は`NpcCrowdTraversalInput`から`SlopeContactResolver`へ、LadderのEnter／Exit／Detachは`ILadderTraversalFeature`へ通知します。これらもBackend分岐より前に処理されるため、Crowd／従来方式で同じCoordinator状態を使用します。

Crowd有効時の通常移動はPlayerのDynamic Rigidbody Motorを直接Tickせず、NPC専用のKinematic Crowd Motorで処理します。Crowd無効時は比較用に`ActorCompositeMotor`を使用します。`IActorMotor`設定、`IActorTraversalCoordinator`、Actor状態契約、Animator Driver、表示補間はPlayerと共通化を維持します。Moduleの責務、Local／Network経路、Server所有契約は[NpcArchitecture.md](NpcArchitecture.md)を正本とします。

`NpcNavMeshController.Use Crowd Simulation`を無効にすると、同じAI／FutureAction疑似入力を`ActorCompositeMotor`へ渡す従来のDynamic Rigidbody経路へ起動時に切り替わります。通常移動、ジャンプ、WallRun、Ladder、Wire入力の生成はBackend選択前に一度だけ行うため、共通化による二重計算は発生しません。これによりPrefab／SceneのInspectorチェック一つでCrowd有無の性能・挙動を比較できます。

## Character ModelとProfile

Profile、Model Sync、Prefab Loader、Character選択UIの詳細は[CharacterArchitecture.md](CharacterArchitecture.md)を正本とします。

## LobbyとScene Flow

Steam Lobby、Local Stage選択、Dedicated Server、Scene Loader、Loading Splashの詳細は[SessionArchitecture.md](SessionArchitecture.md)を正本とします。

## Animatorと表示

```text
ActorAnimatorStateDriver : IActorAnimatorStateDriver
├─ IActorController
├─ IActorTraversalCoordinator
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

`ActorAnimatorStateDriver`は入力を再判定せず、ControllerとCoordinatorが公開する確定済み状態をAnimatorパラメータへ変換します。

`GroundMotionPresentationScheduler`は移動床の表示姿勢を先に、`PhysicsPresentationSmoother`を使用するActorの表示姿勢を後に適用します。床やActorの個別`Update`、Crowd Backendからの手動呼び出しと重複させません。`ActorAnimatorUpdateScheduler`はAnimator状態更新、`NpcCrowdSpringSimulation`はAnimator後のSpring姿勢計算を所有するため、これらはPresentation姿勢適用とは別責務です。

Camera Mixer、Focus Marker、入力割り当て、障害物回避の詳細は[CameraArchitecture.md](CameraArchitecture.md)を正本とします。

## Runtime全体の境界

現在の構成では、以下を境界として扱います。

1. Input Systemを扱うのは入力Readerまで
2. NPCはPlayer用入力・Motor・Animator経路を再利用する
3. Network物理はServer Authorityとする
4. AnimatorとUIは確定済み状態を表示し、ゲーム状態を決定しない

Controller、Coordinator、Feature、Action間の詳細な依存規則はTraversal設計側で管理します。

## Applicationと汎用入力Trigger

```text
com.koiusa.input.core 0.2.0
└─ InputActionPerformedTrigger
   ├─ InputActionsConfig
   ├─ InputActionLease
   └─ UnityEvent Performed
      └─ System.prefabでGameQuitter.RequestQuitへ接続

com.koiusa.application 0.2.0
├─ GameQuitter
│  ├─ RequestQuit()
│  ├─ IsQuitRequested
│  └─ QuitRequested
└─ ApplicationLifecycle
   ├─ FocusChanged / IsFocused
   ├─ PauseChanged / IsPaused
   └─ Quitting / IsQuitting

com.koiusa.inputguide 0.2.0
├─ InputGuideOverlay
├─ InputGuideSelectionController
├─ InputGuideNavigationController
└─ InputGuideOperationPanel
```

両パッケージは相互参照しません。`input.core`は入力発生をシリアライズ済み`UnityEvent`として公開し、`System.prefab`が`application`の終了要求へ接続します。これにより`application`はInput Systemなしでも単独利用できます。
