# 現在の実装レビュー

レビュー基準コミット: `5e53d0b` からの作業差分

レビュー日: 2026-07-25

## 今回の変更概要

NPC のAI処理を着脱可能なコンポーネントへ分割し、Network NPCの疑似入力をPlayerと同じ駆動・同期経路へ統合した。

### NPC機能コンポーネント

- `NpcNavMeshMovementModule`
  - ランダム目的地生成
  - 中心復帰
  - 到着待機
  - スタック検出と再経路探索
- `NpcNavMeshSpeedModule`
  - 区間ごとの速度倍率
  - 中心復帰時の速度補正
  - 基準速度は `PlayerMotorSettings.MoveSpeed` を使用
- `NpcNavMeshSteeringModule`
  - NavMeshの経路方向からPlayer入力への変換
  - コーナリング／到着時入力補正
  - ローパス、デッドバンド、旋回速度制限
- `NpcNavMeshAvoidanceModule`
  - Boid／RVO方式の選択
  - 近傍探索と回避パラメータ
  - 有効中は `NavMeshAgent` 標準回避を停止し、無効化時に復元
- `NpcNavMeshJumpModule`
  - ジャンプ確率、クールダウン、最低移動速度

`NpcNavMeshController` は各コンポーネントを取得し、存在して有効な機能だけを利用する。Inspectorには装着済み機能の一覧、追加、選択操作を表示する。

## 入力とPlayer駆動の統合

既存の `AiPlayerInputSource` を継続利用し、Network NPCでは `ServerDrivenPlayerController.SetInputSource()` へ注入する。

```text
NPC NavMesh modules
        ↓
AiPlayerInputSource
        ↓
ServerDrivenPlayerController
        ↓
PlayerCompositeMotor
        ↓
PlayerMotor
```

NPC独自のMotor TickやAnimator状態同期は追加していない。移動、接地、ジャンプ、落下、速度は既存のPlayer用NetworkVariableを通してクライアントへ同期され、モデル側の `PlayerAnimatorStateDriver` も既存経路を利用する。

入力方向の基準も入力Sourceと一緒に注入する。

- Player: カメラTransform基準
- NPC: NPC自身のTransform基準

Network NPCのAI／NavMesh更新はサーバーだけが実行する。クライアントは物理やAIを再計算せず、NetworkTransformとPlayer状態同期を表示に使用する。

## 目的地デバッグマーカー

Network NPCでは、クライアントの `NavMeshAgent.hasPath` を到着判定に使用しない。クライアントは経路を所有しないためである。

- サーバーが目的地と到着を判定
- `syncedDestination` で目的地を同期
- `syncedVisible` で表示／消去状態を同期
- 途中参加クライアントも表示中の目的地を復元

## 最適化

- Boid／RVOの近傍ID、候補スコア、回避ベクトル配列を再利用
- NavMeshコーナー取得に `GetCornersNonAlloc` を使用
- Network NPCクライアントではAI／NavMesh更新を停止
- RVO半径は独自値を持たず `NavMeshAgent.radius` を使用
- 到着減速距離は `NavMeshAgent.stoppingDistance` を使用

## レビュー結果

### 良い点

1. AI入力とPlayer入力の差が入力Sourceだけに限定された。
2. PlayerのMotor、ネットワーク状態同期、AnimatorをNPCが再利用するため、状態定義の二重化を避けられている。
3. サーバー権限のNavMesh状態をクライアントがローカル判定しない構造になった。
4. NPC機能の追加・削除をコンポーネント単位で行える。
5. 操舵計算のフレームごとの配列確保を削減している。

### P0: PlayModeで確認が必要

1. **Network NPCの所有権条件**
   - `ServerDrivenPlayerController` は `IsOwner` 側で入力Sourceを読み、`IsServer` 側で物理をTickする。
   - 現在のNPCはサーバー所有を前提としている。
   - Ownershipをクライアントへ移す構成ではAI入力が正しく流れないため、サーバー所有をPrefab／Spawner契約として固定する必要がある。

2. **NetworkBehaviour追加後のPrefab互換性**
   - `NetworkPlayer_NPC.prefab` に `ServerDrivenPlayerController` を追加した。
   - NGOのNetworkBehaviour順序変更を伴うため、Host／Client双方でPrefabハッシュ、Spawn、Despawnを実機確認する。

### P1: 次の改善候補

3. **`IPlayerController`実装の一本化（対応済み）**
   - `NpcNavMeshController`の公開状態を`INpcLocomotionState`へ分離した。
   - `ServerDrivenPlayerController`は`IPlayerLocomotionState`を公開し、`IPlayerController`の実装をAdapterへ分離した。
   - Local／Network NPCと通常のNetwork Playerは、すべて`PlayerControllerAdapter`だけが`IPlayerController`を公開する。

4. **入力Source未設定の診断**
   - 外部入力注入を許可するため、`PlayerInputActionsProfile` が未設定でも `ServerDrivenPlayerController` を無効化しなくなった。
   - 通常PlayerでProfileと外部Sourceの両方がない場合に、Inspectorまたは実行時警告を出す診断が必要である。

5. **NPCモジュールの自動テスト**
   - 装着／未装着／無効化ごとの動作
   - AI入力のTransform基準
   - ジャンプTokenの一回消費
   - Boid／RVO切替
   - 到着／スタック復旧
   - マーカーの途中参加と消去同期

### P2: 保守性

6. **Controller内の回避計算をModuleへ移動する**
   - パラメータは `NpcNavMeshAvoidanceModule` に移動したが、Boid／RVO計算本体はControllerのpartial classに残る。
   - 完全な責務分離には、計算用Contextを定義してModule側へ移す余地がある。

7. **設定移行処理をEditor API化する**
   - 今回は既存のLocal／Network NPC Prefabを直接移行した。
   - 派生Prefabが増える場合は、旧シリアライズ設定を各Moduleへ移すEditor migrationを用意する方が安全である。

## 検証済み

- `Koiusa.SteamMultiRuntime.Locomoter.Netcode.Runtime` のMSBuild Compile
- `Koiusa.SteamMultiRuntime.Prototype.Runtime` のMSBuild Compile
- `Koiusa.SteamMultiRuntime.AnimationDriver.Runtime` は変更なし
- `git diff --check`

## 未検証

- Unity Editorでの完全な再インポート
- Host／Client／Dedicated ServerのPlayMode通し確認
- Network NPCのSpawn／Despawnと途中参加
- Local NPCの長時間移動
- Boid／RVOの多数NPC負荷
- Windows／macOS／Linuxプレイヤービルド
