# Development Notes

## Steam Inputとテスト用App ID

Steamネットワークテストでテスト用App ID（480 / Spacewar）を使うと、Steam Inputの設定によってゲームパッド入力がUnity Input Systemへ届かなくなる場合があります。

今回の環境では正規のゲームApp IDに切り替えると解消しました。ネットワーク開始後にゲームパッドだけ反応しなくなる場合は、InputGuideOverlayや通信処理より先に、使用中のApp IDとSteam Input設定を確認してください。

## 大規模NPC Crowd化

NPC 1000体表示を目標とし、Crowd Backendを標準経路とする。現状の実用目安は200体で、Crowd OFFは互換性確認とデバッグ用であり、大規模運用向けではない。

- Local NPCとServer Network NPCは、移動、重力、接地、壁接触、移動床を`NpcCrowdSimulation`と`NpcCrowdMotor`で一括処理する。
- Boid／RVO近傍計算はSpatial GridとBurst Jobを使用する。
- Network ClientはCrowd Simulationを実行せず、Serverの同期結果を表示する。
- AI判断、FutureAction、Coordinator、疑似入力はCrowd ON／OFFで共通とする。
- Playerは従来Motorを使用し、NPC Crowd Motorとは分離する。

### 大量スポーン時の経路制御

大量NPCではUnityのNavMesh経路計算キューが飽和し、経路計算待ちのNPCが停止していた。Crowdでは`NavMeshAgent`を経路探索専用とし、個別Obstacle Avoidance／Stuck再経路／`autoRepath`を使用しない。経路計算予算は登録NPC数に応じて調整し、再経路計算中も直前の進行方向を維持する。

### Network Crowd計測（2026-08-02）

個体別の空振りCallbackを中央Schedulerまたはpush通知へ移した後のNetwork NPC、Crowd ON計測。`NetworkRigidbody`は標準構成を維持し、`NetworkTransform`のPosition Thresholdは0.5m、Y Rotation Thresholdは6度とした。

実行条件はUnity 6000.3.9f1、`ServerScene`、Network Server、Crowd ON、100／200／300体、Warmup 180 frame、Sample 300 frame、乱数Seed 481516、Subsystem Recorder ON、`-batchmode -nographics`である。ヘッドレス実行のためGPU Frame、Render Thread、Draw Callは評価対象外とする。

| NPC | Frame平均 | P95 | FPS | Main Thread | Fixed steps/frame | 移動中 |
|---:|---:|---:|---:|---:|---:|---:|
| 100 | 10.305 ms | 19.305 ms | 97.0 | 10.274 ms | 0.51 | 71 / 100 |
| 200 | 25.023 ms | 46.525 ms | 40.0 | 24.987 ms | 1.25 | 162 / 200 |
| 300 | 45.453 ms | 73.620 ms | 22.0 | 45.411 ms | 2.27 | 237 / 300 |

直前の300体Network Crowd ON計測53.6msに対して45.453msとなり、約15.2%改善した。ただし固定条件のA/B計測ではないため参考値とする。

300体の主要Markerは次の通り。

| Marker | 平均時間 |
|---|---:|
| `LateBehaviourUpdate` | 18.693 ms |
| `BehaviourUpdate` | 9.979 ms |
| `Physics.NpcCrowd.PrepareProbes` | 3.717 ms |
| `MeshSkinning.Skin` | 3.070 ms |
| `FixedUpdate.PhysicsFixedUpdate` | 2.420 ms |
| `Physics.SyncColliderTransformBatchJob` | 2.192 ms |
| `BatchQuery.ExecuteCapsulecastJob` | 1.737 ms |
| `UpdateRendererBoundingVolumes` | 1.422 ms |

`BehaviourUpdate`は以前の約11.9msから9.979msへ低下したが、標準`NetworkTransform`による全NPC走査は残る。最大項目は`LateBehaviourUpdate`である。300体ではFixed stepも平均2.27回まで増え、catch-upによる非線形な悪化が始まっている。

### 運用上の注意

- `ServerScene`のNavMeshはScene全体を収集してベイクする。壁や梯子の追加後は再ベイクする。
- 移動床は`NavMeshModifier.Ignore From Build`で静的NavMeshから除外する。Collider、接地、床上歩行、移動追従は維持し、自律的な乗降経路が必要な場合は動的Linkを別途用意する。
- NPC同士のsolid Colliderは無効化するが、Player、床、移動床、Network Physics Objectとの接触は維持する。
- Wire／Wall／Ladder Actionは疑似入力または継続中Actionがある間だけ起動する。
- `NpcCrowdTraversalTestDriver`は既定OFFとし、特殊移動を手動検証する時だけ有効化する。

### 次の課題

次は`LateBehaviourUpdate`の発生元分離と、NPCごとの`NetworkTransform`を置き換える一括同期を優先する。Animator、Spring Bone、Renderer、スキニングの方式変更は難易度が高いため、その後の課題とする。
