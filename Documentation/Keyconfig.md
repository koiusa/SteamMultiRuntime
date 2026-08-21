# Keyconfig

Keyconfigは、再利用可能な汎用パッケージとSteamMultiRuntime固有のLocalization接続に分離しています。

## パッケージ構成

| パッケージ | 責務 |
|---|---|
| `com.koiusa.keyconfig` | Input Systemの一覧表示、入力監視、リバインド、保存、アイコン解決 |
| `com.koiusa.steammultiruntime.keyconfig` | Keyconfigを`GameLocalization`へ接続 |

汎用KeyconfigはSteamMultiRuntimeを参照しません。SteamMultiRuntime側が`IKeyConfigLocalizer`を実装して接続します。

`Provider`を設定しない場合は、OSのUI言語を初期値とする日本語・英語の内蔵ローカライザーを使用します。実行中の切替は
`KeyConfigLocalization.BuiltInLocale = KeyConfigLocale.Japanese`（または`English`）で行えます。独自の翻訳を使う場合は
`KeyConfigLocalization.Provider`へ`IKeyConfigLocalizer`実装を設定し、言語変更時にその`LocaleChanged`を発火してください。
Action Map、Action、Composite、スキーム、プロファイルの各名前は元の名前をキーとしてProviderへ渡されます。

## npm / Unity Package Managerからの導入

`com.koiusa.input.core`、`com.koiusa.application`、`com.koiusa.ui.core`、`com.koiusa.keyconfig`はnpmレジストリへ公開します。ソースの正本はこのリポジトリ内の各パッケージディレクトリだけとし、公開用リポジトリへ複製しません。`com.koiusa.editor-tools`はSteamMultiRuntime本体へ同梱しますが、単独の公開対象には含めません。

Unityから利用するプロジェクトでは、`Packages/manifest.json`の`scopedRegistries`へnpmを登録します。

```json
{
  "scopedRegistries": [
    {
      "name": "npmjs",
      "url": "https://registry.npmjs.org",
      "scopes": ["com.koiusa"]
    }
  ],
  "dependencies": {
    "com.koiusa.keyconfig": "0.1.6"
  }
}
```

通常のnpmクライアントでは`npm install com.koiusa.keyconfig`で取得できます。ただし内容はUnity Package Manager向けのC#とAssetであり、JavaScriptライブラリとしてのAPIは提供しません。

公開はGitHub Actionsの`Publish reusable Unity packages to npm`を手動実行します。既定はdry-runで、成果物の内容だけを検証します。実公開時は`publish`を有効にし、`input.core`、`application`、`ui.core`、`keyconfig`の未公開バージョンだけを依存順で公開します。`NPM_TOKEN` secretが必要です。

`main`へpushすると`Create UPM Release`が自動実行されます。手動実行では`publish=false`でdry-run、`publish=true`で実公開を選択できます。このWorkflowは先に同じ再利用パッケージ検証・公開Workflowを呼び出し、成功後に本体のnpmパッケージ、署名済みUPMアーカイブ、GitHub Releaseを処理します。既に存在するnpmバージョンやGitHub Releaseは個別にスキップするため、途中で失敗しても再実行できます。タグはGitHub Release作成時に生成されます。

公開対象の現在バージョンは、`com.koiusa.input.core`が`0.2.0`、`com.koiusa.application`が`0.2.0`、`com.koiusa.keyconfig`が`0.1.6`です。Keyconfigは`input.core` 0.2.0を推移依存として導入します。

### パッケージ境界の判断

`input.core`と`ui.core`はKeyconfig以外の画面やGameplay入力からも利用される共有基盤なので、Keyconfigへ統合しません。3パッケージを個別に公開し、利用者は`keyconfig`だけを直接指定して残りを推移依存として解決する構成を維持します。

一方、入力ガイドと内蔵アイコンはKeyconfigのリバインド機能そのものには必須ではなく、配布容量の大部分を占めます。初回公開後に互換性を壊して分離することを避けるため、正式な`1.0.0`公開前に次を判断します。

- リバインドUIと文字列フォールバックを`com.koiusa.keyconfig`へ残す。
- `InputGuideOverlay`、デバイスレイアウト、標準アイコンを任意の`com.koiusa.keyconfig.icons`へ分離する。
- SVG原稿はRuntime配布物へ含めず、生成元またはDocumentation用Assetとして管理する。
- アイコンの出典、改変条件、npmでの再配布可否を確定し、ライセンス文書を同梱する。

現状のnpm tarballは、`input.core`が約5 KB、`ui.core`が約4 KB、`keyconfig`が約1.9 MBです。Keyconfigの展開後約4.3 MBのうち、SVG原稿が約2.0 MB、PNGが約1.7 MBを占めます。このため、パッケージ数を減らすよりアイコンを任意依存へ分離する方が効果的です。

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
- 実行中に日本語／英語を切り替え、標準ボタン、Action Mapタブ、Action、Composite、スキーム、プロファイル見出しが即時更新されることを確認します。
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
`InputGuideOverlay`のMap Filterは`All`、`EnabledOnly`、`Specified`から選択でき、複数MapはMap名ごとのセクションとして表示されます。実行時は`SetActionMaps`、`SetMapFilter`、`Refresh`で対象を変更できます。Map名とAction名は`KeyConfigLocalization`でローカライズされ、Mapの有効状態またはBinding変更時には表示と入力ハイライトが再構築されます。従来の`actionMapName`は`Specified`で複数Map名が空の場合のフォールバックとして維持されます。
操作一覧は画面上部の全幅を使い、各Mapを1列としてスクロールなしで横一列に表示します。

## 入力アイコン

Keyboard、Mouse／Pointer、Gamepad、DualShock、基本的なJoystick入力は内蔵アイコンへ解決されます。Touchscreen、Pen、XR Controller、および`*/{Submit}`のようなデバイス共通Bindingは、専用画像または実デバイスが特定できないため文字列表示になる場合があります。

## 共通Scrollbar

Scrollbarは`com.koiusa.ui.core`の`KoiusaScrollView.uss`を共有します。個別画面で太さを上書きせず、共通USSを修正します。

## 資産の配置

- 汎用パッケージの基本サンプルは`com.koiusa.keyconfig/Samples/Basic`に置きます。
- SteamMultiRuntime本番設定は`Assets/SteamMultiRuntime/Runtime/Configs/Input`に置きます。
- 本番設定を使う機能サンプルSceneは`Assets/SteamMultiRuntime/Samples/Features/Keyconfig`に置きます。

Keyconfigの`PanelSettings`、Runtime Theme、UITK Text Settings、Noto Sans JPの
動的Font Assetとソースフォントはすべて`com.koiusa.keyconfig`内で所有します。
Basic SampleはこのRuntime Assetを直接参照するため、SampleのImport以外に利用側の
PanelSettingsやフォント設定を必要としません。EditorテストはTheme、Text Settings、
日本語フォールバックが解決できることを検証します。
