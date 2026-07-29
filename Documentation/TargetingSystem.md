# Targeting System

Targeting Systemは、ターゲット選択のGameplay状態をCamera、入力、UI、Skill、Networkから分離します。状態の正本は`TargetingController`だけが持ち、利用側は`TargetingStateChange`を購読します。

## パッケージ構成

| パッケージ | 責務 |
|---|---|
| `com.koiusa.targetingsystem` | Targeting状態、Command、候補収集、Filter、Scorer、任意のCinemachine表示 |
| `com.koiusa.steammultiruntime.targetingsystem` | 共有Input ActionsとLocal／Network所有権への接続 |

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

## Camera

汎用Sampleでは`TargetingCameraPresenter`を使用できます。本番SteamMultiRuntimeでは既存`CameraMixerWeightControllerBase`が同じ`CinemachineMixingCamera`配下の4台を一元管理します。

```text
Default Camera
Follow Camera
SingleTargetCamera
MultiTargetCamera -> CinemachineTargetGroup
```

Controllerの状態変更時だけLookAtとTargetGroupを更新し、Camera Weightの補間だけを`Update`で行います。最終Weight決定は既存Camera Controllerへ統合済みです。

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

MultiはKeyboard `3`、Gamepad R3です。明示Clear、Bulk Lock、Focusは設定Pathが空のため無効で、Single／Multiボタンの再入力で解除します。

## Local／Network所有権

`PlayerTargetingOwner`が同じPlayer上の`ILocalPlayerOwnershipNotifier`を解決し、Local OwnerだけでControllerと入力を有効化して`LocalTargetingControllerRegistry`へ登録します。Cameraは`CurrentChanged`を購読します。状態の読み取りだけを提供する`ILocalPlayerOwnership`と、Push通知を提供するNotifierを分離しています。所有権の確定・獲得・喪失・Network Despawnは`OwnershipChanged`で通知され、Frame Pollingは行いません。Remote PlayerとDedicated ServerではLocal Camera Targetingを動作させません。

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

## 移行対象

`SoloLockTargetBinder`、`LockOnTargetGroupBinder`、`TargetingCameraRig`は従来サンプル互換用です。新しい本番構成では使用せず、状態管理は`TargetingController`へ統一します。

検証用Sceneは次です。

```text
Assets/SteamMultiRuntime/Samples/Features/TargetingSystem/TargetingSystem_ProductionInput.unity
```
