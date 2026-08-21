# Editor Specification

この文書は、SteamMultiRuntime固有のUnity Editor拡張について、現在の主要な仕様を簡潔にまとめたものです。

## 基本方針

- Runtimeコンポーネントの責務と構成をInspector上で分かりやすく表示する
- 同一Player GameObject上のFeature／Actionを論理階層で表示する
- 不足コンポーネントは`Undo.AddComponent`を使って追加・修復する
- Editor操作後はObjectとSceneをDirtyにする
- 実行時状態は原則として読み取り専用で表示する
- メニューは操作種別ごとに`Configuration`、`Maintenance`、`Diagnostics`、`Validation`へ分類し、その下を機能別に分ける

## Player／Traversal Inspector

`Feature`で終わるTraversalコンポーネントには青い歯車アイコン、
`Action`で終わるコンポーネントには橙の稲妻アイコンを表示します。
同じGameObjectに多数のコンポーネントが並んでも、役割を素早く判別できます。

`ActorCharacterCoordinatorEditor`はMovement、Skill、Combatを論理階層で表示します。
Skill／Combat Featureは各行の`Add`から個別に装着でき、通常のComponentメニューから個別に削除できます。
Skill Featureには紫の専用アイコンを表示します。

`PlayerSkillInputController`と`NetworkPlayerSkillController`の設定補助は、固定Assetパスを使用せず、同じGameObject上のInput ControllerとSkill Featureから参照を取得します。Prefabの自動移行や一括再適用メニューは提供しません。

| 対象 | Editor | 主な仕様 |
|---|---|---|
| `LocalPlayerController` | `LocalPlayerControllerEditor` | 使用中の`ActorCompositeMotor`を表示する |
| `ServerDrivenActorController` | `ServerDrivenActorControllerEditor` | Composite MotorとControl Mode別の同期方針を表示する |
| `ActorCompositeMotor` | `ActorCompositeMotorEditor` | Motor、Coordinator、Traversal Feature／Actionの装着状況を階層表示し、不足Featureを追加できる |
| `ActorTraversalCoordinator` | `ActorTraversalCoordinatorEditor` | 管理対象Featureを表示し、Play中の状態、Wire接続、Blend値を読み取り専用表示する |
| Player Movement Debugger | `ActorMovementDebuggerWindow` | 選択したPlayerのComposite／Base Motor、Coordinator全体、Wall／Ladder／Wire Feature・Actionの実行状態を一覧監視する |
| Physics Contact Debugger | `PhysicsContactDebuggerWindow` | `GroundContactDebugDisplay`が収集した接触点、法線、Ground判定、Layer分類をPlay Mode中に一覧監視する |
| `WallTraversalFeature` | `WallTraversalFeatureEditor` | Wall ActionとResolverの不足を検出し、`Repair Wall Feature`で補完する |
| `LadderTraversalFeature` | `LadderTraversalFeatureEditor` | Climb／Detach Actionの不足を検出し、`Repair Ladder Feature`で補完する |
| `LadderVolume` | `LadderVolumeEditor` | シーン側Triggerとしての設定を表示する |
| `WireTraversalFeature` | `WireTraversalFeatureEditor` | ActionsとInternal Featuresの不足を検出し、まとめて補完する |

Wire構成の一括追加は次のメニューからも実行できます。

```text
Tools/SteamMultiRuntime/Configuration/Player/Setup Wire Actions On Selected Player
```

Wireの完全構成は次のとおりです。

```text
WireTraversalFeature
├─ WireAttachAction
├─ WireSwingAction
├─ WireReelAction
├─ WireGroundAction
├─ WireGrappleTargetingFeature
└─ WireLineVisualFeature
```

設定Structには専用Property Drawerがあります。

- `ActorMotorSettings`
- `WallRunTraversalSettings`
- `WallJumpTraversalSettings`
- `WallSlideTraversalSettings`
- `LadderTraversalSettings`

## NPC Inspector

`NpcNavMeshControllerEditor`は、同じGameObjectに装着されたNPC Moduleを一覧表示します。
Custom Editor上部には`NpcNavMeshController`の全SerializedFieldを標準描画し、今後設定が追加された場合も省略しません。`Movement Backend > Use Crowd Simulation`で起動時のCrowd／従来Motorを切り替え、`Enable Npc Rigidbody Collisions`でCrowd OFF時のNPC同士のPhysX衝突を選択し、`Crowd Contact`でCrowd有効時の接触設定を編集します。BackendとNPC衝突設定の変更は次回のPlay開始時に反映され、Play中の切り替えは対象外です。

`Tools > SteamMultiRuntime > Configuration > NPC > Crowd Simulation`は専用ウィンドウを開きます。AssetDatabase全体を検索し、`Assets`と`Packages`にある`NpcNavMeshController`を含むプレファブを一覧表示します。各行で`Use Crowd Simulation`と、Crowd OFF時だけ編集可能な`Enable Npc Rigidbody Collisions`を変更すると、そのプレファブ内の全`NpcNavMeshController`へ直ちに設定を保存します。読み取り専用Packageの行も表示しますが変更操作は無効です。Scene上のInstanceやEditor全体のPlayerPrefsは変更しません。

```text
Attached NPC Features
├─ Movement
├─ Speed
├─ Steering
├─ Avoidance
└─ Jump
```

- 未装着Moduleには`Add`を表示する
- 装着済みModuleには`Select`を表示する
- Moduleは任意構成とし、自動で全種類を追加しない
- 追加操作はUndoとScene Dirtyに対応する

## Input／KeyConfig

| 対象 | Editor機能 |
|---|---|
| `InputActionsConfig` | Input Action設定用Inspector |
| `TargetingInputActionsConfig` | Targeting用Action設定Inspector |
| `KeyConfigSettings` | KeyConfig用Action設定Inspector |
| `KeyConfigIconSet` | BindingとIconの対応設定Inspector |

KeyConfig関連メニュー:

```text
Tools/KeyConfig/Input Binding Icon Window
Tools/KeyConfig/Create Input Action Asset Resolver
Tools/KeyConfig/Create Input Binding Icon Resolver
```

## Asset／Scene設定

以下は専用Inspectorまたは生成メニューを持ちます。

- `CharacterModelIdList`
- `StageSceneList`
- `RuntimeUserProfile`
- `LocalRuntimeUserProfile`
- `SteamLobbySceneLoader`
- `LoadingSplashSettings`

主要な生成メニュー:

```text
Assets/Create/SteamMultiRuntime/Character Model Id List
Assets/Create/SteamMultiRuntime/Stage Scene List
Assets/Create/SteamMultiRuntime/Loading Splash Settings
Assets/Create/SteamMultiRuntime/Steam Lobby UI Assets
Assets/Create/SteamMultiRuntime/Stage Select UI Assets
Tools/SteamMultiRuntime/Maintenance/Assets/Create Loading Splash Settings
```

## UI Sorting Order検証

Screen Space `UIDocument`は次の予約帯を使用します。

| Sorting Order | 用途 |
|---:|---|
| 0–49 | HUD／Overlay |
| 50–79 | Debug UI |
| 80–89 | Stage Select／Steam LobbyなどSession Menu |
| 90–99 | PauseなどRoot Menu |
| 100–109 | Character Select／Key ConfigなどChild／Modal Menu |
| 110–119 | Loading／Blocking Dialog |

`Tools/SteamMultiRuntime/Validation/UI/Validate UIDocument Sorting Orders`は、Production Prefabを変更せずに読み込み、GameObject名から判定できるUIが予約帯を外れていないか検査します。違反時はAsset Path、Hierarchy Path、現在値、期待範囲をConsoleへ出力します。自動修正は行いません。

## 補助Window／ツール

| メニュー | 用途 | 変更 |
|---|---|---|
| `Diagnostics/Animation Events/Event Finder` | Animation Eventの検索 | なし |
| `Diagnostics/Animation Events/Receiver Visualizer` | Event受信関係の可視化 | なし |
| `Diagnostics/Resources/Model ID List Path Viewer` | Model IDと参照先Pathの確認 | なし |
| `Diagnostics/Scenes/Scene List Viewer` | Scene Listの内容確認 | なし |
| `Validation/UI/Validate UIDocument Sorting Orders` | Prefab内UIDocumentの予約帯検査 | なし |
| `Diagnostics/Physics/Contact Debugger` | Moverの物理接触とGround判定の監視 | なし |
| `Configuration/Steam/Facepunch App ID` | Steam App ID関連ファイルの設定 | あり |

上表のメニューは、特記がない限り`Tools/SteamMultiRuntime/`以下です。

`GroundContactDebugDisplay`はRuntimeで接触情報の収集とScene Gizmo描画だけを担当します。Game View上のIMGUI表示は持たず、詳細表示は`Tools/SteamMultiRuntime/Diagnostics/Physics/Contact Debugger`から開くEditor専用Windowが内部Snapshotを読み取ります。Prefab上のコンポーネントが無効な場合は接触を収集しないため、調査対象のインスタンスで有効化して使用します。

`NetworkAnimatorParameterSynchronizer`はAnimator ControllerとNetworkAnimatorの同期対象パラメータを比較・更新します。変更前に確認Windowを表示する設計です。

## Repair操作の契約

Repair／Add操作は次の条件を守ります。

1. 既存コンポーネントを重複追加しない
2. 同じPlayer GameObject上へ追加する
3. `Undo.AddComponent`を使用する
4. Scene Object変更時はSceneをDirtyにする
5. 設定済みSerialized Fieldを不要に上書きしない
6. Play Mode中の一時状態とPrefab／Scene編集を混同しない

Prefab構成を変更した場合は、Local Player、Network Player、Local NPC、Network NPCの各主要Prefabで不足と重複がないことを確認します。NetworkBehaviourを追加・並べ替えた場合は、Host／Client間でPrefab互換性も確認します。
