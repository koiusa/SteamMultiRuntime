# NPC Architecture

この文書をNPCのNavMesh機能、Player入力への変換、Local／Network駆動に関する詳細仕様の正本とします。全体配置は[CurrentClassStructure.md](CurrentClassStructure.md)、Inspector操作は[EditorSpecification.md](EditorSpecification.md)を参照してください。

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
├─ AiPlayerInputSource : IPlayerInputSource
├─ NpcDestinationDebugMarker
└─ NpcDestinationDebugMarkerNetSync（Network NPCのみ）
```

各Moduleは任意装着です。`NpcNavMeshController`は存在し、有効になっているModuleだけを利用します。回避方式を有効にした場合は`NavMeshAgent`標準回避を停止し、無効化時に復元します。

## 駆動経路

### Local NPC

```text
NPC Modules
  → NpcNavMeshController
  → AiPlayerInputSource
  → PlayerCompositeMotor
  → PlayerMotor / PlayerTraversalCoordinator
```

### Network NPC

```text
NPC Modules（Serverのみ更新）
  → NpcNavMeshController
  → SetInputSource(AiPlayerInputSource, npcTransform)
  → ServerDrivenPlayerController
  → PlayerCompositeMotor（Server Physics Tick）
  → NetworkVariable / NetworkTransformでClientへ同期
```

Network NPCはサーバー所有を前提とします。ClientはNavMesh、AI、物理を再計算せず、同期された移動・接地・ジャンプ・Traversal状態を表示します。

## 目的地デバッグ表示

- Serverが目的地と到着を判定する
- `NpcDestinationDebugMarkerNetSync`が目的地と表示状態を同期する
- Client側の`NavMeshAgent.hasPath`を到着判定に使わない
- 途中参加Clientも表示中の目的地を復元する

## Controller契約の分離

NPCの経路・移動状態は`INpcLocomotionState`、Network同期状態は
`IPlayerLocomotionState`として公開し、`IPlayerController`は共通Adapterだけが実装します。

```text
NetworkPlayer_NPC
├─ ServerDrivenPlayerController : IPlayerLocomotionState
├─ NpcNavMeshController : INpcLocomotionState
└─ PlayerControllerAdapter : IPlayerController

LocalPlayer_NPC
├─ NpcNavMeshController : INpcLocomotionState
└─ PlayerControllerAdapter : IPlayerController
```

`PlayerControllerAdapter`はLocalではNPC状態、Networkでは同期済み状態を共通のPlayer Controller契約へ変換します。
Local／Networkとも構成が同じになり、Animatorなどが行う`GetComponent<IPlayerController>()`の結果も一意です。

## 表示補間と移動床

Local／Network NPCはPlayerと同じ`PhysicsPresentationSmoother`を使用します。Physics RootのRigidbody補間は`None`とし、Character Modelなどの表示階層だけを`Presentation`上で補間します。Network Clientは物理Simulationを行わず、`NetworkTransform`の補間結果を表示します。

移動床上では`GroundMotionTracker`が`IGroundMotionSnapshotSource`から速度、変位、回転を一括取得します。床の移動行列はNPCごとに再計算せずPhysics tick単位で共有します。床変位は`PlayerMotor`が一度だけ適用し、物理押し出しとの二重適用は行いません。

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
