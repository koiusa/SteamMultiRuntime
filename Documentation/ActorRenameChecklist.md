# Actor Rename Checklist

PlayerとNPCが共有するRuntime機能では`Actor`を使い、ユーザー入力、所有権、Profile、Player専用UIでは`Player`を維持します。

## 変更対象

### Phase 1: Animation表示

- [x] `PlayerAnimatorStateDriver`を`ActorAnimatorStateDriver`へ変更
- [x] `IPlayerAnimatorStateDriver`を`IActorAnimatorStateDriver`へ変更
- [x] Character Model Prefabの型名参照を更新
- [x] Animation／クラス構成ドキュメントを更新
- [x] `.meta` GUIDを維持してPrefabのScript参照を保護

### Phase 2: 共通Controller状態

- [x] `IPlayerLocomotionState`を`IActorLocomotionState`へ変更
- [x] `IPlayerController`を`IActorController`へ変更
- [x] `PlayerControllerAdapter`を`ActorControllerAdapter`へ変更
- [x] Local Player、Network Player、Local NPC、Network NPCの参照を更新
- [x] Animator、Camera、Debug表示の参照を更新

### Phase 3: 共通Motor／Traversal

- [x] `PlayerCompositeMotor`を`ActorCompositeMotor`へ変更
- [x] `PlayerMotor`と関連する共通型を`ActorMotor`系へ変更
- [x] `PlayerTraversalCoordinator`と契約を`ActorTraversalCoordinator`系へ変更
- [x] Editor、Debug Window、テストの参照を更新
- [x] Local／NetworkのPrefabとScene参照を更新

### Phase 4: 入力契約の境界整理

- [x] NPCも利用する入力値・入力Source契約を`ActorInputState`／`IActorInputSource`へ変更
- [x] `PlayerGameplayInputReader`など実Player入力の型は`Player`名を維持
- [x] NPCも利用するNetwork同期データとServer駆動型を`Actor`名へ変更

### Phase 5: Combat／Skill

- [x] NPCが利用するHealth、Damage、Combat、Skill実行型を`Actor`名へ変更
- [x] Player Skill入力Controllerと入力Bindingは`Player`名を維持
- [x] Server authorityとPlayer/NPC双方の経路を確認してから改名

### Phase 6: 残存する共有Presentation／Character契約

- [x] Animation modeとTraversal表示状態を`Actor`名へ変更
- [x] Player／NPC共通World Space Overlayを`ActorWorldSpaceOverlay`へ変更
- [x] Player／NPC共通Model Sync契約・実装を`ActorModelSync`系へ変更
- [x] Profile、Spawn、表示名、Compass、Targeting ownerはPlayer固有として維持

## 検証チェック

- [x] 完了済みPhaseの旧型名がFirst-party Runtime、Editor、Prefab、Scene、文書に残っていない
- [x] 旧型名の意図しない参照がFirst-party Runtime、Editor、Prefab、Scene、文書に残っていない
- [x] 移動したUnity Assetの`.meta` GUIDが変更されていない
- [x] Local Player PrefabのComponent参照がMissingにならない
- [x] Network Player PrefabのComponent参照がMissingにならない
- [x] Local NPC PrefabのComponent参照がMissingにならない
- [x] Network NPC PrefabのComponent参照がMissingにならない
- [x] Character Model PrefabのAnimator Driver参照がMissingにならない
- [x] C# project／Unity script compilationが成功する
- [ ] Local Play Modeで移動、Jump、Traversal、Animationを確認
- [ ] Host／ClientでPlayer移動とAnimation同期を確認
- [ ] Network NPCがServer駆動され、ClientでAnimation表示されることを確認
- [x] 新規Reflection、`SendMessage`、型名文字列によるRuntime解決がない

未実施のPlay Mode、Prefab Import、Host／Client検証は、検索や静的検証だけでチェックしません。

## 最終検証結果

- Unity `6000.3.9f1` Batch Mode Import／Script Compilation成功
- Tundra build成功、C# compiler errorなし
- 改名したUnity AssetのGUID不一致 `0`
- 完了対象の旧共有型名の残存 `0`
- `git diff --check`成功
- Play Mode、Host／Client、Network NPC実動作確認は未実施
