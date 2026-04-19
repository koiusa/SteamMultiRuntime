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
1. UnityのPackage Managerで SteamMultiRuntime の「Samples」から Common Sample Asset をインポート。
2. BuildProfiles をまだ作成していない場合は、Unityエディタのメニューから `Assets > Create > SteamMultiRuntime > BuildProfiles` を表示。
3. シーンリストに、インポートしたサンプル`Assets/Samples/SteamMultiRuntime/<バージョン>/Samples Common Assets`のシーンを追加。
4. 「Samples Steam Multi Player Simple」又は「Samples Steam Multi Player With Animator」をプレイ。  
