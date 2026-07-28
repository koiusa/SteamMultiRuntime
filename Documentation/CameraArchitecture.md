# Camera Architecture

この文書は、SteamMultiRuntimeのCamera切替、入力割り当て、Focus Marker、障害物回避の現在実装をまとめた設計資料です。

## クラス構成

```text
CameraMixerWeightControllerBase
├─ LocalCameraMixerWeightController
│  └─ LocalFocusMarkerContext
└─ LobbyCameraMixerWeightController
   └─ NetworkFocusMarkerContext

IFocusMarkerContext
├─ LocalFocusMarkerContext
└─ NetworkFocusMarkerContext

Focus Marker
├─ ForcusMerker
├─ CameraTrackMarker
└─ FocusMarkerUtility
```

`CameraMixerWeightControllerBase`が共通処理を持ち、派生クラスは利用する`IFocusMarkerContext`の解決だけを担当します。Contextは`IsActive`に加えて、Cameraが追従するローカル`PlayerObject`を公開します。Camera入力の制御では、このPlayerObjectから`IPlayerTraversalCoordinator`を解決し、対象プレイヤー自身のWire接続状態を参照します。

## Camera切替

`CinemachineMixingCamera`内の2台を重みで切り替えます。

- `defaultCameraIndex`: Focusが無効なときに重み1
- `followCameraIndex`: Focusが有効なときに重み1
- `transitionSpeed`: 指数補間による切替速度

Contextの`StateChanged`を購読し、`IsActive`が変わると目標Weightを更新します。Enable直後は現在状態を即時反映し、その後の切替は毎Frame補間します。

## Camera入力

`InputActionsConfig`から次のActionを名前で解決し、`CinemachineInputAxisController`へRuntime生成した`InputActionReference`を割り当てます。

```text
Look Orbit X / Look Orbit Y ← Player/Look
Orbit Scale                 ← Player/CameraZoom
Grapple判定                  ← Player/Grapple
```

Camera入力の有効状態は次の条件で切り替えます。

| 状態 | Camera入力 | 目的 |
|---|---|---|
| Grapple未入力 | 有効 | 通常の視点操作 |
| Grapple入力中、Wire未接続 | 無効 | Wire照準と視点操作の競合を防止 |
| Grapple入力中、Wire接続済み | 有効 | Wire接続を維持したまま視点操作 |

Wire接続判定には追従対象PlayerObjectの`IPlayerTraversalCoordinator.IsWireAttached`を使用します。このため、Lobby内の別プレイヤーがWire接続してもローカルCamera入力には影響しません。生成したAction ReferenceはDestroy時に破棄します。

## 障害物回避

`Enable Camera Collision`が有効な場合、Awake時に配下の全`CinemachineCamera`を走査し、`CinemachineDeoccluder`と`CinemachineDecollider`がなければ追加して共通設定を適用します。Prefab側にComponentがある場合もInspector値で再設定されます。

`Enable Camera Collision`はPlay Mode中にも切り替えられます。無効にすると、今回自動追加したComponentを停止し、元から存在したComponentは有効状態と設定を適用前の値へ復元します。Camera Mixer、Focus切替、視点入力には影響しません。再度有効にすると自動設定を適用し直します。

`CinemachineDeoccluder`はTargetとCameraの間の遮蔽物に対してCameraを前方へ寄せます。短時間の遮蔽を無視する`minimumOcclusionTime`と、復帰時の揺れを抑える`collisionSmoothingTime`／`collisionRecoveryDamping`を持ちます。

`CinemachineDecollider`はCamera自身がCollider内部へ入った場合の押し出しを担当します。遮蔽時の追従には`collisionDamping`、障害物がなくなった後の復帰には`collisionRecoveryDamping`を使うため、壁の内部からは比較的素早く抜けつつ、通常復帰は緩やかになります。

現在の既定値は次のとおりです。

```text
Enable Camera Collision:       Off
Camera Collision Radius:       0.45 m
Minimum Distance From Target:  0.50 m
Minimum Occlusion Time:        0.08 sec
Collision Smoothing Time:      0.25 sec
Collision Damping:             0.40 sec
Collision Recovery Damping:    0.70 sec
```

衝突対象は`cameraCollisionLayers`で制御し、Triggerの扱いなど実際の判定はCinemachine Componentに委ねます。Terrain専用Decollisionは無効です。

## Compass HUD

`PlayerCompassHud`は画面上部に横スクロール式Compassを表示します。Camera Forwardを水平面へ投影した角度を使用し、Worldの`+Z`をNorth、`+X`をEastとして、8方角、15度刻みの目盛り、現在角度を表示します。

CompassはStartup時に自動生成しません。Human Player Prefab上の`PlayerCompassHudOwner`が`ILocalPlayerOwnership`を参照し、Local Playerであることを確認して専用GameObjectを生成します。Playerの無効化／破棄時には削除します。NetworkではOwnerだけが表示し、Remote PlayerとNPCは生成しません。

## 変更時の確認項目

1. Camera Collision LayerにPlayer自身を含めていないか
2. `Enable Camera Collision`が無効な場合にCamera Componentへ干渉しないか
3. Mixing Camera配下の全Virtual Cameraへ同じ設定を適用できるか
4. Wire照準中にCamera入力が停止し、Wire接続後またはGrapple終了後に復帰するか
5. 遮蔽物の出入りでCameraが振動しないか
6. CameraがCollider内部へ入った場合に速やかに押し出されるか
7. LocalとLobbyの両ContextでWeight切替が動作するか
