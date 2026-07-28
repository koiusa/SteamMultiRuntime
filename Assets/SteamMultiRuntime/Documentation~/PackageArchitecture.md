# 内部パッケージ構成

Steam Multi Runtimeの機能パッケージは、`com.koiusa.steammultiruntime.core`および`com.koiusa.steammultiruntime.localization`へ向かう一方向依存で構成します。CoreとLocalizationは互いに依存しない並列の共通基盤とし、共通パッケージからLobby、ResourceLoader、Character UIなどの機能パッケージは参照しません。

汎用Unity基盤は`com.koiusa.system.core`、Input System前提の汎用入力基盤は`com.koiusa.input.core`、汎用UIは`com.koiusa.ui.common`が所有します。

各内部パッケージは`package.json`とasmdefを持ち、asmdefの内部参照はmanifestにも宣言します。Editor専用アセンブリをRuntimeから参照してはいけません。

Localization APIは次のnamespaceから利用します。

```csharp
using Koiusa.SteamMultiRuntime.Localization;
```
