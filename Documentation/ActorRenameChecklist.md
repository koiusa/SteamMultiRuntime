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

- [ ] `PlayerCompositeMotor`を`ActorCompositeMotor`へ変更
- [ ] `PlayerMotor`と関連する共通型を`ActorMotor`系へ変更
- [ ] `PlayerTraversalCoordinator`と契約を`ActorTraversalCoordinator`系へ変更
- [ ] Editor、Debug Window、テストの参照を更新
- [ ] Local／NetworkのPrefabとScene参照を更新

### Phase 4: 入力契約の境界整理

- [ ] NPCも利用する入力値・入力Source契約の命名を決定
- [ ] `PlayerGameplayInputReader`など実Player入力の型は`Player`名を維持
- [ ] Network同期データがPlayer固有かActor共通かを型ごとに確認

### Phase 5: Combat／Skill

- [ ] NPCが利用するHealth、Damage、Combat型だけを`Actor`候補として分類
- [ ] Player入力CoordinatorとPlayer専用Presentationは`Player`名を維持
- [ ] Server authorityとPlayer/NPC双方の経路を確認してから改名

## 検証チェック

- [x] 完了済みPhaseの旧型名がFirst-party Runtime、Editor、Prefab、Scene、文書に残っていない
- [ ] 旧型名の意図しない参照がFirst-party Runtime、Editor、Prefab、Scene、文書に残っていない
- [ ] 移動したUnity Assetの`.meta` GUIDが変更されていない
- [ ] Local Player PrefabのComponent参照がMissingにならない
- [ ] Network Player PrefabのComponent参照がMissingにならない
- [ ] Local NPC PrefabのComponent参照がMissingにならない
- [ ] Network NPC PrefabのComponent参照がMissingにならない
- [ ] Character Model PrefabのAnimator Driver参照がMissingにならない
- [ ] C# project／Unity script compilationが成功する
- [ ] Local Play Modeで移動、Jump、Traversal、Animationを確認
- [ ] Host／ClientでPlayer移動とAnimation同期を確認
- [ ] Network NPCがServer駆動され、ClientでAnimation表示されることを確認
- [x] 新規Reflection、`SendMessage`、型名文字列によるRuntime解決がない

未実施のPlay Mode、Prefab Import、Host／Client検証は、検索や静的検証だけでチェックしません。
