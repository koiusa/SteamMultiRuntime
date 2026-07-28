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
│  ├─ SteamLobbyNetworkFacade
│  ├─ SteamLobbyQualityTracker
│  └─ SteamLobbyConnectionStatus
├─ SteamLobbyUiDocument
│  └─ LobbyView
└─ SteamLobbyMenuToggle

Scene Flow
├─ ISteamLobbySceneLoader
│  ├─ SteamLobbySceneLoader
│  ├─ SteamLobbyDedicatedServer
│  └─ LocalSceneFlowLoader
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
| `SteamLobbyNetworkFacade` | NetworkManagerとTransportの開始・停止を仲介する |
| `SteamLobbyQualityTracker` | Member間の品質情報を収集・配信する |
| `SteamLobbyConnectionStatus` | Memberごとの接続品質を公開する |
| `SteamLobbyUiDocument` / `LobbyView` | Lobby一覧と操作UIを表示する |

接続品質は`NetworkTransport.GetCurrentRtt()`から取得します。Facepunch Transportは`Connection.QuickStatus().Ping`を標準Transport APIへ公開し、Lobby側はFacepunch内部型やReflectionへ依存しません。Steam IDだけからPingを推定する処理は持ちません。

## Scene遷移

```text
Lobby UI / Stage Select UI / Startup Loader
  → ISteamLobbySceneLoader
  → SteamLobbySceneLoader / LocalSceneFlowLoader / DedicatedServer
  → StageSceneListからSceneを解決
  → Scene Load
```

- Network Lobbyでは`SteamLobbySceneLoader`がLobbyとStage間の遷移を管理する
- Local実行では`LocalSceneFlowLoader`が同じScene Loader契約を実装する
- Dedicated ServerはUIを経由せず起動対象Stageを決定する
- Scene参照の一覧は`StageSceneList`へ集約する

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
3. Scene遷移要求は`ISteamLobbySceneLoader`へ集約する
4. Local、Network、Dedicated ServerでLoader実装を分ける
5. SceneとModelの準備が完了するまでLoading表示を維持する
6. 調査用Editor WindowからRuntime状態を変更しない
7. 接続品質はTransportの公開APIを使い、Steamworks APIを名前で探索しない
