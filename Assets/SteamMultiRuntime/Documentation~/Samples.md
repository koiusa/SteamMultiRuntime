# サンプルの導入とBuild Profile設定

Steam Multi RuntimeのサンプルSceneは、パッケージをインストールしただけではBuild Settingsへ追加されません。
Package Managerから必要なサンプルをImportし、使用するBuild ProfileへSceneを登録してください。

## サンプルのImport

Package Managerで`Steam Multi Runtime`を選び、用途に応じて次のサンプルをImportします。

| サンプル | 内容 |
|---|---|
| `Gameplay Sample` | Stage、`UnityLogo`、`WelcomeScene` |
| `Steam Multiplayer - Third Person` | Third Person用Lobby／Local Scene |
| `Keyconfig - Production Input` | 本番Input Actionsを使用するKeyconfig確認Scene |
| `Targeting System - Production Input` | 本番Input Actionsを使用するTargeting確認Scene |
| `Steam Multiplayer - Quarter View` | Quarter View用Lobby／Local Scene |
| `Steam Multiplayer - Server` | Dedicated Server用Lobby Scene |
| `Build Profile Scene Preset` | Build Profile編集Tool検証用の補助Preset |

一覧は主な利用頻度が高い順です。Third Personサンプルを実行する場合は、上位2サンプルを
Importしてください。Scene登録はUnity標準のBuild Profile画面から手動で行います。

Importされたファイルは、通常は次の場所へコピーされます。

```text
Assets/Samples/Steam Multi Runtime/<version>/<sample name>/
```

`Packages/com.koiusa.steammultiruntime/...`や、パッケージ開発プロジェクト内の
`Assets/SteamMultiRuntime/...`を使用側プロジェクトから直接参照しないでください。

## Build ProfileへのScene登録

Unity 6では、Build Profileが固有のScene一覧を使用している場合、Global Build Settingsではなく
そのBuild Profileの一覧へSceneを追加する必要があります。

1. `File > Build Profiles`から使用するBuild Profileを作成または選択します。
2. Build Profileが固有のScene Listを使用する場合は、そのScene Listを表示します。
3. Projectウィンドウから次のSceneをScene Listへドラッグして追加します。
   - 使用するMultiplayerのLobby Scene
   - `Gameplay Sample/Startup/UnityLogo.unity`
   - `Gameplay Sample/Startup/WelcomeScene.unity`
   - 使用する`Gameplay Sample/Stages/<Stage>.unity`
4. 各Sceneが有効になっていることと、旧バージョンのパスがないことを確認します。

`Build Profile Scene Preset`と`Tools > SteamMultiRuntime > Build > Build Profile Scenes`は
補助的なEditor Toolです。現在は完全な構成を保証しないため、標準セットアップには使用せず、
UnityのBuild Profile画面でSceneを手動登録してください。

## パッケージ更新時

UPMサンプルはバージョン別フォルダーへコピーされるため、パッケージを更新しても以前Importした
`Assets/Samples/.../<old version>/`は自動更新・削除されません。

1. Unityを終了するか、Play Modeを終了します。
2. 使用側プロジェクトの旧バージョンSampleフォルダーを削除します。
3. Package Managerから新バージョンの必要なサンプルをImportします。
4. 各Build Profileへ新しいSample Sceneを手動で登録し直します。
5. Build Profile内に旧バージョンや存在しないSceneパスが残っていないことを確認します。

旧版と新版に同名Sceneが同時に存在すると、Scene名による検索結果が曖昧になります。
Build ProfileへSceneを登録する前に旧バージョンのSampleを削除してください。

## `Scene is not in Build Settings`エラー

たとえば次のエラーは、Sceneファイルが存在しないという意味ではなく、現在有効なBuild Profileから
そのSceneをロードできない場合にも発生します。

```text
[SteamLobbySceneLoader] Scene 'UnityLogo' is not in Build Settings.
```

次を確認してください。

- `Gameplay Sample/Startup/UnityLogo.unity`がImportされている
- 現在使用中のBuild Profileに新しい`UnityLogo.unity`が登録され、有効になっている
- Build Profileが固有Scene一覧を使う場合、その一覧に登録している
- `Assets/Samples/.../<old version>/`への参照が残っていない
- Scene移動直後の場合はPlay Modeを終了し、Build Profileへ新しいSceneを登録してから再実行している
