# Session Architecture

この文書をSteam Lobby、Stage選択、Scene遷移、Loading Splashに関する詳細仕様の正本とします。全体配置は[CurrentClassStructure.md](CurrentClassStructure.md)、Editor操作は[EditorSpecification.md](EditorSpecification.md)を参照してください。

## クラス構成

### 所有パッケージ

| パッケージ | 所有するもの |
|---|---|
| `com.koiusa.steammultiruntime.lobby` | バックエンド非依存のStage選択、Scene Flow、Local Loading Splash |
| `com.koiusa.steammultiruntime.lobby.netcode` | NGOセッション、Network Sceneの開始・停止 |
| `com.koiusa.steammultiruntime.lobby.steam` | Steam Lobby、Steam UI、接続品質表示 |
| `com.koiusa.steammultiruntime.resourceloader` | 共通`LoadingSplashPresenter`とCharacter準備判定 |

```text
Steam Lobby
├─ SteamLobbyService
│  ├─ SteamConnection
│  ├─ SteamLobbyManager
│  ├─ INetworkSessionController
│  │  └─ LobbyNetworkSessionController
│  ├─ SteamLobbyQualityTracker
│  └─ SteamLobbyConnectionStatus
├─ SteamLobbyUiDocument
│  └─ LobbyView
└─ SteamLobbyMenuToggle

Scene Flow
├─ IStageSceneCatalog
│  ├─ LocalSceneFlowLoader
│  └─ ISteamLobbySceneLoader
│     ├─ SteamLobbySceneLoader
│     └─ SteamLobbyDedicatedServer
├─ StageSceneList : ScriptableObject
├─ LocalStartupSceneLoader
├─ LocalStageSelectUIDocument
│  └─ StageSelectUI
└─ StageSelectMenuToggle

Loading
├─ ILoadingSplashEventSource
├─ LocalLoadingSplash
└─ SteamLobbyLoadingSplash
```

## Lobbyの責務

| クラス | 主な責務 |
|---|---|
| `SteamLobbyService` | Lobby操作の外部窓口と各内部サービスの統合 |
| `SteamConnection` | Steam初期化・接続状態の基盤 |
| `SteamLobbyManager` | Lobbyの作成、検索、参加、退出、キャッシュ管理 |
| `INetworkSessionController` | LobbyからNetwork実装を分離するセッション操作契約 |
| `LobbyNetworkSessionController` | NGOのHost／Server／Client開始・停止、Facepunch Transport接続先、Network Scene同期を管理する |
| `SteamLobbyQualityTracker` | Member間の品質情報を収集・配信する |
| `SteamLobbyConnectionStatus` | Memberごとの接続品質を公開する |
| `SteamLobbyUiDocument` / `LobbyView` | Lobby一覧と操作UIを表示する |

接続品質は`NetworkTransport.GetCurrentRtt()`から取得します。Facepunch Transportは`Connection.QuickStatus().Ping`を標準Transport APIへ公開し、Lobby側はFacepunch内部型やReflectionへ依存しません。Steam IDだけからPingを推定する処理は持ちません。この型付きアダプター方針は[Package Architecture](PackageArchitecture.md#ドメイン間の接続方法とリフレクション方針)に従います。

`INetworkSessionController`はバックエンド非依存の`lobby`パッケージが所有し、`lobby.netcode`の`LobbyNetworkSessionController`が実装します。`SteamLobbyService`はこの実装を`SteamLobbyManager`と`SteamLobbyQualityTracker`へ渡し、Steam Lobbyの操作とNGOのセッション制御を合成します。

Lobby一覧の再検索・再描画では、選択中のLobby IDが引き続き一覧に存在して参加可能な場合、その行のフォーカスとハイライトを復元します。

## Scene遷移

```text
Lobby UI / Stage Select UI / Startup Loader
  ├─ Local Stage Select → IStageSceneCatalog → LocalSceneFlowLoader
  └─ Steam Lobby → ISteamLobbySceneLoader → SteamLobbySceneLoader / DedicatedServer
  → StageSceneListからSceneを解決
  → Scene Load
```

- Network Lobbyでは`SteamLobbySceneLoader`がLobbyとStage間の遷移を管理する
- `IStageSceneCatalog`は選択可能なStage一覧だけを公開し、Local UIとSteam Lobby Loaderで共有する
- `ISteamLobbySceneLoader`は`IStageSceneCatalog`を継承し、Lobby入退室のScene Lifecycleを追加する
- Local実行の`LocalSceneFlowLoader`は`IStageSceneCatalog`だけを実装し、未対応のLobby操作を公開しない
- LocalManager PrefabのStage Selectは`LocalSceneFlowLoader`をCatalogとして参照し、SteamConnection PrefabのLobby UI／Serviceは`SteamLobbySceneLoader`を参照する
- Local Stage Selectは`UiNavigationInputSession`を使い、UI Navigate上下／左右でStage候補を循環し、Submitで選択中のStageを読み込む
- Dedicated ServerはUIを経由せず起動対象Stageを決定する
- Scene参照の一覧は`StageSceneList`へ集約する
- Stage Scene Cameraの無効化では`IPreservedLoadedSceneCamera`を持つUI基盤Cameraを除外する。通常Camera／AudioListenerの停止と保護Camera維持は動的一時SceneのPlayModeテストで確認する
- Sceneの非同期待機は所有ObjectのライフタイムCancellationTokenを受け取り、所有Objectの破棄後に後続処理を継続しない。Local Stage切替はActive Scene変更時にStage Select UIが自動で閉じても、新Stageの有効化と旧StageのUnloadを1つの切替処理として最後まで完了する
- `LoadingStarted`と`LoadingFinished`は`try/finally`で対にし、起動用Unity Messageの`async void`入口ではキャンセル以外の例外を記録する
- 事前キャンセル時の即時伝播と、キャンセル／Scene未設定スキップ時のLoading通知対称性はPlayModeテストで保護する

パッケージ使用側ではSample SceneをImportしただけではBuild Settingsへ追加されません。
Build Profile固有のScene一覧と、パッケージ更新時の旧Sample削除については
[サンプルの導入とBuild Profile設定](../Assets/SteamMultiRuntime/Documentation~/Samples.md)を参照してください。

## Loading Splash

`LocalLoadingSplash`は`ILoadingSplashEventSource`を購読し、共通の`LoadingSplashPresenter`を利用します。`SteamLobbyLoadingSplash`はNetwork Scene遷移とPlayer Model準備を監視して表示を制御します。

Local Playerの準備判定は`core`の`ILocalPlayerProvider`と`LocalPlayerProviderRegistry`を経由します。これにより`lobby`と`resourceloader`は`integration`の`LocalManager`を直接参照しません。

```text
Scene Loader / Stage Select
  → Loading開始・完了Event
  → LocalLoadingSplash / SteamLobbyLoadingSplash
  → LoadingSplashPresenterまたはLobby用表示
```

## 境界

1. UIはLobbyやScene状態を直接所有しない
2. Lobby操作は`SteamLobbyService`を外部窓口とする
3. Stage一覧は`IStageSceneCatalog`、Steam Lobby遷移要求は`ISteamLobbySceneLoader`へ分離する
4. Local、Network、Dedicated ServerでLoader実装を分ける
5. SceneとModelの準備が完了するまでLoading表示を維持する
6. 調査用Editor WindowからRuntime状態を変更しない
7. 接続品質はTransportの公開APIを使い、Steamworks APIを名前で探索しない
