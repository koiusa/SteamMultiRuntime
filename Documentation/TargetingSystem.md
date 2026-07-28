# TargetingSystem

TargetingSystemは、再利用可能なターゲティング実装とSteamMultiRuntimeの共有入力設定を接続するパッケージに分離しています。

## パッケージ構成

| パッケージ | 責務 |
|---|---|
| `com.koiusa.targetingsystem` | 検出、Solo／Multi Lock-On、ターゲット表示、Camera Rig |
| `com.koiusa.steammultiruntime.targetingsystem` | 共有`InputActionsConfig`をTargetingの入力契約へ接続 |

汎用コンポーネントは抽象`TargetingInputActions`を参照します。SteamMultiRuntimeでは`SteamMultiRuntimeTargetingInputActions`が共有入力設定からActionを解決します。

## 本番入力設定

本番の`InputActionAsset`は次の1ファイルだけです。

```text
Assets/SteamMultiRuntime/Runtime/Configs/Input/SteamMultiRuntime_InputActions.inputactions
```

`GameplayTargetingInputActions.asset`はInputActionAssetを複製せず、`GameplayInputActionsConfig.asset`を介して同じ本番アセットを参照します。

## テスト

`Assets/SteamMultiRuntime/Samples/Common/TargetingSystem_ProductionInput.unity`を開いてPlayします。

| 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|
| 移動 | WASD | 左スティック |
| 視点 | マウス移動 | 右スティック |
| Solo Lock-On開始 | 中クリック | LT / L2 |
| 前のターゲット | 1 | D-pad左 |
| 次のターゲット | 2 | D-pad右 |

現在の本番入力ではMulti Lock-On、Lock-On解除、Bulk Lock、FocusのAction Pathは空欄です。専用Actionを本番InputActionAssetへ追加するまで、これらの操作は安全に無効化されます。

## シーンへの配置

1. サンプルの`TargetingCameraRig.prefab`を構成例としてCamera Rigを配置します。
2. `TargetingCameraRigInput`、`SoloLockTargetInput`などの`Input Actions Config`へ`GameplayTargetingInputActions.asset`を割り当てます。
3. ターゲットへ`TargetMarker`を追加し、RootとAim Pointを設定します。
4. シーンに`TargetMarkerRegistry`と`ScreenTargetDetector`を用意します。
5. 必要に応じて`TargetIndicatorController`を設定します。

## 設定検証

Unityメニューから次を実行します。

```text
Koiusa > Steam Multi Runtime > Targeting > Validate Production Input
```

空欄ではないAction Pathが共有Input Actions設定から解決できるか検証し、成功時は設定アセットを選択します。

## 資産の配置

- 汎用パッケージのサンプル設定は`com.koiusa.targetingsystem/Samples/Resources`に置きます。
- SteamMultiRuntime本番設定は`Assets/SteamMultiRuntime/Runtime/Configs/Input`に置きます。
- 本番設定を使うテストSceneは`Assets/SteamMultiRuntime/Samples/Common`に置きます。
