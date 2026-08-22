# KeyConfig公開API再編

## 目的

`com.koiusa.keyconfig`の公開契約を最小化し、キー設定、入力アイコン、操作ガイドの責務を分離する。後方互換は維持せず、SteamMultiRuntime内のPrefab、Scene、設定Assetを新しい契約へ一括移行する。

## 完成時のパッケージ境界

現在のパッケージ境界と依存方向は[PackageArchitecture.md](PackageArchitecture.md)を正本とします。

## KeyConfig公開API

現在の公開API、内部クラスとの境界、クラス構成図は[KeyConfigArchitecture.md](KeyConfigArchitecture.md)を正本とします。

## Panelの責務

Panelは`KeyConfigController`だけを操作し、`InputBindingService`と`InputRebindController`を直接参照しない。

Panelは永続化先を所有しない。Load／SaveボタンはJSONの要求イベントを発行し、利用側が保存先を決める。開閉中の未保存変更復元用スナップショットは編集セッション責務としてPanelに残す。

## リバインド要件

- Escapeと5秒タイムアウトを区別して結果通知する。
- Compositeの途中キャンセルでは全パートを復元する。
- action/binding indexを公開しない。
- L1／R1登録直後のSection移動抑止を維持する。
- L2／R2の同一物理入力による別Control通知抑止を維持する。
- Update／onAfterUpdateの重複ポーリングを追加しない。

## 進捗

| 項目 | 状態 | 完了条件 |
|---|---|---|
| namespace・asmdefを`Koiusa.KeyConfig`へ統一 | 完了 | 旧namespace参照なし |
| 公開型の改名と旧MenuToggle除去 | 完了 | Panelが`IUiMenu`を直接実装 |
| GUIDベースControllerとDTO | 完了 | indexを公開せず取得・Reset・Rebind可能 |
| Controllerの修飾キー・競合・変更通知 | 完了 | UIに必要な操作をControllerが提供 |
| PanelをControllerへ一本化 | 完了 | Panelから低レベルService参照なし |
| 永続化責務をゲーム側へ移動 | 完了 | keyconfig RuntimeからファイルRepository削除 |
| タイムアウト結果の識別 | 完了 | Escapeとtimeoutのテストが別結果 |
| `com.koiusa.input.icons`分離 | 完了 | keyconfigとinputguideが共通Packageを参照 |
| `com.koiusa.inputguide`分離 | 完了 | keyconfigにInputGuide型・Assetなし |
| Prefab／Scene／Editor移行 | 実装完了・Unity検証待ち | Missing Scriptなし、旧型参照なし |
| 公開APIホワイトリストテスト | 完了 | 許可型以外がPublicでない |
| Unity Test Runner | KeyConfig EditMode完了 | EditMode／Runtime関連テスト成功 |

## 検証記録

- 分離前の変更はUnity 6000.3.9f1でコンパイル成功を確認済み。
- 分離後はUPM IPC接続失敗、`-noUpm`ではライセンス再接続待ちとなり、UnityコンパイルとTest Runnerを完了できていない。実行済みとして扱わない。
- Unity再ログイン後、通常ユーザー環境で`Koiusa.KeyConfig.Editor.Tests`を実行。29件中27件成功、2件失敗。結果は`TestResults/KeyConfig/editmode-results.xml`。
- 候補確定待機を無効化した修正後、同じEditModeテストを再実行して29件すべて成功。結果は`TestResults/KeyConfig/editmode-results-after-fix.xml`。
- Unity 6000.3.9f1でTriggerButtonエイリアス除外のEditModeテストに成功。
- Unity 6000.3.9f1で日本語Fallback FontのEditModeテストに成功し、終了後も配布元`Noto Sans JP SDF.asset`に差分がないことを確認。
- `com.koiusa.input.icons`と`com.koiusa.inputguide`は`npm pack --dry-run`に成功。
- パッケージ公開とGit commitは行わない。
