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

サンプルシーンをビルド対象に追加します。  
1. Unityエディタのメニューから `Assets > Create > SteamMultiRuntime > BuildProfiles` を表示してビルドプロファイルを作成。
2. BuildProfiles ファイルを開き、以下のサンプルシーンを追加：
   - 配下のシーンをすべて追加
     - `Assets/SteamMultiRuntime/Samples/Common/<シーン名>.unity`
   - ゲームシーン: 以下のいずれかを選択
     - `Assets/SteamMultiRuntime/Samples/SteamMultiPlayer_<ビューモード>/<シーン名>.unity`
3. Unityエディタでサンプルシーンをプレイ。  
