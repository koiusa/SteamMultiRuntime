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

回避のNPC近傍検索は`NpcCrowdSimulation`がPersistent Native Collection上へ構築する共有Spatial Gridを使用します。Grid構築とBoid／RVO計画はBurst Jobで並列実行し、Main ThreadにはUnity ObjectからのSnapshot取得、結果適用、Motor Tickだけを残します。各NPCがPhysics World全体へOverlap Queryを発行しないため、NPC数増加時も近傍Cellだけを調べます。経路方向にはNavMeshAgentのallocation-freeな`steeringTarget`を使い、`agent.path`取得によるNPC数比例のGCを発生させません。現在の標準PrefabはNetwork NPCがBoid、Local NPCがRVOを使用します。

重いSteering計画は設定周期で更新しますが、その計画値に対するLow-passと最大旋回速度の適用はPlayer Loopごとに連続更新します。方向角Deadband以内の微小な左右変化は現在の進行方向を維持します。Boid／RVOの回避成分は目標速度成分の75%以下へ制限し、目標速度がない場合は回避移動を生成しません。これにより、計画値の段階更新、回避方向の符号反転、低速時の回避過多による蛇行とその場旋回を抑えます。標準Local／Network NPCはLow-pass 1.5 Hz、方向角Deadband 3度、最大旋回速度120度／秒です。Crowd NPCのNavMesh `nextPosition`はMovement適用時と移動床追従時に同期し、到着・中心復帰の状態観測は距離LODされた期限で行います。

## 駆動経路

`Tools > SteamMultiRuntime > NPC > Crowd Simulation`で専用ウィンドウを開くと、`Assets`と`Packages`にある`NpcNavMeshController`を含むプレファブが一覧表示されます。各プレファブのチェックでCrowd ON／OFFを選び、そのプレファブの移動Backendを次回Play開始時から切り替えます。有効時は`NpcCrowdAgent`／`NpcCrowdMotor`による共有Burst Crowd、無効時は`ActorCompositeMotor`／Dynamic Rigidbodyによる従来のNPC個別更新です。設定は`NpcNavMeshController.Use Crowd Simulation`としてプレファブへ保存されるため、Local／Network NPCを個別に設定できます。読み取り専用Packageは一覧へ含めますが編集しません。AI判断、NavMesh目的地、FutureAction／テストドライバからの疑似入力、Wire入力は`BuildNpcInputCommand`で一度だけ生成し、Crowd Commandまたは従来Motor入力へ変換します。Playerと共通のMotor／Traversal契約はどちらでも維持します。実行中のHot Swapは状態移行の不整合を避けるため対象外です。

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

Network NPCはサーバー所有を前提とします。ClientはNavMesh、AI、物理を再計算せず、同期された移動・接地・ジャンプ・Traversal状態を表示します。Scene切替後のNetwork Spawnでは`OnEnable`が`OnNetworkSpawn`より先にCrowdへ仮登録されるため、Crowdの各描画フレーム先頭でNetwork権限を確認し、Remote ClientをMovementより前に登録解除します。距離LODされたNavigation更新まで解除を遅らせてClient姿勢を表示補間へ記録することはありません。

Network NPCの`NetworkTransform`はPlayerとは別のPrefab設定を持ち、Position 0.1m、Y Rotation 6度を送信閾値とします。標準移動速度5m/sでは30Hz Network Tickごとの変位が閾値を超えるため、Remote Clientへ継続的に位置サンプルを供給します。0.5m閾値による約10Hzの更新は補間上限0.1秒と同程度になり、配送揺らぎやUnreliable Deltaの欠落時に補間バッファが尽きて移動が飛び飛びになるため使用しません。Netcode 2.11.2の`NetworkTransform`は権限側の全InstanceをNetwork Tickごとに内部走査するため、個体別の変化判定コスト自体は残ります。

Crowd Server NPCが`IGroundMotionSource`／`IGroundMotionSnapshotSource`を持つ移動床へBindingしている間だけ、Position Thresholdを0.02mへ下げます。床は高頻度で同期される一方NPCが通常の0.1m閾値のままだと、Remote Client上で床が先行して相対位置誤差となるためです。Binding開始／解除のpush通知で切り替え、通常地面と空中では0.1mへ戻します。

Network NPCのServer／Remote ClientにおけるDynamic／Kinematic切替は、`AutoUpdateKinematicState`を有効にした`NetworkRigidbody`が所有します。`ServerDrivenActorController`はこの状態を重複設定せず、表示補間との二重適用を避けるためRigidbody補間を`None`に固定します。

`ServerDrivenActorController`は個別`Update`を持ちません。Network Playerだけを`ServerDrivenActorInputLoop`へ登録して単一のPlayer Loopから入力を読み取り、入力を持たないServer NPCが毎フレーム空の`Update` callbackを受けるコストを除外します。

`NpcNavMeshController`も個別`Update`を持ちません。従来Rigidbody Backendだけを`NpcConventionalUpdateLoop`へ登録してAI計画を一括更新し、Crowd BackendのNPCが従来経路判定のためだけに空の`Update` callbackを受けないようにします。

目的地Markerの終了判定は`NpcNavMeshMovementModule.OnDestinationArrived`からのpush通知で行います。Local markerとNetwork marker同期は個別`Update`やNavMesh到着判定を重複実行せず、この通知で表示を閉じます。Marker Objectは到着ごとに破棄せず非表示で再利用し、目的地更新のたびに`Instantiate`／`Awake`を発生させません。

Wire未接続ActorはWire用`FixedUpdate`／`LateUpdate`を持ちません。接続時に`WireTraversalUpdateLoop`へ登録し、接続中の`WireTraversalFeature`、`WireSwingAction`、`WireGroundAction`だけを単一のFixed／Late callbackから更新して、待機中NPCの空振りcallbackを除外します。

`LocalNPC.prefab`と`NetworkNPC.prefab`は、Wall／Ladder／WireのFeature・Action一式を同じGameObject構成で保持します。特殊ActionはPrefabへ明示的にSerializeし、Runtime Spawn時には追加しません。

Crowd有効時も`ServerDrivenActorController.ApplyServerNpcCrowdState`が通常のNetwork Motorと同じ`ActorMovementFlagsState`と`WireSwingNetworkState`を送信します。Ladder状態／速度、WallRun状態／法線、Wire Anchor／Rope LengthをRemote Clientへ同期し、Client側のCoordinatorは受信したWire状態だけを表示へ適用します。

`Use Crowd Simulation`が有効なLocal／Network Server NPCのMovement Tickは、個別Componentの`FixedUpdate`ではなく`NpcCrowdSimulation`から30Hzで一括実行します。描画が遅れた場合も1描画フレームにつき最大1回とし、FixedUpdateのcatch-upがCrowd全体を複数回評価する負荷循環を防ぎます。Movement積分は30Hz、Ground／Wall ProbeとPenetrationは15Hzです。AI CommandとNavMesh到着・中心復帰観測はNPCごとの期限で位相分散し、Cameraから12m以内は10Hz、30m以内は5Hz、それより遠距離は2Hzで実行します。Cameraが存在しないDedicated Serverでは10Hzを維持します。中間Movement Tickは直近のCommandと接地状態を再利用します。移動床の50Hz物理姿勢通知によるBinding中NPCの即時追従は、この多段レートとは別経路で維持します。無効時は比較用の従来Rigidbody経路として`NpcConventionalPhysicsLoop`から`ActorCompositeMotor`を一括駆動します。この経路も過負荷時は1描画フレームにつき最大1回だけNPC Motorを評価し、Unity Physics自体のfixed stepとは分離してcatch-upの正帰還を抑えます。プロジェクトのMaximum Allowed Timestepは0.10秒（標準Fixed Timestep 0.02秒で最大5 step）です。

Crowd実装のファイル責務は次のように分離します。`NpcCrowdSimulation`は`NpcCrowdAgent`だけを登録し、30HzのPlayer Loop、Jobと一括Queryの編成を所有します。Native Collectionの確保・破棄は`NpcCrowdSimulation.Buffers`へ分離します。`NpcCrowdAgent`はSimulationとNPC一体分の実行境界であり、Probe、Movement Snapshot、結果適用、表示補間、Network状態反映を担当します。Steering用Snapshotは位置と速度を既存のRigidbody／Crowd Motorキャッシュから取得し、重力軸はCrowd Tick単位で共有します。回避方式の不変設定はController初期化時にBlittableテンプレートへ変換し、Movement TickごとのComponentプロパティ走査を行いません。`NpcNavMeshController`はNavMesh AI判断、目的地、共通疑似入力とBackend選択を担当し、Simulationから直接参照されません。`NpcCrowdMotor`はKinematic状態、接地・壁、外部物体接触と移動結果を所有します。Job境界を通過するBlittable DTO、共通入力DTO、接触設定は`NpcCrowdData`、モデル生成時のAnimator／Spring／装飾設定は`NpcCrowdModelPresentation`が所有します。`NpcCrowdSpringSimulation.Registration`はモデル別Spring Rigの収集と無効化を所有します。Crowd有効時はLocal／Network Serverとも`NpcCrowdMotor`の一括Movement Job、無効時は`ActorCompositeMotor`の個別Physics Tickを使用します。

NPCの通常移動・加減速・ジャンプ・重力・接地は`NpcCrowdMotor`のNative状態としてBurst Jobで計算します。NPC RigidbodyはKinematic、Colliderは攻撃Overlap用Triggerとして残します。接地は`CapsulecastCommand`と`OverlapCapsuleCommand`のBatchで一括取得します。`NpcCrowdMovingPlatformAction`は接地した`IGroundMotionSnapshotSource`の床Snapshotを共有利用し、Crowd Tickの実測間隔に対応する床の点速度・変位・回転をCrowd移動へ合成します。Castが継ぎ目で短時間外れてOverlapだけが残った場合も、床Bindingと変位を維持します。PlayerおよびCrowd無効NPCは従来のDynamic Rigidbody Motorと`GroundMotionTracker`を使用します。

Vertical／Spin移動床がCrowd tick間にカプセルへ入り、Shape Castが開始Overlapを返さない場合は、最大4件のOverlapから現在Binding中の床を優先します。新規Binding時は`ComputePenetration`の押し出し法線が接地法線条件を満たすColliderだけを床として採用し、配列先頭の側面や別Colliderを誤って床扱いしません。

CastとOverlapの両方が一時的に外れた場合も、移動床Colliderの`ClosestPoint`がNPCの接地可能範囲内なら、固定tick数ではなく幾何距離を条件に既存BindingとSnapshot追従を維持します。ジャンプなどで非接地になった場合、Colliderが無効になった場合、または接地可能距離を越えた場合に解除します。

`PrototypeMotionMover`は`IGroundMotionPhysicsPoseSource`を実装し、50HzのPhysics姿勢を適用した直後にインスタンス単位の型付き通知を送ります。SimulationはNPCが床へBindingした時だけそのSourceを購読し、床ごとのBinding中NPCレジストリだけを走査して同じ点変位と回転を即時適用します。購読はSourceのDisable／破棄通知でも解除し、Scene遷移後にFollower参照を残しません。移動床ごとに全Crowdを走査せず、30Hz Crowd tickまで床だけが先行してカプセルへ入り込む時間差も作りません。初回接触時の`IGroundMotionSnapshotSource`／`IGroundMotionSource`解決は共通`GroundMotionSourceResolver`がTransform単位でキャッシュし、Playerの`GroundMotionTracker`とCrowdの両方が共有します。同じ床へ複数Actorが接触しても親階層探索を繰り返さず、全`MonoBehaviour`配列も生成・走査しません。Crowd NPCと`IGroundMotionPhysicsPoseSource`のCollider Pairは、床またはNPCの有効化時に型付きRegistryから事前登録して無効化します。初回接触のホットパスで大量の`Physics.IgnoreCollision`を登録せず、`Physics.SendTriggerEvents`にも床接触を配送しません。Collider自体は有効なため、LadderやSensorのTrigger検出と手動Physics Queryは維持します。すべての`IGroundMotionPhysicsPoseSource`はpush通知側で変位を適用し、30Hz側では具象型に依存せず変位をゼロにして二重適用を防ぎます。NPC個別の`FixedUpdate`、実行順属性、Reflectionは使用しません。

NPCと`ServerDrivenNetworkRigidbody`の接触は権限側だけがImpulseを適用します。Network ServerではSpawn済みServer Instance、Local実行では未SpawnのLocal Instanceを対象とし、Network Client上のKinematic複製には適用しません。

Crowd NPCがDynamic Rigidbodyへ与えるImpulseは、Crowd専用の倍率や上限値を持ちません。PlayerのDynamic Rigidbody接触と同じく、NPCと接触相手双方の`Rigidbody.mass`および接触法線方向の相対速度から非弾性衝突のImpulseを求めます。接触解決では、NPC側の壁向き速度を除去する前の相対速度を物体へ伝え、その後でKinematic Motorのめり込みを解消します。これにより標準Player／NPCが共有するCharacter Coreの質量設定を接触応答にもそのまま反映します。

Player／NPC共通の`ActorAnimatorStateDriver`は個別`LateUpdate`を持たず、単一SchedulerがCamera距離に応じて更新頻度を切り替えます。各`ActorAnimatorStateDriver`のInspectorにある`Animation Update LOD`で近距離／中距離の境界と近距離／中距離／遠距離の各更新Hzを設定できます。近距離／中距離ではAnimatorを連続評価し、状態Parameterを設定Hzで更新します。中距離でAnimator自体を15Hz停止／再開する方式は姿勢が一時停止して見えるため採用しません。遠距離だけはAnimator状態を保持したまま5Hzのフレームで明示評価し、それ以外は停止します。Cameraが存在しないDedicated ServerではAnimatorを停止します。初期値は近距離12m・30Hz、中距離30m・15Hz、遠距離5Hzです。

NPCモデルの`UTJ.SpringManager`と`UnityChan.SpringManager`は、全NPC共通の`NpcNavMeshController`がNPC Rootを中央Solverへ事前登録し、`CharacterPrefabLoader`の共通モデル生成通知を1つだけ購読する`NpcCrowdSpringSimulation`が型付きAdapterからRigを登録します。Network NPCのLoaderは`OnNetworkSpawn`で動的追加されるため、NPCの`Awake`時点に個別Loaderを探索・購読しません。Controller初期化より前に生成済みのモデルは`CharacterPrefabLoader.LastInstantiatedObject`から登録し、Crowd ON／OFFのどちらでも同じ中央Solverを使用します。少なくとも1本のBoneを登録できたManagerだけ元のManager／Bone更新を停止し、ばね演算とSphere／Capsule／Panel Collider解決をBurst Jobで一括処理します。Animator Schedulerが姿勢を更新したRigだけをSnapshotし、NPC Rig単位のJob内で親から子を順に解決します。Jobは同じフレームに待たず、次フレームで完了済みの場合だけLocal Rotationを適用し、未完了なら前回姿勢を維持します。Camera距離に応じて30Hz／15Hz／5Hzへ更新頻度を落とします。

Marie／Tokoが使用するTokoChanz版`UTJ.SpringBone`は、初期Local Rotationを基準にBone AxisからTip方向へのAim Rotationを再構築します。SD UnityChanが使用する旧`UnityChan.SpringBone`の現在姿勢差分方式とは回転契約が異なるため、中央Solver内でも型別に回転復元式を分けます。

モデル生成時とモデルの`OnEnable`完了後に型付きの一回限りガードを適用し、NPC上の`AutoBlinkforSD`、`SDRandomWind`、`UTJ.HighLeg`を停止します。Spring Manager／Boneは中央Solverへの登録後に個別更新を停止します。型名文字列探索、Reflection、毎フレーム監視は使用しません。

Spawn位置の最小距離判定は共有Spatial Gridへ登録済みの近傍Cellだけを調べます。生成済み全位置との総当たり比較は行わず、大量生成時の位置決定をO(N²)にしません。Character Debug Overlayは各Actorを表示候補として登録しますが、個別`Update`を持たず単一の中央Loopが現在のUI所有者だけを更新します。

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

Local／Network NPCはPlayerと同じ`PhysicsPresentationSmoother`を使用します。Physics RootのRigidbody補間は`None`とし、Character Modelなどの表示階層だけを`Presentation`上で補間します。PlayerはFixed時刻、Crowd Motorは通常時に30Hzサンプルの時刻と実測間隔を補間器へ渡します。Physics姿勢通知を持つ移動床へのBinding中は、床追従後の50Hzサンプルだけを補間器へ渡し、30Hz Crowd結果を同じ履歴へ混在させません。`GroundMotionPresentationScheduler`が各描画フレームの`LateUpdate`で、移動更新後に移動床Presentationを先に確定し、その後でActor Presentationを1回だけ更新します。補間を適用した後にPhysics Rootが30Hzで動いて表示階層へ段差移動を再適用すること、床とActorが別々の順序で表示姿勢を適用する位相差、複数周期からの重複Captureを作りません。Network Clientは物理Simulationを行わず、`NetworkTransform`の補間結果を表示します。

移動床上では`GroundMotionTracker`が`IGroundMotionSnapshotSource`から速度、変位、回転を一括取得します。床の移動行列はNPCごとに再計算せずPhysics tick単位で共有します。床変位は`ActorMotor`が一度だけ適用し、物理押し出しとの二重適用は行いません。

## 変更時の確認項目

1. Moduleの装着、未装着、無効化が独立して動作するか
2. AI入力がNPC自身のTransform基準になっているか
3. Boid／RVO切替時にAgent設定が復元されるか
4. 到着、スタック復旧、中心復帰が競合しないか
5. Network NPCがサーバー所有になっているか
6. Spawn、Despawn、途中参加時の同期が正しいか
7. PlayerとNPCでPresentation補間が二重適用されていないか
8. 多数のNPCが移動床へ乗った際に床行列がNPCごとに再計算されていないか
