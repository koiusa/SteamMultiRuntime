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

Package Managerから必要なサンプルをImportし、使用するBuild ProfileへSceneを追加します。
パッケージ使用側では、Sceneは通常`Assets/Samples/Steam Multi Runtime/<version>/`へコピーされます。

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
