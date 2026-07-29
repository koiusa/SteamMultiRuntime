# SteamMultiRuntime

[![GitHub commit activity](https://img.shields.io/github/commit-activity/m/koiusa/SteamMultiRuntime)](https://github.com/koiusa/SteamMultiRuntime/graphs/commit-activity)
[![GitHub issues](https://img.shields.io/github/issues/koiusa/SteamMultiRuntime)](https://github.com/koiusa/SteamMultiRuntime/issues)
[![GitHub license](https://img.shields.io/github/license/koiusa/SteamMultiRuntime)](https://github.com/koiusa/SteamMultiRuntime/blob/main/LICENSE.md)

Install
-------

- Scoped Registry (UPM)
  - Add a scoped registry to your project.
    - URL: `https://registry.npmjs.com`
    - Scope: `com.koiusa`
  - Install SteamMultiRuntime in Package Manager.

Usage
-----

### サンプルの実行

1. Package Managerから次のサンプルをImportします。
   - `Gameplay Sample`
   - `Steam Multiplayer - Third Person`
2. `File > Build Profiles`から使用するBuild Profileを作成または選択します。
3. Build ProfileのScene Listへ、Projectウィンドウから次のSceneをドラッグして追加します。
   - `Steam Multiplayer - Third Person/SampleLobbyScene_ThirdPersonView_Traversal.unity`
   - `Gameplay Sample/Startup/UnityLogo.unity`
   - `Gameplay Sample/Startup/WelcomeScene.unity`
   - 使用する`Gameplay Sample/Stages/<Stage>.unity`
4. 追加したSceneが有効になっていることを確認します。
5. `SampleLobbyScene_ThirdPersonView_Traversal.unity`を開いてPlayします。

### System／デバッグ操作

| 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|
| 入力ガイド表示切替 | F1 | DualShock Touchpad |
| キャラクターデバッグ表示切替 | F2 | L3 2回押し |
| デバッグ用セッションメニュー | F3 | Select / Share |
| ゲーム終了／EditorのPlay Mode終了 | Escape | — |

Localization Setup
------------------

ローカライズを使用する場合は、パッケージを参照する側のプロジェクトで初期設定が必要です。

[日本語・英語ローカライズの導入手順](Assets/SteamMultiRuntime/Documentation~/Localization.md)

Documentation
-------------

- [ドキュメント索引](Documentation/README.md) — 設計資料、設定資料、運用手順の一覧
- [現在のクラス構成](Documentation/CurrentClassStructure.md) — Runtime全体の概要
- [パッケージ構成](Documentation/PackageArchitecture.md) — 依存方向と境界の正本
- [入力一覧](Documentation/InputBindings.md) — Gameplay／UI／System操作
- [Sample Setup](Assets/SteamMultiRuntime/Documentation~/Samples.md) — Import、Build Profile、更新手順

Development Notes
-----------------

- [開発時に判明した環境依存の注意事項](Documentation/DevelopmentNotes.md)
