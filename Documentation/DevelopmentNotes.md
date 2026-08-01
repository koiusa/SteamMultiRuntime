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

Server Network NPC 300体の診断では、移動中40体から最新計測で250体へ改善した。残る停止数には目的地到着後の意図的な待機を含む。Crowd OFFの導入前比デグレは解消済み。

Main Thread CPU時間は次の通り。現在値は最小化EditorでON／OFFを同条件計測した結果。旧基準値とは実行条件が異なるため単純比較はしない。

| NPC | Crowd ON | Crowd OFF | ON FPS | ON移動中 |
|---:|---:|---:|---:|---:|
| 100 | 27.685 ms | 31.971 ms | 36.0 | 74 / 100 |
| 200 | 56.273 ms | 56.770 ms | 17.7 | 144 / 200 |
| 300 | 85.744 ms | 88.930 ms | 11.6 | 250 / 300 |

経路制御変更前の旧基準値は、100体でON 11.009 ms／OFF 12.325 ms、300体でON 43.284 ms／OFF 59.737 ms。現在もCrowd ONはOFFより速いが、NavMesh経路予算増加により差が約5%まで縮小している。

計測は`NpcPerformanceBenchmark`を使用し、100／200／300体を比較する。Networkでは`-npcBenchmarkNetwork 1`を指定する。

### 運用上の注意

- `ServerScene`のNavMeshはScene全体を収集してベイクする。壁や梯子の追加後は再ベイクする。
- 移動床は`NavMeshModifier.Ignore From Build`で静的NavMeshから除外する。Collider、接地、床上歩行、移動追従は維持し、自律的な乗降経路が必要な場合は動的Linkを別途用意する。
- NPC同士のsolid Colliderは無効化するが、Player、床、移動床、Network Physics Objectとの接触は維持する。
- Wire／Wall／Ladder Actionは疑似入力または継続中Actionがある間だけ起動する。
- `NpcCrowdTraversalTestDriver`は既定OFFとし、特殊移動を手動検証する時だけ有効化する。

### 次の課題

Crowd化によって移動Simulationは改善したが、1000体規模ではAnimator評価、ボーンTransform更新、`SkinnedMeshRenderer`のスキニング、Renderer／Material描画がボトルネックになる。

次はUnity標準描画を基準に、Blend Shape、複数Skinned Mesh、モデル別bind pose／軸変換、Humanoid Retargeting、頭・髪・装飾・Spring Boneを検証し、GPU Skinning、Animation Texture、Compute Skinning、Entities Graphicsの採用方式を判断する。
