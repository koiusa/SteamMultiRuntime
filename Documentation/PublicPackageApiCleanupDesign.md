# 公開パッケージ API 整理設計

## 目的

公開済み汎用パッケージの次期メジャーAPIとして、位置引数が多く変更に弱い呼び出しを整理する。
型数を減らすこと自体や、利用範囲の広い型を無理に共通化・ネストすることは目的にしない。

## 対象

### `com.koiusa.input.core`

`UiNavigationInputSession` は入力元、ハンドラー、任意設定を分ける。入力元は標準pathを解決する
`InputActionsConfig`またはNavigate専用`InputAction`、ハンドラーは`UiNavigationInputHandlers`、
閾値・repeat・UI Toolkit event制御は`UiNavigationInputOptions`で表す。長い既存コンストラクタは削除する。

### `com.koiusa.keyconfig`

`KeyConfigBindingRowFactory.Create` は行固有値と描画中に共有する依存を別オブジェクトへ分ける。
いずれもinternal実装型とし、公開APIを増やさない。

`InputBindingService.BindingEntry` は`InputAction`と`InputBinding`から自身の値を組み立てる。
呼び出し側が15個の派生値を正しい順序で渡す構造を廃止する。

## 非対象

- `InputGuideConfiguration`の5引数は一つの設定スナップショットを表すため維持する。
- `IInputGuideOverlay`は現時点で単一実装・単一ライフサイクルであり、インターフェース分割は行わない。
- デバイス判定はKeyconfigとInput Guideで意味が異なるため、共通Utilityへ引き上げない。
- 公開型のネストや削除は行わない。

## 互換性とバージョン

- `com.koiusa.input.core`の次回リリースは破壊的変更として`0.3.0`を予定する。
- 利用パッケージは新コンストラクタへ一括移行する。公開時に依存バージョンを一括で揃える。
- Keyconfigの変更はinternal実装に限定する。

## 検証

- navigation-only利用箇所が短いオーバーロードを使用していることを検索で確認する。
- Keyconfigの行生成に長い位置引数が残っていないことを確認する。
- KeyconfigとInput GuideのEditorテストを実行する。
