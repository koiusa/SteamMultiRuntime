# Documentation

SteamMultiRuntimeの設計・運用資料の索引です。

初めて構成を確認するときは[Current Class Structure](CurrentClassStructure.md)から読み、変更対象の領域別資料へ進んでください。パッケージの追加、依存方向の変更、ドメイン間接続を扱う場合は、先に[Package Architecture](PackageArchitecture.md)を確認してください。

## 全体像と境界

| 文書 | 内容 |
|---|---|
| [Current Class Structure](CurrentClassStructure.md) | Runtime全体のクラス配置、責務、主要な処理経路 |
| [Package Architecture](PackageArchitecture.md) | パッケージ境界、依存方向、型付き接続、リフレクション方針の正本 |
| [Editor Specification](EditorSpecification.md) | Inspector、補助Window、Repair操作の契約 |

`CurrentClassStructure.md`は全体を案内する概要です。領域固有の詳細は、以下のArchitecture文書を正本とします。

## 領域別Architecture

| 領域 | 正本文書 |
|---|---|
| Player移動、Wall／Ladder／Wire Traversal | [Traversal Architecture](TraversalArchitecture.md) |
| Player Skill、Combat、Guard | [Player Gameplay Architecture](PlayerGameplayArchitecture.md) |
| NPCのNavMesh、Local／Network駆動 | [NPC Architecture](NpcArchitecture.md) |
| Character Profile、Model、選択UI、表示名 | [Character Architecture](CharacterArchitecture.md) |
| Lobby、Session、Stage選択、Scene遷移 | [Session Architecture](SessionArchitecture.md) |
| Camera切替、入力、障害物回避、Compass | [Camera Architecture](CameraArchitecture.md) |
| メニュー、フォーカス、UI入力の所有権 | [UI Architecture](UiArchitecture.md) |

## 機能・設定リファレンス

| 文書 | 内容 |
|---|---|
| [Input Bindings](InputBindings.md) | 本番Input Actionの操作一覧 |
| [Keyconfig](Keyconfig.md) | リバインド、保存、入力アイコン、Input Guideの配置と操作 |
| [Targeting System](TargetingSystem.md) | Lock-On実装、入力接続、シーン設定 |

## セットアップと運用

| 文書 | 内容 |
|---|---|
| [Sample Setup](../Assets/SteamMultiRuntime/Documentation~/Samples.md) | SampleのImport、Build Profile、更新手順 |
| [Localization Setup](../Assets/SteamMultiRuntime/Documentation~/Localization.md) | Localizationの導入手順 |
| [Development Notes](DevelopmentNotes.md) | 計測結果や環境依存の注意事項をまとめた事象別ノートの索引 |

機能単位のSample一覧と追加規約は[Samples README](../Assets/SteamMultiRuntime/Samples/README.md)を参照してください。

## 履歴資料

| 文書 | 内容 |
|---|---|
| [全体実装レビュー（2026-07-30）](../IMPLEMENTATION_REVIEW.md) | 現行実装の横断レビュー、改善候補、検証状況。現行仕様の正本ではありません。 |

## 文書を更新するとき

- クラスの追加・移動や主要な処理経路の変更は、領域別の正本文書と`CurrentClassStructure.md`へ反映する。
- パッケージ、asmdef、依存方向、接続方式の変更は`PackageArchitecture.md`へ反映する。
- Input Actionまたは既定Bindingの変更は`InputBindings.md`と、必要に応じてルート`README.md`の操作表へ反映する。
- Inspector、Editor Window、Repair操作の変更は`EditorSpecification.md`へ反映する。
- Sampleの導入手順、Scene List、更新手順の変更はPackage内の`Documentation~/Samples.md`へ反映する。
- 開発中に確認した個別事象は`DevelopmentNotes/`へファイルを分け、`DevelopmentNotes.md`の索引からリンクする。
- 検証前の計画や未確認事項は、検証済みの現行仕様と区別して記載する。

