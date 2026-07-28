# Package Architecture

Steam Multi Runtimeは、汎用基盤、ドメイン、技術アダプター、合成、UIを分離します。依存方向は原則として上位の具体実装から下位の契約・基盤へ向け、循環参照を作りません。

## レイヤー

```text
Samples / Prototype / Integration
  ├─ UI packages
  ├─ Steam / Netcode adapters
  └─ Domain implementations
       ├─ SteamMultiRuntime Core / Localization
       ├─ Input Core / System Core / UI Common
       └─ Unity packages
```

- 汎用基盤はSteam、Lobby、Netcode、ゲーム固有型を参照しません。
- ドメインパッケージは自分の状態と契約を所有します。
- NetcodeやSteamworksへの依存は接尾辞付きパッケージへ隔離します。
- UIはドメインの公開契約を利用し、ネットワーク実装へ直接依存しません。
- 複数ドメインの組み立ては`integration`または`prototype`が担当します。

## パッケージ一覧

### プロジェクト汎用

| パッケージ | 責務 |
|---|---|
| `com.koiusa.system.core` | 終了処理などUnityプロジェクト全般で使えるシステム機能 |
| `com.koiusa.input.core` | Input System設定、入力Actionの共有とライフタイム管理 |
| `com.koiusa.ui.common` | 特定ゲームやLocalizationに依存しないUI共通機能 |
| `com.koiusa.keyconfig` | Input Systemのリバインドと入力表示 |
| `com.koiusa.targetingsystem` | ターゲット検出、ロックオン、Camera連携 |

### Steam Multi Runtime共通基盤

| パッケージ | 責務 | 主な型 |
|---|---|---|
| `com.koiusa.steammultiruntime.core` | 内部パッケージ間で共有する最小契約と属性 | `ILocalPlayerProvider`, `LocalPlayerProviderRegistry`, `FallRecovery` |
| `com.koiusa.steammultiruntime.localization` | 日本語・英語カタログ、Unity Localization連携、導入ツール | `GameLocalization`, `UiLocalizationCatalog` |
| `com.koiusa.steammultiruntime.editor-tools` | Steam Multi Runtime共通のEditor支援 | Animation Event可視化など |

CoreとLocalizationは異なる関心事です。CoreからLocalizationを参照せず、Localizationから機能ドメインも参照しません。

### Characterとリソース

| パッケージ | 責務 | 主な型 |
|---|---|---|
| `com.koiusa.steammultiruntime.character` | モデルID、プロフィール、モデル同期の契約 | `CharacterModelIdList`, `IRuntimeUserProfileModelSource`, `IPlayerModelSync` |
| `com.koiusa.steammultiruntime.resourceloader` | Character Prefabの解決・生成、Loading Splash表示 | `ICharacterPrefabLoader`, `CharacterPrefabLoader`, `LoadingSplashPresenter` |
| `com.koiusa.steammultiruntime.character.ui` | Character選択UI | `CharacterSelectUiDocument`, `CharacterSelectView` |

依存方向は`character.ui -> character`、`resourceloader -> character`です。`character`はUIやロード方式を知りません。

### Player、移動、表示

| パッケージ | 責務 |
|---|---|
| `com.koiusa.steammultiruntime.locomoter` | Motor、Traversal、移動状態の非Network実装 |
| `com.koiusa.steammultiruntime.locomoter.netcode` | 移動状態とTraversalのNGO同期 |
| `com.koiusa.steammultiruntime.player` | Player入力、Skill、Health、表示名などPlayerドメイン |
| `com.koiusa.steammultiruntime.player.netcode` | Player状態、モデル、表示名のNGO同期 |
| `com.koiusa.steammultiruntime.player.ui` | Player名などPlayer固有UI |
| `com.koiusa.steammultiruntime.animationdriver` | 確定済み移動状態からAnimatorへの変換 |
| `com.koiusa.steammultiruntime.audio` | Footstep検出と音声再生。受信契約は`IFootstepReceiver` |

`player.ui`は`player`だけを参照し、`player.netcode`を参照しません。Audioはメソッド名探索や`SendMessage`を使わず、`IFootstepReceiver`で接続します。

### Lobbyとセッション

| パッケージ | 責務 |
|---|---|
| `com.koiusa.steammultiruntime.lobby` | バックエンド非依存のシーンフロー、Stage選択、Loading Splash |
| `com.koiusa.steammultiruntime.lobby.netcode` | NGOセッションとNetwork Scene連携 |
| `com.koiusa.steammultiruntime.lobby.steam` | Steamworks Lobby、Steam UI、Facepunch Transport連携 |

依存方向は`lobby.steam -> lobby.netcode -> lobby`です。Steam固有型を`lobby`へ持ち込みません。

### 合成と試作

| パッケージ | 責務 |
|---|---|
| `com.koiusa.steammultiruntime.integration` | Character、ResourceLoader、Locomoterなど完成ドメインの合成 |
| `com.koiusa.steammultiruntime.prototype` | NPCなど試作段階の機能。安定後に専用ドメインへ昇格する候補 |

## 主要な依存関係

```text
character.ui ───────────────> character
resourceloader ─────────────> character
integration ────────────────> core + character + resourceloader + locomoter

player.ui ──────────────────> player
player.netcode ─────────────> player + locomoter.netcode
animationdriver ────────────> locomoter
locomoter.netcode ──────────> locomoter + core

lobby.netcode ──────────────> lobby
lobby.steam ────────────────> lobby + lobby.netcode + player
lobby ──────────────────────> core + resourceloader + localization

feature packages ───────────> input.core / core / localization as required
```

## ドメイン間の接続方法

直接参照が不自然になる場合は、型名文字列やリフレクションで回避せず、所有者が明確な小さな契約を下位パッケージへ置きます。

例:

```text
LocalManager : ILocalPlayerProvider
  -> LocalPlayerProviderRegistry (core)
  -> LocalLoadingSplash (lobby)
  -> LoadingSplashPresenter (resourceloader)

FootstepCollider
  -> IFootstepReceiver
  -> FootstepColliderSpawner
```

実行時コードで`Type.GetType`、`GetMethod`、`SendMessage`をパッケージ境界の代用にしません。Unityの内部API調査など、Editor専用ツールで用途が限定される場合のみ例外とします。

## 依存ルール

1. 共通パッケージから機能パッケージを参照しない。
2. 非Networkパッケージから`.netcode`を参照しない。
3. バックエンド非依存パッケージから`.steam`を参照しない。
4. UIから具体的なNetwork実装を参照しない。
5. 複数ドメインの合成は`integration`または上位パッケージで行う。
6. Runtime asmdefからEditor asmdefを参照しない。
7. asmdefで内部パッケージを参照した場合、同じ依存を`package.json`にも宣言する。
8. ファイル移動時はUnity参照を維持するため`.meta`も同時に移動する。
9. Thirdparty変更は互換対応など必要最小限にし、上流との差分理由を残す。
10. 新しい共有型は「複数箇所で使う」だけでCoreへ移さず、Steam Multi Runtime全体が所有すべき契約かで判断する。

## 関連文書

- [CurrentClassStructure.md](CurrentClassStructure.md)
- [CharacterArchitecture.md](CharacterArchitecture.md)
- [SessionArchitecture.md](SessionArchitecture.md)
- [PlayerGameplayArchitecture.md](PlayerGameplayArchitecture.md)
- [ローカライズ導入手順](../Assets/SteamMultiRuntime/Documentation~/Localization.md)
