# 内部パッケージ構成

Steam Multi Runtimeの機能パッケージは、`com.koiusa.steammultiruntime.core`および`com.koiusa.steammultiruntime.localization`へ向かう一方向依存で構成します。CoreとLocalizationは互いに依存しない並列の共通基盤とし、共通パッケージからLobby、ResourceLoader、Character UIなどの機能パッケージは参照しません。

Lobbyはバックエンド非依存の`com.koiusa.steammultiruntime.lobby`、NGO連携の`com.koiusa.steammultiruntime.lobby.netcode`、Steamworks実装の`com.koiusa.steammultiruntime.lobby.steam`に分割します。依存はSteamからLobby／Lobby Netcodeへ向け、逆方向の参照は作りません。

Character選択UIは`com.koiusa.steammultiruntime.character.ui`、プレイヤー名などPlayer固有UIは`com.koiusa.steammultiruntime.player.ui`が所有します。Player UIは`player`の契約だけを参照し、`player.netcode`へ直接依存しません。

Characterモデル一覧とプロフィール契約は`com.koiusa.steammultiruntime.character`が所有します。`character.ui`と`resourceloader`はCharacterへ依存しますが、CharacterからUIまたはロード実装は参照しません。

汎用Unity基盤は`com.koiusa.system.core`、Input System前提の汎用入力基盤は`com.koiusa.input.core`、汎用UIは`com.koiusa.ui.common`が所有します。

各内部パッケージは`package.json`とasmdefを持ち、asmdefの内部参照はmanifestにも宣言します。Editor専用アセンブリをRuntimeから参照してはいけません。

Localization APIは次のnamespaceから利用します。

```csharp
using Koiusa.SteamMultiRuntime.Localization;
```
