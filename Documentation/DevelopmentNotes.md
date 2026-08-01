# Development Notes

## Steam Inputとテスト用App ID

Steamネットワークテストでテスト用App ID（480 / Spacewar）を使うと、Steam Inputの設定によってゲームパッド入力がUnity Input Systemへ届かなくなる場合があります。

今回の環境では正規のゲームApp IDに切り替えると解消しました。ネットワーク開始後にゲームパッドだけ反応しなくなる場合は、InputGuideOverlayや通信処理より先に、使用中のApp IDとSteam Input設定を確認してください。

## 大規模NPC Crowd化

### 現在の方針

NPC 1000体表示を目標とし、Crowd Backendを標準経路として維持する。Crowd OFFは互換性確認とデバッグ用であり、大規模運用向けではない。

- Local NPCとServer Network NPCの移動、重力、接地、壁接触、移動床を`NpcCrowdSimulation`と`NpcCrowdMotor`で一括処理する。
- Boid／RVO近傍計算はSpatial GridとBurst Jobを使用する。
- Network ClientはCrowd Simulationを重複実行せず、Serverの同期結果を表示する。
- AI判断、FutureAction、Coordinator、疑似入力はCrowd ON／OFFで共通とする。
- Playerは従来Motorを使用し、NPC Crowd Motorとは分離する。

Crowd化によって移動Simulationは改善した。ただし1000体規模の最終的なボトルネックは、Animator評価、ボーンTransform更新、`SkinnedMeshRenderer`のスキニング、Renderer／Materialの描画である。Crowd Motorだけで1000体を達成できるとは扱わない。

### 確定したBenchmark結果（2026-08-01）

`NpcPerformanceBenchmark`を使用し、`ServerScene`、固定乱数seed、100／300体、180 frame Warmup、300 frame Samplingで計測する。Batch EditorではRender Thread、GPU、Draw Callsを正しく取得できないため、Main Thread、Frame time、GC、Subsystem Marker、fixed steps/frameを比較対象とする。

Network計測では`-npcBenchmarkNetwork 1`を指定する。NetworkManagerをServer起動してからNetworkNPCを明示Spawnし、`ServerDrivenActorController`を持つ個体数が要求数と一致しない場合は計測失敗とする。以前のLocalNPCへフォールバックした結果をNetwork比較には使用しない。

#### Server Network NPC

| NPC | Crowd | Main Thread | FPS | Fixed steps/frame | GC/frame |
|---:|:---:|---:|---:|---:|---:|
| 100 | ON（現行） | 11.316 ms | 88.3 | 0.57 | 198,118 B |
| 100 | OFF（現行・特殊移動要求時起動） | 13.041 ms | 76.7 | 0.65 | 219,189 B |
| 300 | ON（現行） | 46.359 ms | 21.6 | 2.32 | 1,919,064 B |
| 300 | OFF（修正前） | 176.931 ms | 5.7 | 8.84 | 8,792,191 B |
| 300 | OFF（Wire休止後） | 130.712 ms | 7.6 | 6.54 | 6,111,124 B |
| 300 | OFF（現行・従来経路復元後） | 70.180 ms | 14.3 | 3.51 | 2,773,286 B |

Crowd導入直前commit `4406a644`のServer Network NPC実測は100体12.355〜14.202 ms、300体58.507〜61.003 msだった。現行OFFは修正前176.931 msから70.180 msまで戻ったが、300体では導入前より約9〜12 ms遅いため、回帰修正はまだ完了扱いにしない。71.977 msの測定では従来Spatial Grid登録が欠落しており、Boid／RVO近傍が常に0だったため正式値には使用しない。

残差ではDynamic Rigidbodyと床接触によるFixedUpdate catch-upが支配的である。詳細計測時の導入前／現行300体は、`FixedBehaviourUpdate`相当9.476／14.090 ms、`PhysicsFixedUpdate` 10.927／15.570 ms、`LateBehaviourUpdate` 19.228／18.589 msだった。LateUpdateは同等で、残差は主にfixed steps/frame 3.05／3.69と各物理stepの差にある。

#### Historical Local NPC参考値

Crowd導入直前のcommit `4406a644`を隔離worktreeで計測したLocalNPC参考値は、100体13.830 ms（72.3 FPS）、300体85.680 ms（11.7 FPS）だった。これはServer Network NPCのBaselineではないため、Network性能の回帰判定には使用しない。

### 採用した改善

- Crowd OFFでは従来のBoid／RVO回避を使用する。Crowd Job導入時にOFF側まで省略されていた処理を復元した。
- Crowd OFFのNPCを従来Spatial Gridへ登録し、Boid／RVOの近傍0件デグレを修正した。
- Crowd OFFでは従来のNavMesh corner blendを復元し、Crowd ONだけ`desiredVelocity`直結のallocation-free経路を使用する。
- Character model生成後にNPC間のCollider pair除外を更新し、遅延生成されたColliderも登録対象にする。
- 従来Motorの接触点はSlope判定と移動床判定で共有し、同じ`Collision`を二重走査しない。既に追跡中の同一床では`IGroundMotionSource`の親階層探索も繰り返さない。
- Crowd OFFのNPC同士のsolid Collider pairを除外し、Player、床、移動床、物理Objectとの接触は維持する。
- Crowd OFFの個別`NpcNavMeshController.FixedUpdate`を`NpcConventionalPhysicsLoop`の単一callbackへ集約した。
- AI判断と疑似入力生成はUpdateで1回だけ行い、複数回走り得るFixedUpdateでは最新commandを消費する。物理catch-up中の重複生成を防ぐ。
- Local／Server Network NPCとも、Wire／Wall／Ladder Actionは疑似入力で要求された間だけ有効化し、終了後は休止する。
- Crowd OFFの`ActorTraversalCoordinator`も、疑似入力または継続中の特殊移動がある間だけ有効化する。
- Remote Clientへsimulationを引き渡す際は、replicated presentation用にWire Actionを復帰させる。
- Animator距離LODは距離だけでなくRenderer可視性も確認し、視錐台外の近距離NPCでAnimator graphを再有効化しない。
- 未接続かつblend完了後の`WireGroundAction`は補間計算を省略する。
- Benchmarkは乱数seed、Network／Local区分、Network NPC実数、fixed steps/frameを記録する。
- Benchmarkの全Subsystem Recorderは測定負荷になるため、`-npcBenchmarkSubsystems 0`で軽量な回帰判定を選択できる。

### 採用しなかった案

- Wire系ActionのFixedUpdate共通loop化：300体OFFの改善が小さく、300体ONが15.952 msから16.970 msへ悪化したため不採用。
- GPU Instancing Crowd Renderer：顔、頭、髪、装飾、Blend Shape、モデル別bind pose／軸変換を正しく再現できず不採用。特にSD Unitychanはメッシュと腰骨の軸変換が他モデルと異なる。
- 現行Prefabの一部を止めた状態を「Crowd導入前Baseline」とする方法：historical revisionそのものではなく、Fixed catch-upによる測定振れも大きいため回帰判定には使用しない。

### 残課題

次の機能課題へ進む前に、Crowd OFF 300体を導入前Server Network NPCの約58〜61 msと同等まで戻す。現在の機能同等条件は70.180 msであり、FixedUpdate／PhysX contact dispatchの残差を解消する。

接触内訳の追加計測では300体・3.88 fixed steps/frame時に`Physics.SendContactEvents` 11.004 ms、そのうち`ActorMotor`の接触更新が4.072 msだった。追加された特殊移動Behaviourをすべて無効にした診断でも300体71.123 ms／3.55 stepsに留まり、Prefab追加機能だけでは導入前との差を説明できない。接触コールバック自体は導入前にも存在するため、残差は個別メソッドの追加より、移動状態とfixed catch-upによって1 frame当たりのPhysX接触dispatch回数が増えるフィードバックを優先して追う。

その後の優先課題はスキンメッシュアニメーションと描画方式の再設計である。Unity標準描画を正解として、次の検証基盤を先に用意する。

- `SkinnedMeshRenderer.BakeMesh`との頂点比較
- bind pose時の単位行列検証
- ボーンインデックスとウェイト形式の検査
- モデル別の上方向／前方向と軸変換テスト
- 顔のBlend Shape、複数Skinned Mesh、頭・髪・装飾・Spring Boneの検証
- Humanoid Retargeting、遷移、特殊移動Animationとの同期確認

検証後にGPU Skinning、Animation Texture、Compute Skinning、Entities Graphicsのいずれを採用するか判断する。Crowd OFFをさらに軽量化する場合はNPC Motorの低頻度化が必要だが、移動・加速・特殊Actionの時間刻みが変わるため、互換Backendではなく別の軽量Backendとして扱う。
