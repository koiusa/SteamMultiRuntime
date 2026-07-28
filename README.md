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

ローカライズを使用する場合は、パッケージを参照する側のプロジェクトで初期設定が必要です。  
[日本語・英語ローカライズの導入手順](Documentation/LOCALIZATION.md)

サンプルシーンをビルド対象に追加します。  
1. Unityエディタのメニューから `File > BuildProfiles` を表示してビルドプロファイルを作成。
2. BuildProfiles ファイルを開き、以下のサンプルシーンを追加：
   - 配下のシーンをすべて追加
     - `Assets/SteamMultiRuntime/Samples/Common/<シーン名>.unity`
   - ゲームシーン: 以下のいずれかを選択
     - `Assets/SteamMultiRuntime/Samples/SteamMultiPlayer_<ビューモード>/<シーン名>.unity`
3. Unityエディタでサンプルシーンをプレイ。  

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
- [ローカライズ導入手順](Documentation/LOCALIZATION.md)

Development Notes
-----------------

- [Development Notes](Documentation/DevelopmentNotes.md)
