# Player Gameplay Architecture

Playerの移動、スキル、戦闘は、以下の論理階層で管理します。すべて同じPlayer GameObjectへ配置し、Transformの子には分けません。

## 予定クラス構成

以下をPlayer Gameplayの最終的なクラス構成とします。`[実装済]`は現在存在するクラス、`[予定]`は今後分割・追加するクラスです。

```text
Player
└─ PlayerCharacterCoordinator                           [実装済]
   ├─ PlayerCompositeMotor : IPlayerMotorMotionSink     [実装済]
   │  ├─ PlayerMotor : IPlayerMotor                     [実装済]
   │  └─ PlayerTraversalCoordinator                     [実装済]
   │     ├─ WallTraversalFeature                        [実装済]
   │     │  ├─ WallRunAction                            [実装済]
   │     │  ├─ WallSlideAction                          [実装済]
   │     │  └─ WallJumpAction                           [実装済]
   │     ├─ LadderTraversalFeature                      [実装済]
   │     │  ├─ LadderClimbAction                        [実装済]
   │     │  └─ LadderDetachAction                       [実装済]
   │     └─ WireTraversalFeature                        [実装済]
   │        ├─ WireAttachAction                         [実装済]
   │        ├─ WireSwingAction                          [実装済]
   │        ├─ WireReelAction                           [実装済]
   │        ├─ WireGroundAction                         [実装済]
   │        ├─ WireGrappleTargetingFeature              [実装済]
   │        └─ WireLineVisualFeature                    [実装済]
   │
   ├─ PlayerSkillCoordinator : IPlayerSkillCoordinator  [実装済]
   │  ├─ PlayerSkillFeature : IPlayerSkillFeature       [実装済・抽象基底]
   │  ├─ DashSkillFeature                               [実装済]
   │  ├─ SwordAttackSkillFeature                        [実装済]
   │  │  ├─ LightAttackAction                           [予定]
   │  │  ├─ HeavyAttackAction                           [予定]
   │  │  └─ ComboAttackAction                           [予定]
   │  ├─ GuardSkillFeature                              [実装済]
   │  │  └─ GuardCounterAction                          [予定]
   │  └─ HealSkillFeature                               [実装済]
   │
   └─ PlayerCombatCoordinator : IPlayerCombatCoordinator [実装済]
      ├─ PlayerHealthFeature : IPlayerHealthFeature      [実装済]
      ├─ PlayerDamageReceiverFeature                     [実装済]
      └─ PlayerHitDetectionFeature                       [実装済]
```

複数の動作を持たないSkillはFeature単体で実装します。Sword AttackやGuard Counterのように具体動作が増えた場合だけ、Skill Feature配下へActionを追加します。Actionを外部から直接発動せず、必ず対応するFeatureを入口にします。

### 入力・Networkを含む予定経路

```text
Local Player
└─ PlayerGameplayInputReader
   └─ PlayerCharacterCoordinator.TryActivateSkill(...)

Network Player
├─ Owner
│  └─ PlayerSkillInputState                              [予定]
└─ Server
   ├─ ServerDrivenPlayerControllerへのSkill入力統合      [予定]
   ├─ PlayerSkillCoordinator.TryActivate(...)            [実装済]
   └─ PlayerSkillRuntimeState                             [予定]
      ├─ ActiveSkillId
      ├─ ActivationSequence
      └─ Cooldown State
```

Local／NetworkともSkill Featureを直接呼ばず、`PlayerCharacterCoordinator`または`IPlayerSkillCoordinator`を共通入口にします。Networkでは発動可否、Hit判定、Damage、HealをServer Authorityで確定する予定です。

### 設定クラスの予定

```text
PlayerSkillSettings                                     [予定]
├─ SkillId
├─ Cooldown
└─ ActiveDuration

DashSkillSettings                                       [予定]
├─ PlayerSkillSettings
├─ Speed
├─ Duration
└─ AirDash条件

AttackSkillSettings                                     [予定]
├─ PlayerSkillSettings
├─ Damage
├─ Hit Radius
├─ Hit Timing
└─ Combo定義
```

現在は各Feature内のSerializeFieldに設定を保持しています。複数Characterや装備間で設定を共有する段階で、上記のSerializable設定クラスまたはScriptableObjectへ分離します。

## 現在の基本構成

```text
PlayerCharacterCoordinator
├─ PlayerCompositeMotor
│  ├─ PlayerMotor
│  └─ PlayerTraversalCoordinator
│     ├─ WallTraversalFeature
│     ├─ LadderTraversalFeature
│     └─ WireTraversalFeature
├─ PlayerSkillCoordinator
│  ├─ DashSkillFeature
│  ├─ SwordAttackSkillFeature
│  ├─ GuardSkillFeature
│  └─ HealSkillFeature
└─ PlayerCombatCoordinator
   ├─ PlayerHealthFeature
   ├─ PlayerDamageReceiverFeature
   └─ PlayerHitDetectionFeature
```

## 責務

- `PlayerCharacterCoordinator`は3領域への共通アクセスポイントです。
- `PlayerCompositeMotor`は通常移動、Traversal、外部モーション要求を処理します。
- `PlayerSkillCoordinator`は装着済みSkillの検索、排他実行、キャンセルを処理します。
- `PlayerCombatCoordinator`はHP、被ダメージ倍率、範囲Hit判定を仲介します。
- `Feature`はPlayerへ個別に付け外しできる機能単位です。
- `Action`はTraversal Feature内部の具体動作に使用します。

## Skillの呼び出し

入力およびNetworkコードは具象Skillを直接操作せず、次の共通入口を使用します。

```csharp
characterCoordinator.TryActivateSkill("DashSkillFeature", moveDirection);
characterCoordinator.TryActivateSkill("SwordAttackSkillFeature", aimDirection);
```

`skillId`をInspectorで設定した場合は、その値を使用します。未設定時はコンポーネント型名がIDになります。

## 依存方向

```text
Input / Network
    ↓
PlayerCharacterCoordinator
    ↓
PlayerSkillCoordinator
    ↓
SkillFeature
    ├─→ IPlayerMotorMotionSink
    └─→ IPlayerCombatCoordinator
```

Dashは`Rigidbody`を直接更新せず、`PlayerCompositeMotor`へ期限付きモーションを要求します。攻撃、Guard、Healは`PlayerCombatCoordinator`を経由します。

## 初期Skill

| Feature | 動作 |
|---|---|
| `DashSkillFeature` | 指定方向へ期限付きモーションを要求する |
| `SwordAttackSkillFeature` | 前方の範囲内へ一度だけダメージを与える |
| `GuardSkillFeature` | 発動中の被ダメージ倍率を下げる |
| `HealSkillFeature` | 生存中かつHPが減っている場合に回復する |

各Skillには共通してSkill ID、Cooldown、Active Durationがあります。Dashだけは固有のDurationをモーション時間にも使用します。
