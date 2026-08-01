# NPC Architecture

この文書をNPCのNavMesh機能、Player入力への変換、Local／Network駆動に関する詳細仕様の正本とします。全体配置は[CurrentClassStructure.md](CurrentClassStructure.md)、Inspector操作は[EditorSpecification.md](EditorSpecification.md)を参照してください。

大量NPC対応で得られた成果と、スキンメッシュアニメーション／描画に残る性能課題は[Development Notes](DevelopmentNotes.md#大規模npc-crowd化の到達点と残課題)に記録します。

## クラス構成

```text
NpcNavMeshController : INpcLocomotionState
├─ NpcNavMeshMovementModule
│  ├─ ランダム目的地
│  ├─ 到着待機
│  ├─ 中心復帰
│  └─ スタック検出・再経路探索
├─ NpcNavMeshSpeedModule
│  ├─ 区間速度倍率
│  └─ 中心復帰速度
├─ NpcNavMeshSteeringModule
│  ├─ 経路方向からPlayer入力への変換
│  └─ 平滑化、デッドバンド、旋回制限
├─ NpcNavMeshAvoidanceModule
│  ├─ Boid
│  └─ RVO
├─ NpcNavMeshJumpModule
│  └─ 確率、クールダウン、最低移動速度
├─ AiActorInputSource : IActorInputSource
├─ NpcCrowdTraversalInput
│  └─ FutureAction／テストドライバからの疑似入力保持
├─ NpcControllerInputCommand
│  └─ Backend非依存のMove／Jump／Wire入力Snapshot
├─ NpcCrowdAgent
│  └─ NpcCrowdSimulationとの実行境界
├─ NpcCrowdMotor
│  └─ Kinematic移動、接地、壁、移動床、外部物体接触
├─ NpcDestinationDebugMarker
└─ NpcDestinationDebugMarkerNetSync（Network NPCのみ）
```

各Moduleは任意装着です。`NpcNavMeshController`は存在し、有効になっているModuleだけを利用します。回避方式を有効にした場合は`NavMeshAgent`標準回避を停止し、無効化時に復元します。

回避のNPC近傍検索は`NpcCrowdSimulation`がPersistent Native Collection上へ構築する共有Spatial Gridを使用します。Grid構築とBoid／RVO計画はBurst Jobで並列実行し、Main ThreadにはUnity ObjectからのSnapshot取得、結果適用、Motor Tickだけを残します。各NPCがPhysics World全体へOverlap Queryを発行しないため、NPC数増加時も近傍Cellだけを調べます。経路方向にはNavMeshAgentのallocation-freeな`desiredVelocity`を使い、`agent.path`取得によるNPC数比例のGCを発生させません。現在の標準PrefabはNetwork NPCがBoid、Local NPCがRVOを使用します。

重いSteering計画は設定周期で更新しますが、その計画値に対するLow-passと最大旋回速度の適用はPlayer Loopごとに連続更新します。方向角Deadband以内の微小な左右変化は現在の進行方向を維持します。Boid／RVOの回避成分は目標速度成分の75%以下へ制限し、目標速度がない場合は回避移動を生成しません。これにより、計画値の段階更新、回避方向の符号反転、低速時の回避過多による蛇行とその場旋回を抑えます。標準Local／Network NPCはLow-pass 1.5 Hz、方向角Deadband 3度、最大旋回速度120度／秒です。

## 駆動経路

`NpcNavMeshController`のInspectorにある`Use Crowd Simulation`で、NPCの移動Backendを起動時に切り替えます。有効時は`NpcCrowdAgent`／`NpcCrowdMotor`による共有Burst Crowd、無効時は`ActorCompositeMotor`／Dynamic Rigidbodyによる従来のNPC個別更新です。AI判断、NavMesh目的地、FutureAction／テストドライバからの疑似入力、Wire入力は`BuildNpcInputCommand`で一度だけ生成し、Crowd Commandまたは従来Motor入力へ変換します。Playerと共通のMotor／Traversal契約はどちらでも維持します。切替はPrefabまたはScene上のチェック一つで行い、実行中のHot Swapは状態移行の不整合を避けるため対象外です。

### Local NPC

```text
NPC Modules / FutureAction / Test Driver
  → NpcNavMeshController
  → BuildNpcInputCommand（NPCごとに1回）
  → NpcControllerInputCommand
      ├─ Crowd ON
      │   → NpcCrowdCommand
      │   → NpcCrowdAgent
      │   → NpcCrowdSimulation / NpcCrowdMotor
      └─ Crowd OFF
          → ActorCompositeMotor
          → ActorMotor / ActorTraversalCoordinator
```

### Network NPC

```text
NPC Modules / FutureAction / Test Driver（Serverのみ更新）
  → NpcNavMeshController
  → BuildNpcInputCommand（NPCごとに1回）
  → NpcControllerInputCommand
      ├─ Crowd ON
      │   → NpcCrowdCommand
      │   → NpcCrowdAgent / NpcCrowdMotor
      │   → ApplyServerNpcCrowdState
      └─ Crowd OFF
          → ServerDrivenActorController.TickServerNpcPhysics
          → ActorCompositeMotor（Server Physics Tick）
  → NetworkVariable / NetworkTransformでClientへ同期
```

Network NPCはサーバー所有を前提とします。ClientはNavMesh、AI、物理を再計算せず、同期された移動・接地・ジャンプ・Traversal状態を表示します。

`LocalNPC.prefab`と`NetworkNPC.prefab`は、Wall／Ladder／WireのFeature・Action一式を同じGameObject構成で保持します。特殊ActionはPrefabへ明示的にSerializeし、Runtime Spawn時には追加しません。

Crowd有効時も`ServerDrivenActorController.ApplyServerNpcCrowdState`が通常のNetwork Motorと同じ`ActorMovementFlagsState`と`WireSwingNetworkState`を送信します。Ladder状態／速度、WallRun状態／法線、Wire Anchor／Rope LengthをRemote Clientへ同期し、Client側のCoordinatorは受信したWire状態だけを表示へ適用します。

`Use Crowd Simulation`が有効なLocal／Network Server NPCのPhysics Tickは、個別Componentの`FixedUpdate`ではなく`NpcCrowdSimulation`から30Hzで一括実行します。描画が遅れた場合も1描画フレームにつき最大1回とし、FixedUpdateのcatch-upがCrowd全体を複数回評価する負荷循環を防ぎます。空中・特殊移動中の壁Probeは毎Crowd Tick、通常接地移動中はNPCごとに位相をずらして隔Tickで実行します。無効時は比較用の従来経路としてNPCごとの`FixedUpdate`から`ActorCompositeMotor`を駆動します。

Crowd実装のファイル責務は次のように分離します。`NpcCrowdSimulation`は`NpcCrowdAgent`だけを登録し、30HzのPlayer Loop、Jobと一括Queryの編成を所有します。Native Collectionの確保・破棄は`NpcCrowdSimulation.Buffers`へ分離します。`NpcCrowdAgent`はSimulationとNPC一体分の実行境界であり、Probe、Movement Snapshot、結果適用、表示補間、Network状態反映を担当します。`NpcNavMeshController`はNavMesh AI判断、目的地、共通疑似入力とBackend選択を担当し、Simulationから直接参照されません。`NpcCrowdMotor`はKinematic状態、接地・壁・外部物体接触と移動結果を所有します。Job境界を通過するBlittable DTO、共通入力DTO、接触設定は`NpcCrowdData`、モデル生成時のAnimator／Spring／装飾設定は`NpcCrowdModelPresentation`が所有します。`NpcCrowdSpringSimulation.Registration`はモデル別Spring Rigの収集と無効化を所有します。Crowd有効時はLocal／Network Serverとも`NpcCrowdMotor`の一括Movement Job、無効時は`ActorCompositeMotor`の個別Physics Tickを使用します。

NPCの通常移動・加減速・ジャンプ・重力・接地は`NpcCrowdMotor`のNative状態としてBurst Jobで計算します。NPC RigidbodyはKinematic、Colliderは攻撃Overlap用Triggerとして残します。接地は`RaycastCommand.ScheduleBatch`で一括取得します。`NpcCrowdMovingPlatformAction`は接地した`IGroundMotionSnapshotSource`の床Snapshotを共有利用し、Crowd Tickの実測間隔に対応する床の点速度・変位・回転をCrowd移動へ合成します。Castが継ぎ目で短時間外れてOverlapだけが残った場合も、床Bindingと変位を維持します。PlayerおよびCrowd無効NPCは従来のDynamic Rigidbody Motorと`GroundMotionTracker`を使用します。

NPCと`ServerDrivenNetworkRigidbody`の接触は権限側だけがImpulseを適用します。Network ServerではSpawn済みServer Instance、Local実行では未SpawnのLocal Instanceを対象とし、Network Client上のKinematic複製には適用しません。

Player／NPC共通の`ActorAnimatorStateDriver`は個別`LateUpdate`を持たず、単一SchedulerがCamera距離に応じて更新頻度を切り替えます。各`ActorAnimatorStateDriver`のInspectorにある`Animation Update LOD`で近距離／中距離の境界と近距離／中距離／遠距離の各更新Hzを設定できます。遠距離ActorはAnimator状態を保持したまま更新時だけ評価し、Cameraが存在しないDedicated ServerではAnimatorを停止します。初期値は近距離12m・30Hz、中距離30m・15Hz、遠距離2Hzです。

NPCモデルの`UTJ.SpringManager`と`UnityChan.SpringManager`は個別`LateUpdate`を停止し、`NpcCrowdSpringSimulation`へ登録します。中央SchedulerはAnimator評価後、`TransformAccessArray`からボーンとSpring Colliderの姿勢をJob内で取得し、Verlet積分、バネ、減衰、重力、長さ制約、Sphere／Capsule／Panel Collider制約、回転算出、Transform反映をBurstで並列実行します。Camera距離による初期更新頻度は15m以内30Hz、40m以内15Hz、それ以遠5Hzです。PlayerのSpringManagerは従来処理を維持します。

Spawn位置の最小距離判定は共有Spatial Gridへ登録済みの近傍Cellだけを調べます。生成済み全位置との総当たり比較は行わず、大量生成時の位置決定をO(N²)にしません。Character Debug Overlayの登録・解除も既存Overlay全体を再走査しません。

## 目的地デバッグ表示

- Serverが目的地と到着を判定する
- `NpcDestinationDebugMarkerNetSync`が目的地と表示状態を同期する
- Client側の`NavMeshAgent.hasPath`を到着判定に使わない
- 途中参加Clientも表示中の目的地を復元する

## Controller契約の分離

NPCの経路・移動状態は`INpcLocomotionState`、Network同期状態は
`IActorLocomotionState`として公開し、`IActorController`は共通Adapterだけが実装します。

```text
NetworkNPC
├─ ServerDrivenActorController : IActorLocomotionState
├─ NpcNavMeshController : INpcLocomotionState
└─ ActorControllerAdapter : IActorController

LocalNPC
├─ NpcNavMeshController : INpcLocomotionState
└─ ActorControllerAdapter : IActorController
```

`ActorControllerAdapter`はLocalではNPC状態、Networkでは同期済み状態を共通のPlayer Controller契約へ変換します。
Local／Networkとも構成が同じになり、Animatorなどが行う`GetComponent<IActorController>()`の結果も一意です。

## 表示補間と移動床

Local／Network NPCはPlayerと同じ`PhysicsPresentationSmoother`を使用します。Physics RootのRigidbody補間は`None`とし、Character Modelなどの表示階層だけを`Presentation`上で補間します。PlayerはFixed時刻、Crowd Motorは30Hzサンプルの時刻と実測間隔を補間器へ渡し、Crowd計算周期とは独立して表示を毎フレーム更新します。Network Clientは物理Simulationを行わず、`NetworkTransform`の補間結果を表示します。

移動床上では`GroundMotionTracker`が`IGroundMotionSnapshotSource`から速度、変位、回転を一括取得します。床の移動行列はNPCごとに再計算せずPhysics tick単位で共有します。床変位は`ActorMotor`が一度だけ適用し、物理押し出しとの二重適用は行いません。

## 変更時の確認項目

1. Moduleの装着、未装着、無効化が独立して動作するか
2. AI入力がNPC自身のTransform基準になっているか
3. Jump Tokenが一度だけ消費されるか
4. Boid／RVO切替時にAgent設定が復元されるか
5. 到着、スタック復旧、中心復帰が競合しないか
6. Network NPCがサーバー所有になっているか
7. Spawn、Despawn、途中参加時の同期が正しいか
8. PlayerとNPCでPresentation補間が二重適用されていないか
9. 多数のNPCが移動床へ乗った際に床行列がNPCごとに再計算されていないか
