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
| `com.koiusa.input.core` | Input System設定、入力Actionの共有とライフタイム管理、UIナビゲーションの共通リピート制御 |
| `com.koiusa.ui.common` | 特定ゲームやLocalizationに依存しないUIテーマ、`IUiMenu`、スタック式`UiMenuNavigator` |
| `com.koiusa.keyconfig` | Input Systemのリバインドと入力表示 |
| `com.koiusa.targetingsystem` | ターゲット検出、ロックオン、Camera連携 |

### Steam Multi Runtime共通基盤

| パッケージ | 責務 | 主な型 |
|---|---|---|
| `com.koiusa.steammultiruntime.core` | 内部パッケージ間で共有する最小契約と属性 | `ILocalPlayerProvider`, `LocalPlayerProviderRegistry`, `FallRecovery` |
| `com.koiusa.steammultiruntime.localization` | 日本語・英語カタログ、Unity Localization連携、導入ツール | `GameLocalization`, `UiLocalizationCatalog` |
| `com.koiusa.steammultiruntime.keyconfig` | 汎用KeyconfigをSteamMultiRuntimeのLocalizationへ接続 | `SteamMultiRuntimeKeyConfigLocalizer` |
| `com.koiusa.steammultiruntime.targetingsystem` | 汎用TargetingSystemを共有Input Actions設定へ接続 | `SteamMultiRuntimeTargetingInputActions` |
| `com.koiusa.steammultiruntime.editor-tools` | Steam Multi Runtime共通のEditor支援 | Animation Event可視化など |

CoreとLocalizationは異なる関心事です。CoreからLocalizationを参照せず、Localizationから機能ドメインも参照しません。
KeyconfigとTargetingSystemの汎用実装はプロジェクト汎用パッケージに残し、SteamMultiRuntime固有の接続だけを同名の`com.koiusa.steammultiruntime.*`パッケージへ配置します。
Keyconfigの導入とテストは[Keyconfig.md](Keyconfig.md)、TargetingSystemは[TargetingSystem.md](TargetingSystem.md)を参照してください。

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

steammultiruntime.keyconfig ───────> keyconfig + localization
steammultiruntime.targetingsystem ─> targetingsystem

feature packages ───────────> input.core / core / localization as required
```

## ドメイン間の接続方法とリフレクション方針

直接参照が不自然になる場合は、型名文字列やリフレクションで回避せず、所有者が明確な小さな契約を下位パッケージへ置きます。

接続方法は次の順で検討します。

1. 直接の型付き呼び出しまたはジェネリック
2. 下位パッケージが所有する小さなインターフェースまたはイベント
3. `integration`など上位パッケージに置く明示的なアダプター
4. Inspectorで割り当てるシリアライズ済みUnity参照

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

### 原則

自作のRuntimeコードへReflection、`dynamic`、`SendMessage`、メンバー名探索、型名文字列による解決を追加しません。特に、次の問題を回避する目的では使用しません。

- asmdefの依存方向や循環参照
- オプショナルパッケージの分離
- Local／Network所有権の判定
- APIの可視性やコンパイルエラー

これらは契約の所有先、アダプター、パッケージ境界を修正して解決します。

### 例外

例外は、Unityの動的機構または非公開APIを調査するEditor専用ツールと、隔離されたThirdpartyコードに限定します。例外を追加・変更する場合は、公開された型付きAPIでは実現できない理由をコードまたは対応文書に残し、対象メンバーが存在しない場合も安全に失敗させ、Gameplayの実行経路へ持ち込みません。

`AnimationEventReceiverVisualizerWindow`のReceiver列挙は、Animation Event自体がメソッド名でReceiverを解決するため既知の例外です。この例外をRuntimeの動的ディスパッチへ一般化しません。

`Runtime/Packages/Thirdparty`はvendorコードとして扱い、既存のReflectionを無関係な変更で書き換えません。連携方法を変える場合は、自作側に型付きアダプターを設けます。

レビューでは変更した自作C#を対象に、`System.Reflection`、`BindingFlags`、`GetMethod`、`GetField`、`GetProperty`、`Type.GetType`、`Activator`、`MethodInfo`、`FieldInfo`、`PropertyInfo`、`dynamic`、`SendMessage`を確認します。delegateの`Invoke`や通常の`object.GetType()`はReflection利用と区別します。

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

## Asset配置ルール

- RuntimeのPrefabや設定から参照されるAssetは`Runtime`を正本とし、`Samples`へ置かない。
- `Resources`には`Resources.Load`で動的解決するAssetだけを置く。GUIDで直接参照するPrefabやAnimationは`Runtime/Prefabs`、`Runtime/Animations`、`Runtime/Configs`へ置く。
- Stage固有のTerrain、NavMesh、演出Assetは対象の`Samples/Gameplay`配下へ置く。
- Sample SceneからRuntime Assetを参照してよいが、Runtime AssetからSample Assetを参照しない。
- 汎用パッケージの単体Sample用Input Actionsは、そのパッケージの`Samples/Basic`で所有する。

## 関連文書

- [UI Architecture](UiArchitecture.md)
- [CurrentClassStructure.md](CurrentClassStructure.md)
- [CharacterArchitecture.md](CharacterArchitecture.md)
- [SessionArchitecture.md](SessionArchitecture.md)
- [PlayerGameplayArchitecture.md](PlayerGameplayArchitecture.md)
- [ローカライズ導入手順](../Assets/SteamMultiRuntime/Documentation~/Localization.md)
