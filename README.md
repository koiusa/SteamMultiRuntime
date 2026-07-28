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
   - `Shared Sample Assets`
   - `Build Profile Scene Preset`
2. `File > Build Profiles`から使用するBuild Profileを作成または選択します。
3. `Tools > SteamMultiRuntime > Build > Build Profile Scenes`を開きます。
4. `Scene Preset`へ`ThirdPersonView_BuildPreset`を指定し、`Apply Preset`を押します。
5. Third PersonのLobby Sceneを開いてPlayします。

パッケージ使用側のSceneは、通常
`Assets/Samples/Steam Multi Runtime/<version>/<sample name>/`へコピーされます。
パッケージ開発プロジェクトでは`Assets/SteamMultiRuntime/Samples/`にあります。

### 操作

| 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|
| 移動 | WASD / 矢印キー | 左スティック |
| カメラ | マウス移動 | 右スティック |
| ジャンプ | Space | A / × |
| ダッシュ | Left Alt | RT / R2 |
| スプリント | Left Shift | 左スティック押し込み |
| ロックオン | 中クリック | LT / L2 |
| 前／次のターゲット | 1 / 2 | D-pad左／右 |
| グラップル | 右クリック | RB / R1 |
| メニュー | Tab | Start / Options |

すべてのGameplay／UI操作は[Input Bindings](Documentation/InputBindings.md)を参照してください。

### 詳細なセットアップと更新

[サンプルのImport・Build Profile設定・更新手順](Assets/SteamMultiRuntime/Documentation~/Samples.md)

機能単位のサンプルは`Assets/SteamMultiRuntime/Samples/Features/<機能名>/`にあります。
一覧と追加規約は[Samples README](Assets/SteamMultiRuntime/Samples/README.md)を参照してください。

Localization Setup
------------------

ローカライズを使用する場合は、パッケージを参照する側のプロジェクトで初期設定が必要です。

[日本語・英語ローカライズの導入手順](Assets/SteamMultiRuntime/Documentation~/Localization.md)

Documentation
-------------

- [現在のクラス構成](Documentation/CurrentClassStructure.md)
- [Traversal Architecture](Documentation/TraversalArchitecture.md)
- [Camera Architecture](Documentation/CameraArchitecture.md)
- [Player Gameplay Architecture](Documentation/PlayerGameplayArchitecture.md)
- [NPC Architecture](Documentation/NpcArchitecture.md)
- [Character Architecture](Documentation/CharacterArchitecture.md)
- [Session Architecture](Documentation/SessionArchitecture.md)
- [Editor Specification](Documentation/EditorSpecification.md)
- [Package Architecture](Documentation/PackageArchitecture.md)
- [Keyconfig](Documentation/Keyconfig.md)
- [TargetingSystem](Documentation/TargetingSystem.md)
- [Input Bindings](Documentation/InputBindings.md)
- [Sample Setup](Assets/SteamMultiRuntime/Documentation~/Samples.md)

Development Notes
-----------------

- [Development Notes](Documentation/DevelopmentNotes.md)
