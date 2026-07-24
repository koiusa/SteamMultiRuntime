# 最新実装レビュー

レビュー対象: `cfa46a0`（パッケージ 0.5.11）

レビュー日: 2026-07-24

## 確認した変更

- 梯子面を基準にキャラクターの向きを補正する処理を追加
- `IsLadder` / `LadderSpeed` を Animator へ反映し、サンプル用梯子アニメーションを追加
- `LocalManager` の再スポーン時に旧プレイヤーを先に無効化し、二重スポーン中の干渉を抑制
- `FaceAnimationDriver` の生成リソース解放を補強
- ローカル用ステージ選択 UI のシーンロード処理を修正
- パッケージを 0.5.11、Unity 要件を 6000.3.9f1 へ更新

## 改善候補

### P0: リリース前に対応

1. **AppID の解決処理を一本化する**
   - Play Mode／通常ビルドは `STEAM_APP_ID`、`FACEPUNCH_STEAM_APP_ID`、ローカルファイルの順で解決します。
   - macOS 後処理は `STEAM_APP_ID` だけを参照し、未指定時は無条件に `480` を書き込みます。
   - Transport に設定された AppID と `steam_appid.txt` が食い違う可能性があります。共通 Utility を利用し、本番構成で暗黙の `480` を許可しない方針が安全です。

2. **ビルド失敗／キャンセル時にもシーンを復元する**
   - Build Hook は有効なシーンを実際に保存して AppID を書き換え、Postprocess で復元します。
   - ビルドが Postprocess まで到達しない場合、変更値がシーンに残る恐れがあります。`BuildPlayerWindow.RegisterBuildPlayerHandler` 等で `try/finally` を保証するか、`IProcessSceneWithReport` でビルド用コピーだけを書き換える設計を推奨します。

### P1: 次の開発サイクル

3. **Editor / PlayMode テストを追加する**
   - 現在、製品コードを検証する Test Assembly は見当たりません。
   - 優先対象は AppID の優先順位と復元、LocalManager の連続シーン変更、梯子進入／退出と上下速度、非ループアニメーション終了判定です。

4. **LocalSceneFlowLoader の未実装契約を整理する**
   - ローカル版ではロビー退出／ロビーシーン設定に関する複数メソッドが警告のみです。
   - 呼び出し可能なインターフェースを分割するか、ローカル版の期待動作を実装し、実行時に初めて未対応と判明する状態を解消します。

5. **パッケージ依存関係を再確認する**
   - Runtime の asmdef／コードは Input System、AI Navigation、UI Toolkit、Facepunch Transport なども利用しますが、ルート `package.json` の dependencies だけでは依存元が分かりにくい構成です。
   - 埋め込みパッケージに依存する設計を維持する場合も、直接依存と第三者ライセンスを一覧化し、UPM のクリーンプロジェクトへの導入テストを自動化すべきです。

### P2: 保守性改善

6. **命名とディレクトリの誤記を段階的に修正する**
   - `Locomoter`、`ForcusMerker`、`Asstes`、`WelcomScene`、`Suece` などの表記揺れがあります。
   - Unity の GUID を保ったまま Editor 上で移動／改名し、公開 API は `FormerlySerializedAs` や互換ラッパーで段階移行します。

7. **診断ログを構造化する**
   - 一部ログには型名プレフィックスがありますが、形式が統一されていません。
   - 共通カテゴリ、AppID の取得元、シーン名、NetworkRole を含めると、ユーザー環境での切り分けが容易になります。AppID 以外のアカウント情報はログへ出さない運用も明記します。

8. **サンプルと Runtime の境界を明確にする**
   - NPC、デバッグ表示、テスト用 Mover など prototype 要素が配布 Runtime 配下にあります。
   - Samples または明示的な optional assembly へ分けることで、コンパイル時間と公開 API 面積を減らせます。

## 検証チェックリスト

- Unity 6000.3.9f1 で再インポート後、Console にコンパイルエラーがない
- AppID ローカルファイル、両環境変数の優先順位が期待どおり
- ビルド成功、失敗、キャンセルの各経路でシーン差分が残らない
- Windows／macOS／Linux の成果物で Steam ネイティブライブラリをロードできる
- Host、Client、Dedicated Server、ローカル実行でステージ遷移できる
- 梯子の両側から進入して正しい向きになり、上昇／下降アニメーションが切り替わる
- シーンを連続切り替えしてローカルプレイヤーが一体だけ残る

## 今回の検証範囲

ソース、パッケージ定義、Build Settings、直近コミットを静的に確認しました。Unity Editor を使う再インポート、PlayMode、Steam 実アカウント接続、各 OS のプレイヤービルドは未実施です。
