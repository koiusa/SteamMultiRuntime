# Keyconfig

Keyconfigは、再利用可能な汎用パッケージとSteamMultiRuntime固有のLocalization接続に分離しています。

## パッケージ構成

| パッケージ | 責務 |
|---|---|
| `com.koiusa.keyconfig` | Input Systemの一覧表示、入力監視、リバインド、保存、アイコン解決 |
| `com.koiusa.steammultiruntime.keyconfig` | Keyconfigを`GameLocalization`へ接続 |

汎用KeyconfigはSteamMultiRuntimeを参照しません。SteamMultiRuntime側が`IKeyConfigLocalizer`を実装して接続します。

## レイアウト

Keyconfig Panelは画面内の利用可能な高さを満たします。Binding Listは項目数に関係なく、Table Headerと下部Button Rowの間にある残り領域を埋めます。
Action Mapタブは固定高の横ScrollViewへ表示します。Map数が増えてもPanelや下部Function Button Rowを押し出しません。

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
- `Save`／`Load`でユーザー設定を保存／復元します。Change／Reset／Reset Allは画面内の編集中状態へ即時反映されますが、`Save`せずに閉じた場合は画面を開いた時点の設定へ戻ります。
- 疑似デバイスUIとOperationパネルはInput SystemのBinding変更通知を購読し、Change／Reset／Loadで現在のOverrideへ自動更新します。
- Player、System、UIタブでAction Mapを切り替えます。
- Action Mapを切り替えたときは一覧を先頭へ戻し、Binding Groupとデバイス名の見出しを表示します。
- 最初のフォーカス行より前に編集不可行や見出しがある場合も、上端への移動ではScrollViewを完全に先頭へ戻します。
- キーボードまたはゲームパッドのUI Navigateでフォーカスを移動し、Submitで操作します。Cancelは通常時に画面を閉じます。リバインド中はEscapeでキー変更をキャンセルします。
- UI Action Mapは入力状態を確認できるようタブと一覧へ表示し、Submitで行の中へ入って上下移動できます。Keyconfig自身の操作を失わないようChange／Resetは無効のままです。保存データに古いUI Overrideが含まれていてもLoad時に除去します。
- LB／RB（`UI/PreviousSection`／`UI/NextSection`）でBinding GroupとAction Mapタブを循環切り替えします。左右はLoad／Save／Reset All／Closeだけを循環し、Binding Groupへは移動しません。通常のD-padまたは左スティック上下はフォーカスを移動せずリストをスクロールし、Binding Groupにフォーカス中だけDropdownの選択操作へ渡します。
- Action MapタブでSubmitすると、そのMap内の最初に変更可能な行へ入ります。リスト内では上下で行、左右でChange／Resetを選び、CancelでAction Mapタブへ戻ります。
- UI Navigateの方向判定とリピートは`input.core`の`UiNavigationInputSession`を使います。単発入力は1行ずつ移動し、長押しは0.4秒後から0.1秒間隔で連続移動します。
- Changeの完了／キャンセル／失敗、および行単位Resetの後も対象行へフォーカスを戻し、リスト内の操作を継続します。
- Keyconfig表示中はUI Map以外で有効だったActionを一時停止し、画面を閉じると元の有効状態へ戻します。変更待機中はタブ、ファンクション、スクロールを含むKeyconfig内の移動もロックします。
- 未接続デバイスのBindingは表示と入力診断だけを行い、Changeを無効化します。変更待機は5秒でタイムアウトして元の行へ戻るため、入力できないデバイスを選んでも操作不能になりません。
- 同じキーを異なるActionやAction Mapで共有する構成を許可します。重複として拒否するのは、同一Action内の別Bindingへ同じ入力を割り当てた場合だけです。
- 異なる変更可能Actionとの競合時は確認パネルを表示し、「既存を解除」「両方に設定」「キャンセル」から選択します。保護されたUI Mapとの共有は確認対象外です。

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
