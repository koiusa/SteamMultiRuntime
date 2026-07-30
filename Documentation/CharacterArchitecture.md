# Character Architecture

この文書をユーザープロファイル、Character Modelの解決・反映、選択UI、表示名に関する詳細仕様の正本とします。全体配置は[CurrentClassStructure.md](CurrentClassStructure.md)を参照してください。

## クラス構成

### 所有パッケージ

| パッケージ | 所有するもの |
|---|---|
| `com.koiusa.steammultiruntime.character` | モデルID、ProfileとModel Syncの契約 |
| `com.koiusa.steammultiruntime.resourceloader` | Prefab解決・生成、Local Model Sync、Loading Splash Presenter |
| `com.koiusa.steammultiruntime.character.ui` | Character選択UI |
| `com.koiusa.steammultiruntime.player` | Local表示名と表示名契約 |
| `com.koiusa.steammultiruntime.player.netcode` | Network Profile、Network Model Sync、Network表示名 |
| `com.koiusa.steammultiruntime.player.ui` | Player Name Overlay |
| `com.koiusa.steammultiruntime.integration` | LocalManagerとLocal Profileの合成 |

```text
IRuntimeUserProfileModelSource
└─ PlayerModelProfileBase
   ├─ RuntimeUserProfile（Network）
   └─ LocalRuntimeUserProfile（Local）

IPlayerModelSync
├─ LocalPlayerModelSync
└─ NetworkPlayerModelSync : NetworkBehaviour

ICharacterPrefabLoader
└─ CharacterPrefabLoader
   └─ CharacterModelIdList : ScriptableObject

ILocalPlayerProvider
└─ LocalManager
   └─ LocalPlayerProviderRegistry

Character UI
├─ CharacterSelectUiDocument
│  └─ CharacterSelectView
└─ CharacterSelectMenuToggle

Display Name
├─ LocalPlayerDisplayName : IPlayerDisplayNameSource + IPlayerDisplayNameNotifier
├─ NetworkPlayerDisplayName : IPlayerDisplayNameSource + IPlayerDisplayNameNotifier
└─ PlayerNameOverlayUiDocument（World Space）
```

## モデル反映経路

```text
RuntimeUserProfile / LocalRuntimeUserProfile
  → model ID
  → LocalPlayerModelSync / NetworkPlayerModelSync
  → CharacterPrefabLoader
  → CharacterModelIdListからPrefabを解決
  → Player Model Instance
```

- `PlayerModelProfileBase`がモデル選択の共通契約を提供する
- Localは`LocalPlayerModelSync`が直接反映する
- Networkは`NetworkPlayerModelSync`が選択Indexを同期して各表示へ反映する
- Prefab解決処理は`ICharacterPrefabLoader`の背後へ置く

## Character選択UI

```text
CharacterSelectMenuToggle
  → CharacterSelectUiDocument
  → CharacterSelectView
  → IRuntimeUserProfileModelSource
  → Model Sync経路
```

UIは選択値をProfileへ渡し、モデルを直接生成しません。モデル生成と差し替えはModel Sync／Loader側の責務です。

Character Select表示中は`input.core`の`UiNavigationInputSession`を使い、本番Input Actionsの`UI/Navigate`／`Submit`／`Cancel`を共通ポリシーで処理します。画面側は候補移動、決定、キャンセルの結果だけを受け取り、Actionの有効化やリピート時間を所有しません。EventSystemが生成する同じ移動Eventは消費し、1入力で1候補だけ移動します。

## Player Name Overlay

```text
LocalPlayerDisplayName / NetworkPlayerDisplayName
  → IPlayerDisplayNameSource / IPlayerDisplayNameNotifier
  → PlayerNameOverlayUiDocument（Presentation配下）
  → World Space Label表示
```

Network表示名はNetworkBehaviour側で同期し、Overlayは表示処理だけを担当します。表示位置は`Presentation`の補間Transformから継承し、Player一覧の走査やWorld座標からScreen座標への毎Frame変換は行いません。名前変更は`DisplayNameChanged`で反映し、UIDocumentのDynamicサイズで文字列と余白に追従します。カメラ正対、画面上の見かけサイズを一定にするView Depth／投影行列補正、距離FadeだけをRender PipelineのCamera描画開始通知で更新します。PerspectiveのFOV変更とOrthographic Sizeにも追従します。

Client起動時は`SteamMultiRuntime_UI` Sceneを加算ロードし、`WorldSpaceUiOverlaySceneRoot`がWorld Space UIの後段Camera群を所有します。World Space UIは実行時に未使用User Layerへ登録し、`WorldSpaceUiOverlayCamera`が各Game Cameraと同じ投影設定でDepthだけをクリアしたCameraを生成します。元CameraからOverlay Layerを除外し、後段CameraはOverlay Layerだけを描画するため、UI ToolkitのWorld Space描画を維持したままシーンObjectによる遮蔽を受けません。

利用側ProjectでUI SceneがBuild Settingsへ未登録の場合は、同じScene Rootを`DontDestroyOnLoad`で生成してフォールバックします。利用側のTagManager設定は要求しません。URPでは`WorldSpaceUiOverlayUrpCameraAdapter`が後段CameraをCamera StackのOverlayとして接続し、Colorを維持してDepthだけをクリアします。Render Pipeline固有設定はAdapter assemblyへ分離し、HDRPも同じ契約へ接続します。Graphics DeviceがNullのDedicated ServerではUI Sceneをロードしません。Compassは別のScreen Space PanelSettingsを使用します。

Presentation Scene LoaderがStage内Cameraを無効化するとき、Overlay Cameraは`IPreservedLoadedSceneCamera`によって対象外になります。Stage Cameraを無効化する既存ポリシーは維持し、UI基盤Cameraだけを保護します。

EditorのFast Enter Play ModeでDomain ReloadとScene Reloadの両方が無効でも、Subsystem初期化後に有効な`PlayerNameOverlayUiDocument`を再登録します。Scene Componentの`OnEnable`が再実行されない設定でも、Camera Stackと表示名Event購読をPlay開始ごとに復元します。

## 境界

1. Profileは選択値を保持し、Prefabを直接生成しない
2. UIはProfileを更新し、Model Syncを再実装しない
3. Model SyncはPrefab解決をLoaderへ委譲する
4. LocalとNetworkでProfile／Sync実装を分け、Loader契約は共有する
5. 表示名Overlayはゲーム状態を所有しない
6. Local Playerの探索は`ILocalPlayerProvider`を使用し、`LocalManager`の型名やプロパティ名をReflectionで探索しない。全体方針は[Package Architecture](PackageArchitecture.md#ドメイン間の接続方法とリフレクション方針)に従う
