# Keyconfig

Keyconfigは、再利用可能な汎用パッケージとSteamMultiRuntime固有のLocalization接続に分離しています。

## パッケージ構成

| パッケージ | 責務 |
|---|---|
| `com.koiusa.keyconfig` | Input Systemの一覧表示、入力監視、リバインド、保存、アイコン解決 |
| `com.koiusa.steammultiruntime.keyconfig` | Keyconfigを`GameLocalization`へ接続 |

汎用KeyconfigはSteamMultiRuntimeを参照しません。SteamMultiRuntime側が`IKeyConfigLocalizer`を実装して接続します。

## 本番入力設定

本番の`InputActionAsset`は次の1ファイルだけです。

```text
Assets/SteamMultiRuntime/Runtime/Configs/Input/SteamMultiRuntime_InputActions.inputactions
```

Keyconfig用の`GameplayKeyConfigInputActionsConfig.asset`はInputActionAssetを複製せず、同じ本番アセットを参照します。

## テスト

`Assets/SteamMultiRuntime/Samples/Features/Keyconfig/Keyconfig_ProductionInput.unity`を開いてPlayします。

- 入力すると該当行が点灯します。
- `Change`で新しいキー／ボタンを入力します。Escapeでキャンセルします。
- `Reset`で行単位、`Reset All`で全体を初期化します。
- `Save`／`Load`でユーザー設定を保存／復元します。
- Player、System、UIタブでAction Mapを切り替えます。
- キーボードまたはゲームパッドのUI Navigateでフォーカスを移動し、Submitで操作します。Cancelは通常時に画面を閉じます。リバインド中はEscapeでキー変更をキャンセルします。
- UI Action Mapは入力状態を確認できるよう一覧へ表示しますが、Keyconfig自身の操作を失わないようChange／Resetの対象外です。保存データに古いUI Overrideが含まれていてもLoad時に除去します。
- LB／RB（`UI/PreviousSection`／`UI/NextSection`）でAction Mapタブを循環切り替えします。左右はBinding Group／Load／Save／Reset All／Closeのフォーカスを循環します。通常のD-padまたは左スティック上下はフォーカスを移動せずリストをスクロールし、Binding Groupにフォーカス中だけDropdownの選択操作へ渡します。
- Action MapタブでSubmitすると、そのMap内の最初に変更可能な行へ入ります。リスト内では上下で行、左右でChange／Resetを選び、CancelでAction Mapタブへ戻ります。
- Changeの完了／キャンセル／失敗、および行単位Resetの後も対象行へフォーカスを戻し、リスト内の操作を継続します。
- Keyconfig表示中はUI Map以外で有効だったActionを一時停止し、画面を閉じると元の有効状態へ戻します。変更待機中はタブ、ファンクション、スクロールを含むKeyconfig内の移動もロックします。

保存先は`Application.persistentDataPath/InputBindings`です。

## シーンへの配置

1. `UIDocument`を持つGameObjectへ`KeyConfigUiDocument`を追加します。
2. `Input Actions Config`へ`GameplayKeyConfigInputActionsConfig.asset`を割り当てます。
3. UI操作など固定するAction Mapを`Non Rebindable Action Maps`へ設定します。
4. KeyconfigのUXML、USS、必要に応じて`InputBindingIconResolver`を設定します。

操作ガイドだけを表示する場合は`Runtime/Resources/System/InputGuideOverlay.prefab`を使用できます。

## 入力アイコン

Keyboard、Mouse／Pointer、Gamepad、DualShock、基本的なJoystick入力は内蔵アイコンへ解決されます。Touchscreen、Pen、XR Controller、および`*/{Submit}`のようなデバイス共通Bindingは、専用画像または実デバイスが特定できないため文字列表示になる場合があります。

## 共通Scrollbar

Scrollbarは`com.koiusa.ui.common`の`SteamMultiRuntimeScrollView.uss`を共有します。個別画面で太さを上書きせず、共通USSを修正します。

## 資産の配置

- 汎用パッケージの基本サンプルは`com.koiusa.keyconfig/Samples/Basic`に置きます。
- SteamMultiRuntime本番設定は`Assets/SteamMultiRuntime/Runtime/Configs/Input`に置きます。
- 本番設定を使う機能サンプルSceneは`Assets/SteamMultiRuntime/Samples/Features/Keyconfig`に置きます。
