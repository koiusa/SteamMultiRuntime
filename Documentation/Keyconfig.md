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

`Assets/SteamMultiRuntime/Samples/Common/Keyconfig_ProductionInput.unity`を開いてPlayします。

- 入力すると該当行が点灯します。
- `Change`で新しいキー／ボタンを入力します。Escapeでキャンセルします。
- `Reset`で行単位、`Reset All`で全体を初期化します。
- `Save`／`Load`でユーザー設定を保存／復元します。
- Player、System、UIタブでAction Mapを切り替えます。

保存先は`Application.persistentDataPath/InputBindings`です。

## シーンへの配置

1. `UIDocument`を持つGameObjectへ`KeyConfigUiDocument`を追加します。
2. `Input Actions Config`へ`GameplayKeyConfigInputActionsConfig.asset`を割り当てます。
3. KeyconfigのUXML、USS、必要に応じて`InputBindingIconResolver`を設定します。

操作ガイドだけを表示する場合は`Runtime/Resources/System/InputGuideOverlay.prefab`を使用できます。

## 入力アイコン

Keyboard、Mouse／Pointer、Gamepad、DualShock、基本的なJoystick入力は内蔵アイコンへ解決されます。Touchscreen、Pen、XR Controller、および`*/{Submit}`のようなデバイス共通Bindingは、専用画像または実デバイスが特定できないため文字列表示になる場合があります。

## 共通Scrollbar

Scrollbarは`com.koiusa.ui.common`の`SteamMultiRuntimeScrollView.uss`を共有します。個別画面で太さを上書きせず、共通USSを修正します。

## 資産の配置

- 汎用パッケージのサンプル設定は`com.koiusa.keyconfig/Samples/Resources`に置きます。
- SteamMultiRuntime本番設定は`Assets/SteamMultiRuntime/Runtime/Configs/Input`に置きます。
- 本番設定を使うテストSceneは`Assets/SteamMultiRuntime/Samples/Common`に置きます。
