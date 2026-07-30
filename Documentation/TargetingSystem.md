# Targeting System

Targeting Systemは、ターゲット選択のGameplay状態をCamera、入力、UI、Skill、Networkから分離します。状態の正本は`TargetingController`だけが持ち、利用側は`TargetingStateChange`を購読します。

## パッケージ構成

| パッケージ | 責務 |
|---|---|
| `com.koiusa.targetingsystem` | Targeting状態、Command、候補収集、Filter、Scorer、UI Toolkit Indicator、Sample Player移動、Random Spawner／Moverなどの汎用部品 |
| `com.koiusa.steammultiruntime.targetingsystem` | 共有Input ActionsとLocal／Network所有権への接続、Cinemachine込みの完成Showcase |

Targetingの状態管理はCinemachine、Netcode、Skillの具象実装を参照しません。`TargetingCameraPresenter`は任意の表示アダプターであり、Targeting状態の所有者ではありません。

## Runtime構成

```text
TargetingCommandInput
  -> TargetingController
       ├─ ITargetingContextSource
       ├─ ITargetCandidateSource
       ├─ ITargetFilter[]
       └─ ITargetScorer[]
            |
            └─ TargetingStateChange
                 ├─ TargetingCameraPresenter
                 ├─ Target Indicator
                 └─ Skill Aim Adapter
```

`TargetingState`は次を公開します。

- `Mode`: `None`、`Single`、`Multi`
- `PrimaryTarget`: SkillとCameraが主に注視する対象
- `SelectedTargets`: Singleでは1件、Multiでは選択済みの全対象
- `Revision`: 状態スナップショットの世代

SingleとMultiで別々の状態を所有しません。Multi中も`PrimaryTarget`を1体持ち、Previous／NextはPrimaryだけを切り替えます。

## 標準Policy

- `RegistryTargetCandidateSource`は`TargetMarkerRegistry`から候補を収集します。
- `ViewportTargetPolicy`は取得距離と画面範囲をFilterし、画面中央距離、World距離、Target Priorityを合成してScoreを返します。
- Scoreは小さい候補ほど優先されます。
- Multi Lockの既定上限は8体で、上限到達後の追加は拒否します。
- `TargetMarker`が無効化またはTarget不可になるとイベントで即時に選択から除外します。

候補探索はCommand実行時だけ行います。Camera補間やWorld座標追従以外の理由で全候補を毎Frame走査しません。
Multi開始時に、その時点の距離・Viewport条件を通過した候補集合を固定します。長押し追加中にGroup Framingで画角が広がっても候補集合を再収集しないため、Cameraの引きに連鎖して遠方Targetが追加されることはありません。取得距離はCamera位置ではなくPlayer Owner位置を基準にします。

## Camera

統合パッケージのShowcaseでは`TargetingCameraPresenter`を使用します。本番SteamMultiRuntimeでは既存`CameraMixerWeightControllerBase`が`Camera Mixer`配下の4台を一元管理します。共通`Targeting Camera System.prefab`をLocal／Network Camera PrefabへNested配置し、Outer Prefab OverrideからMixer Controllerと対象Cameraをシリアライズ参照します。

```text
Camera System
├─ Camera Mixer
│  ├─ DefaultCamera
│  ├─ FollowCamera
│  ├─ SingleTargetCamera
│  └─ MultiTargetCamera
├─ CameraAnchors
└─ Targeting Camera System (Nested Prefab)
   ├─ TargetingTargetGroup
   └─ PrimaryCenteredTargetGroup
```

Controllerの状態変更時だけLookAtとTargetGroupを更新し、Camera Weightの補間だけを`Update`で行います。最終Weight決定は既存Camera Controllerへ統合済みです。
None／Single／Multiの切替時は、遷移元Cameraの最終位置と向きを遷移先Cameraへ渡してからWeightをブレンドします。ロック解除時もFollow Cameraへ現在姿勢を引き継ぐため、Follow Cameraに残っていた古い軌道角へ急に戻りません。
実ゲームのSingle／Multi CameraはPlayerと選択Targetを同じ`CinemachineTargetGroup`へ登録し、`CinemachineGroupFraming`で両方を画角へ収めます。Single CameraがTargetだけをLookAtしてPlayerを画角外へ出す構成にはしません。
Target Groupの再構築、Single LookAt、Group Framingの保証は汎用`TargetingCameraGroupPresenter`が所有します。実ゲームの`CameraMixerWeightControllerBase`とSampleの`TargetingCameraPresenter`は同Componentへ状態を渡し、Camera Weight制御だけを各自で担当します。
Camera Controllerの`Targeting Framing Mode`では、Primaryを画面中心に置く`Primary Centered`、Playerと選択対象全体の中心を使う`Group Centered`、任意の`ITargetingCameraFramingGroup`を指定する`Custom`を選択できます。
標準Prefabは必要なフレーミングComponentを事前配置し、欠落時はRuntime生成せず警告を出します。Player生成後に必要になるCamera Follow Targetだけは、Camera Rigへ事前配置した`TargetingCameraRuntimeObjectFactory`が生成と破棄を一元管理します。
共通Prefabの正本は`Assets/SteamMultiRuntime/Runtime/Prefabs/Camera/Targeting Camera System.prefab`です。Local／Network側には共通構成を複製せず、Camera参照だけをNested Prefab Overrideとして保持します。
`PrimaryCenteredCinemachineTargetGroup`と標準`CinemachineTargetGroup`は別GameObjectへ配置します。同じLookAt Transformに複数の`ICinemachineTargetGroup`を置くとCinemachineのGroup解決が曖昧になるため、併置しません。
Free／Single Target CameraはPlayerをTracking Target、Player配下の高さ付き`Camera Aim`をLookAtとして分離します。Single未選択時も`Camera Aim`をfallback LookAtとして保持し、無効WeightのRotation Composerから警告が出ないようにします。
ShowcaseのOrbital Follow、Rotation Composer、Input Axis Controllerは実ゲームの`Local Mixing Camera.prefab`を正本としてコピーし、独自の視点感度処理を持ちません。

Wire照準はTargeting状態とは別のCamera入力制約です。Wire照準中もSingle／Multi状態を保持し、Camera Directorが一時的にWire側の要求を優先します。

## Input

本番Input Action Assetの正本は次です。

```text
Assets/SteamMultiRuntime/Runtime/Configs/Input/SteamMultiRuntime_InputActions.inputactions
```

`TargetingCommandInput`は入力をCommandへ変換します。Single／Multi入力は同じモードで再入力するとClearへ切り替わります。

現在解決されるActionは次です。

| 操作 | Action |
|---|---|
| Single開始／解除 | `Player/LockOn` |
| Multi開始／解除 | `Player/MultiLock` |
| 前の対象 | `Player/Previous` |
| 次の対象 | `Player/Next` |

MultiはKeyboard `3`、Gamepad R3です。入力時にMultiへ遷移して画面内候補を上限8体まで一括選択し、同じボタンの再入力で解除します。明示Clear、独立Bulk Lock、Focusの設定Pathは空のままです。
Gamepad L2の`Player/Strafe`はホールド式です。押している間だけStrafeになり、離すと通常移動へ戻ります。Single Lock中に押した場合はStrafe開始と同時に現在対象を保持したままMultiへ昇格し、画面内候補を追加選択します。L2を離すと追加対象を解除し、Primary Targetを維持したSingleへ戻ります。`3`／R3で開始したMultiはL2解放の影響を受けません。

## Local／Network所有権

`PlayerTargetingOwner`が同じPlayer上の`ILocalPlayerOwnershipNotifier`を解決し、Local OwnerだけでControllerと入力を有効化して`LocalTargetingControllerRegistry`へ登録します。Cameraは`CurrentChanged`を購読します。状態の読み取りだけを提供する`ILocalPlayerOwnership`と、Push通知を提供するNotifierを分離しています。所有権の確定・獲得・喪失・Network Despawnは`OwnershipChanged`で通知され、Frame Pollingは行いません。Remote PlayerとDedicated ServerではLocal Camera Targetingを動作させません。

`LocalTargetingIndicatorPresenter`も同じRegistryを購読し、Local Controllerが存在する間だけScreen SpaceのUI Toolkit Indicatorを有効化します。選択集合とPrimary表示は`TargetingStateChange`で更新し、移動対象の画面位置だけを1つの`TargetIndicatorController`がまとめて追従します。Remote PlayerごとのUIDocumentは生成しません。

Network Skillへ対象を渡す場合、Clientの選択結果は入力意図としてだけ扱います。Serverは対象の存在、敵味方、距離、角度、Skill固有条件、Multi対象数を検証し、HitとDamageを確定します。

## 本番Prefab設定

Player側に次を配置します。

1. `TargetingContextProvider`
2. `RegistryTargetCandidateSource`
3. `ViewportTargetPolicy`
4. `TargetingController`
5. `TargetingCommandInput`
6. SteamMultiRuntime Playerでは`PlayerTargetingOwner`

上記Componentは標準Local／Network Player Prefabへ適用済みです。全標準Player／NPC Proxyは`TargetMarker`を持ち、`System.prefab`の`TargetMarkerRegistry`へ有効化イベントで登録します。Local／Network Mixing Camera PrefabにはSingle／Multi Camera、Target Group、`LocalTargetingCameraConnector`を適用済みです。

同じ構成を再生成するEditor操作は`Tools/SteamMultiRuntime/Targeting/Install Production Setup`です。処理はUnityのPrefab／Asset公開APIを使用し、Reflectionは使用しません。

検証用Sceneは次です。

```text
Assets/Samples/Steam Multi Runtime/<version>/Targeting System/TargetingSystem.unity
```

旧Binder、旧Input、旧Camera Rig、Material差し替え式デバッグ表示は削除済みです。汎用パッケージのBasicは`TargetingSamplePlayerMover`、`TargetMarkerRandomSpawner`、`TargetMarkerRandomMover`を含む最小構成です。SteamMultiRuntime統合側のShowcaseは構築済みCinemachine、Production Input、UI Toolkit Indicatorとこれらの汎用Sample部品を組み合わせます。
