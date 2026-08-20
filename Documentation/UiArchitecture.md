# UI Architecture

この文書をメニュー入力、フォーカス移動、Actionの所有権に関する共通仕様の正本とします。

## 原則

1. 通常のButton、Dropdown、TextFieldはEventSystem標準ナビゲーションだけを使う
2. 行、グリッド、タブなど独自移動が必要なUIは`UiNavigationInputSession`を使う
3. UI側で`InputAction.Enable`／`Disable`、長押し時間、デッドゾーンを再実装しない
4. 1つの画面でEventSystem標準移動と独自移動を同時に実行しない
5. 画面を閉じるときは入力Sessionを破棄し、共有Actionとカーソル表示の取得前状態を復元する
6. 複数画面が存在する場合も、入力を処理するのは最後に開いた最前面Sessionだけとする
7. メニュー表示と親子遷移は`IUiMenu`／`UiMenuNavigator`を通し、画面同士が直接Show／Hideを連鎖しない

## メニュー遷移

`com.koiusa.ui.core`の`IUiMenu`は表示状態、Activate、Deactivate、初期フォーカスだけを定義します。`UiMenuNavigator`はRoot表示、子メニューのPush、Backによる親メニュー復帰、シーン変更時のCloseAllを管理します。

Pause MenuからKey ConfigまたはCharacter Selectを開く場合はPushし、子画面を決定またはCancelで閉じるとPause Menuを再表示します。Character Select、Stage Select、Steam Lobbyをショートカットから直接開く場合はRootとして表示します。

Pause MenuからCharacter SelectをPushするときは`CharacterSelectMenuRegistry.Current`を使用します。Character Select Menuは有効期間だけRegistryへ登録し、複数の有効Menuが存在する設定をエラーにします。Pause MenuはScene全体から任意のMenuを探索しません。Registryは最初の有効Instanceを保持し、別Instanceによる重複登録や解除では現在値を変更しません。

## 共通入力

`com.koiusa.input.core` が次を所有します。

| 型 | 責務 |
|---|---|
| `InputActionLease` | 共有Actionの参照数と取得前の有効状態を管理 |
| `InputActionBinding` | performed CallbackとLeaseの寿命を1つにまとめる |
| `UiNavigationInputSession` | 最前面入力の排他、Navigate方向判定、長押しリピート、Submit、Cancel、カーソル表示を管理 |

`UiNavigationInputSession`の標準値は、方向しきい値0.5、リピート開始0.4秒、リピート間隔0.1秒です。画面は`UiNavigationDirection`のMove、Submit、CancelのCallbackだけを渡します。方向変化はInput ActionのCallbackで受信し、方向が保持されている間だけInput System更新後にリピート時刻を評価します。各画面に入力用`Update`は置きません。複数Sessionが一時的に存在しても、最後に作られたSessionだけがMove／Submit／Cancelを処理します。Sessionの参照数でカーソル表示も保持するため、背面UIの終了処理が前面UIのカーソルを隠すことはありません。Up／Down／Left／Rightを保持するため、縦リスト、横タブ、2次元グリッドで同じSessionを使えます。独自入力を使う場合は対象`VisualElement`を渡し、EventSystemの重複`NavigationMoveEvent`／`NavigationSubmitEvent`／`NavigationCancelEvent`をSession内で消費します。

`UI/Navigate`はEventSystem標準UIとの互換性を保つため`PassThrough`とします。独自UIの重複入力はAction Typeの変更ではなく、`UiNavigationInputSession`のイベント消費で抑止します。

## 既存UIの選択

| UI | 入力方式 |
|---|---|
| Pause Menu | `UiNavigationInputSession` |
| Character Select | `UiNavigationInputSession` |
| Stage Select | `UiNavigationInputSession` |
| Key Config | `UiNavigationInputSession` + タブ、行、列のフォーカス遷移 |
| Steam Lobby | `UiNavigationInputSession` + 複数ペインのフォーカス遷移 |

画面固有Controllerが必要でも、ActionのLease、方向判定、リピートを複製しないでください。フォーカス遷移だけを固有Controllerに残します。

## 新しいUIの追加

- 標準UIなら、GameObjectを有効化した後にUI Toolkitのschedulerから最初の操作要素へFocusを設定します。
- 新しいメニューは`IUiMenu`を実装し、外部公開のShow／Hide／Toggleを`UiMenuNavigator`へ委譲します。
- 独自選択UIなら、表示時に`UiNavigationInputSession`を作成します。入力更新とリピートはSessionが管理します。
- 非表示時にSessionを`Dispose`します。
- Input Action名、リピート、Event消費を各UIにコピーしません。

## デバッグUI

本番のCharacter Select、Stage Select、Steam Lobby Menu／Document／Loading Splashは、同一Composition Root内のシリアライズ参照または親子Componentだけを使用します。参照欠落時に別Sceneや別Runtime ProfileのUI／Serviceを`FindFirstObjectByType`で選びません。設定不備は起動時エラーとして扱います。Loading Splashの`PanelSettings`は専用設定を優先し、未設定時も他画面の`UIDocument`から借用せず、Splash自身がRuntime設定を所有します。

- Input GuideはF1入力を`InputActionBinding`で所有し、疑似デバイス表示とOperationパネルを同じInputActionAssetから構築します。`InputGuideOverlay`は入力監視と表示モードを、`InputGuideOperationPanel`はBinding Groupで絞った操作一覧の生成とデバイス別表示を所有します。Operationパネルは画面上部の全幅を使い、各Mapを1列としてスクロールなしで横一列に表示します。
- Character DebugのF2購読は`CharacterDebugToggleController`が所有し、NPC Spawn ManagerはNPC群の表示状態だけを所有します。
- `ServerScene`のCharacter Debugは初期非表示とし、必要なときだけF2で表示を切り替えます。
- Character Debugのテレメトリ更新は連続状態のため0.1秒間隔のポーリングを許可します。非表示時や表示担当でないInstanceはUIを更新しません。
- `CharacterDebugOverlay`はGameplay／Animator／Network参照をラベルから直接読みません。`ICharacterDebugSnapshotSource`が0.1秒ごとに再利用可能な`CharacterDebugSnapshot`へ値を取得し、UIはSnapshotだけを描画します。Animator Parameter定義とSnapshot内Listは再利用します。
- Character Debugの使用Character名と実行Mode（`Local`／`Host`／`Server`／`Client`）は、STATE／ANIMATIONタブに依存しない固定サマリーとしてタブの上へ表示します。Player Object名は上部のTarget Selectorへ表示します。
- Stage Select／Steam LobbyのF3は通常の`IUiMenu`遷移として扱い、Overlayとは分離します。

## World Space UI

Actorの名前・HP表示は専用Overlay Cameraで深度クリア後に描画します。距離フェードが完全に0になるActorは`ActorWorldSpaceOverlay`が`UIDocument`を停止し、Overlay Cameraの描画登録から外します。Cameraが表示距離内へ戻ったときだけ再登録するため、不可視の遠距離ActorをCamera CullingとUI描画へ残しません。
