# NPC Architecture

この文書をNPCのNavMesh機能、Player入力への変換、Local／Network駆動に関する詳細仕様の正本とします。全体配置は[CurrentClassStructure.md](CurrentClassStructure.md)、Inspector操作は[EditorSpecification.md](EditorSpecification.md)を参照してください。

## クラス構成

```text
NpcNavMeshController : IPlayerController
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

## 現在の注意点

`NetworkPlayer_NPC`には2つの`IPlayerController`実装があります。

```text
NetworkPlayer_NPC
├─ ServerDrivenPlayerController : IPlayerController
└─ NpcNavMeshController : IPlayerController
```

現在はNPC側がNetwork Controllerへ公開状態を委譲しています。単一実装を仮定した`GetComponent<IPlayerController>()`には曖昧さがあるため、将来はNPC固有状態を別契約へ分離する余地があります。

## 変更時の確認項目

1. Moduleの装着、未装着、無効化が独立して動作するか
2. AI入力がNPC自身のTransform基準になっているか
3. Jump Tokenが一度だけ消費されるか
4. Boid／RVO切替時にAgent設定が復元されるか
5. 到着、スタック復旧、中心復帰が競合しないか
6. Network NPCがサーバー所有になっているか
7. Spawn、Despawn、途中参加時の同期が正しいか
