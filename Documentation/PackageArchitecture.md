# Package Architecture

Steam Multi Runtimeの内部パッケージは、機能パッケージ同士の横断参照を避け、共通基盤へ向かう一方向依存にします。

```text
Feature packages
  -> com.koiusa.steammultiruntime.localization
  -> com.koiusa.steammultiruntime.core
  -> com.koiusa.system.core / com.koiusa.input.core / com.koiusa.ui.common
  -> Unity packages
```

## 共通基盤

- `com.koiusa.system.core`: Unityプロジェクト全般で再利用できるシステム機能。Steam、Netcode、ゲームプレイ固有型を含めない。
- `com.koiusa.input.core`: Input Systemを前提とする汎用入力設定とActionライフタイム管理。
- `com.koiusa.ui.common`: 特定ゲームやLocalization実装に依存しない汎用UI機能。
- `com.koiusa.steammultiruntime.core`: Steam Multi Runtime内で共有する契約、属性、ゲームプレイ共通基盤。
- `com.koiusa.steammultiruntime.localization`: Steam Multi Runtime共通のLocalizationランタイム、カタログ、UI Toolkitバインディング、導入ツール。Coreとは独立した横断機能として扱う。
- `com.koiusa.steammultiruntime.lobby`: バックエンド非依存のLobby契約、シーンフロー、ローカルLobby UI。
- `com.koiusa.steammultiruntime.lobby.netcode`: Netcode for GameObjectsによるセッションとシーン同期。
- `com.koiusa.steammultiruntime.lobby.steam`: Steamworks LobbyバックエンドとSteam固有UI。LobbyおよびLobby Netcodeへ一方向に依存する。
- `com.koiusa.steammultiruntime.character.ui`: Character選択などCharacter固有UI。
- `com.koiusa.steammultiruntime.character`: Characterモデル一覧、プロフィール、モデル同期の共通契約。ロード方式には依存しない。
- `com.koiusa.steammultiruntime.player.ui`: プレイヤー名表示などPlayer固有UI。Netcodeへ直接依存しない。

## 依存ルール

1. 機能パッケージから別の機能パッケージを参照するのは、実装合成パッケージと明示的な拡張パッケージに限定する。
2. 共通パッケージから機能パッケージを参照しない。
3. Editor専用パッケージをRuntime asmdefから参照しない。
4. asmdefで内部パッケージを参照した場合、`package.json`にも同じ依存を宣言する。
5. パッケージ共通型は利用箇所が2つ以上あることだけで移動せず、ドメイン所有者が共通基盤である場合に限って移す。
6. Unityアセット参照を維持するため、ファイル移動時は`.meta`を同時に移動する。
7. Character UIはCharacterへ、ResourceLoaderはCharacterへ依存する。CharacterからUIやResourceLoaderを参照しない。

## Localization API

Localization APIのnamespaceは次です。

```csharp
using Koiusa.SteamMultiRuntime.Localization;
```

Localization機能の導入方法は[LOCALIZATION.md](LOCALIZATION.md)を参照してください。
