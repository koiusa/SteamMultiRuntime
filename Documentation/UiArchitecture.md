# UI Architecture

この文書をメニュー入力、フォーカス移動、Actionの所有権に関する共通仕様の正本とします。

## 原則

1. 通常のButton、Dropdown、TextFieldはEventSystem標準ナビゲーションだけを使う
2. 行、グリッド、タブなど独自移動が必要なUIは`UiNavigationInputSession`を使う
3. UI側で`InputAction.Enable`／`Disable`、長押し時間、デッドゾーンを再実装しない
4. 1つの画面でEventSystem標準移動と独自移動を同時に実行しない
5. 画面を閉じるときは入力Sessionを破棄し、共有Actionの取得前の有効状態を復元する

## 共通入力

`com.koiusa.input.core` が次を所有します。

| 型 | 責務 |
|---|---|
| `InputActionLease` | 共有Actionの参照数と取得前の有効状態を管理 |
| `InputActionBinding` | performed CallbackとLeaseの寿命を1つにまとめる |
| `UiNavigationInputSession` | Navigate方向判定、初回移動、長押しリピート、Submit、Cancelを管理 |

`UiNavigationInputSession`の標準値は、方向しきい値0.5、リピート開始0.4秒、リピート間隔0.1秒です。画面は`UiNavigationDirection`のMove、Submit、CancelのCallbackだけを渡します。Session自身がInput Systemの更新後に保持方向とリピート時刻を評価するため、各画面に入力用`Update`は置きません。Up／Down／Left／Rightを保持するため、縦リスト、横タブ、2次元グリッドで同じSessionを使えます。独自入力を使う場合は対象`VisualElement`を渡し、EventSystemの重複`NavigationMoveEvent`／`NavigationSubmitEvent`／`NavigationCancelEvent`をSession内で消費します。

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
- 独自選択UIなら、表示時に`UiNavigationInputSession`を作成します。入力更新とリピートはSessionが管理します。
- 非表示時にSessionを`Dispose`します。
- Input Action名、リピート、Event消費を各UIにコピーしません。
