# Player Gameplay Architecture

Playerの移動、スキル、戦闘は、以下の論理階層で管理します。すべて同じPlayer GameObjectへ配置し、Transformの子には分けません。

## パッケージ分離

Player GameplayはNetcodeの有無でパッケージを分離します。

```text
com.koiusa.steammultiruntime.player
├─ Runtime: Coordinator、Skill／Combat Feature、Local Skill入力
└─ Editor: 共通Inspector、Local入力の参照設定補助

com.koiusa.steammultiruntime.player.netcode
├─ Runtime: NetworkPlayerSkillController
└─ Editor: Network入力の参照設定補助
```

依存方向は`Player.Netcode → Player`の一方向です。基本Playerパッケージは`Unity.Netcode`およびPlayer.Netcode型を参照しません。これにより、Local専用構成ではNetcode依存なしでPlayer Gameplayを利用できます。

## 現在の実装状況

`ActorCharacterCoordinator`を中心とするPlayer Gameplayの基盤コードと、標準Character Prefabへの適用は完了しています。Local Playerでは`PlayerSkillInputController`、Network Playerでは`NetworkPlayerSkillController`が`Combat/Attack`、`Player/Dash`、`Player/Guard`、`Player/Heal`を読み取ります。Network Playerの発動可否、Hit判定、Damage、HealをServer Authorityで確定します。DashはStrafe移動方向へ対応し、GuardはLocal／Remote双方のShield表示へ接続済みです。Combat全般のPlay Mode検証は継続中です。

### 実装済み

- `ActorCharacterCoordinator`をMovement、Skill、Combatへの共通アクセスポイントとして追加
- `ActorSkillCoordinator`によるSkill ID検索、排他実行、キャンセル、Cooldown、開始／終了通知
- `ActorSkillDefinition` ScriptableObjectによる固定Skill IDと表示名の一元管理
- `DashSkillFeature`、`SwordAttackSkillFeature`、`GuardSkillFeature`、`HealSkillFeature`の初期実装
- `ActorCombatCoordinator`によるHP、被ダメージ倍率、範囲Hit判定の仲介
- `ActorRespawnFeature`による死亡確定、3秒後の生成位置への復帰、HP全回復
- `ActorHealthFeature`、`ActorDamageReceiverFeature`、`ActorHitDetectionFeature`の初期実装
- `ActorCharacterCoordinatorEditor`によるMovement、Skill、Combatの論理階層表示と任意Featureの追加
- 5種類の標準Character PrefabへのCoordinatorおよび初期Featureの適用
- `PlayerSkillInputController`によるLocal PlayerのAttack／Dash／Guard／Heal入力接続
- `NetworkPlayerSkillController`によるOwner入力、Skill発動／Guard解除のServerRpc接続
- Active Skill IndexとActivation SequenceのServer書き込み・全Client読み取り同期
- Strafe中の移動入力方向を使用するDash
- Guard状態に連動するIcosphere Shield、攻撃命中Ring、Scene Depth環境交差表示
- Active Skill Indexを利用したRemote Guard Shield表示
- Input Guide OverlayによるSkillキーのBinding名とライブ入力状態表示

`ActorCharacterCoordinator`が現在提供する主な入口は以下です。

```csharp
characterCoordinator.TryActivateSkill(skillId, direction, target);
characterCoordinator.ResetState();
```

### 未完了・未接続・未確認

- Combat全般のPlay Mode動作確認（Hit判定、Damage、Heal、Guard倍率、死亡状態）
- Network環境でのCombat動作確認とServer Authorityの検証
- Cooldown StateのNetwork同期
- Skill Animation用Triggerを持つAnimator Controller／Animation Clipの制作
- Sword AttackのLight／Heavy／Combo Action
- Guard Counter Action
- Cooldown、Active Duration、Skill固有値の設定アセットへの分離
- Host／Client RPC経路、Server Authority、Skill Featureを含むPlayer Gameplay自動テストの拡充

Local Playerの各Skill入力は`TryActivateSkill(...)`を直接呼び出します。Network PlayerではOwnerだけが入力を取得し、ServerRpcを経由してServer上の同じ入口を呼び出します。HostはRPCを経由せずServer処理を直接実行します。Server入口では方向Vectorの各成分が有限値であることを検証し、非ゼロ方向を正規化してからCoordinatorへ渡します。Player NetcodeのPlayModeテストは、この入口と同じ正規化関数に対してNaN、Infinity、二乗長のOverflow、Zero、通常Vectorを検証します。Guardは入力押下中だけ継続し、入力解放時にLocalまたはServer上のActive Skillをキャンセルします。Unity Editor上でのコンパイル、Prefabロード、Play Mode動作、およびHost／Client RPC統合テストについては継続して確認が必要です。

Player Action Mapには以下のSkill入力を定義しています。

| Action | Keyboard／Mouse | Gamepad |
|---|---|---|
| `Combat/Attack` | Left Click | Button West |
| `Player/Dash` | Left Alt | Button East |
| `Player/Guard` | G | Left Shoulder |
| `Player/Heal` | H | D-pad Down |

## 予定クラス構成

以下をPlayer Gameplayの最終的なクラス構成とします。`[実装済]`は現在存在するクラス、`[予定]`は今後分割・追加するクラスです。

```text
Player
└─ ActorCharacterCoordinator                           [実装済]
   ├─ ActorCompositeMotor : IActorMotorMotionSink     [実装済]
   │  ├─ ActorMotor : IActorMotor                     [実装済]
   │  └─ ActorTraversalCoordinator                     [実装済]
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
   ├─ ActorSkillCoordinator : IActorSkillCoordinator  [実装済]
   │  ├─ ActorSkillFeature : IActorSkillFeature       [実装済・抽象基底]
   │  ├─ DashSkillFeature                               [実装済]
   │  ├─ SwordAttackSkillFeature                        [実装済]
   │  │  ├─ LightAttackAction                           [予定]
   │  │  ├─ HeavyAttackAction                           [予定]
   │  │  └─ ComboAttackAction                           [予定]
   │  ├─ GuardSkillFeature                              [実装済]
   │  │  ├─ GuardShieldVisual                           [実装済]
   │  │  └─ GuardCounterAction                          [予定]
   │  └─ HealSkillFeature                               [実装済]
   │
   └─ ActorCombatCoordinator : IActorCombatCoordinator [実装済]
      ├─ ActorHealthFeature : IActorHealthFeature      [実装済]
      ├─ ActorDamageReceiverFeature                     [実装済]
      └─ ActorHitDetectionFeature                       [実装済]
```

複数の動作を持たないSkillはFeature単体で実装します。Sword AttackやGuard Counterのように具体動作が増えた場合だけ、Skill Feature配下へActionを追加します。Actionを外部から直接発動せず、必ず対応するFeatureを入口にします。

### 入力・Networkを含む予定経路

```text
Local Player
├─ PlayerGameplayInputReader
│  └─ Movement／Traversal入力
└─ PlayerSkillInputController
   └─ ActorCharacterCoordinator.TryActivateSkill(...)

Network Player
├─ Owner
│  └─ NetworkPlayerSkillController
│     ├─ InputActionLeaseによるOwner入力取得             [実装済]
│     └─ Skill発動／Guard解除要求                         [実装済]
└─ Server
   ├─ Skill発動／Guard解除ServerRpc                      [実装済]
   ├─ ActorSkillCoordinator.TryActivate(...)            [実装済]
   └─ Network Skill State                                [一部実装済]
      ├─ ActiveSkillIndex                                [実装済]
      ├─ ActivationSequence                              [実装済]
      └─ Cooldown State                                  [予定]
```

Local／NetworkともSkill Featureを直接呼ばず、`ActorCharacterCoordinator`または`IActorSkillCoordinator`を共通入口にします。NetworkではOwner入力をServerRpcで送り、発動可否、Hit判定、Damage、HealをServer Authorityで確定します。

`ActiveSkillIndex`は`PlayerSkillSlot`によりAttack／Dash／Guard／Healをそれぞれ`0`／`1`／`2`／`3`で表し、非発動時は`-1`です。`ActivationSequence`はServer上でSkill開始のたびに増加し、`LastActivatedSkillIndex`と組み合わせて短時間Skillが同一Network tick内に終了しても一回限りの演出を失わないようにします。すべて全Clientから読み取り可能ですが、書き込みはServerだけが行います。

Local／ServerのSkill開始・終了は`ActorSkillCoordinator`から、Remote ClientはNetwork Skill Stateから、共通の`ActorSkillPresentation`へ通知します。HostではServer側のCoordinator通知だけを使うため二重再生しません。PresentationはAttack／Dash／HealのVFX Graph、Guard Shield、Animatorの任意Trigger／Boolを一元管理します。現在の標準Animator ControllerにはSkill用Parameter／Clipがないため、Animationは対応Controllerへ`Attack`、`Dash`、`Guard`、`Heal`を追加した場合に再生されます。

Dash TrailはDash本体の0.2秒で新規発生を停止し、0.42秒でEffect Objectを破棄します。移動終了後に長いTrailが残らないよう、発生時間と残像時間を分けて管理します。

`NetworkActorCombatState`は`IActorCombatProcessGate`としてNetwork CombatのDamage、Heal、Hit DetectionをSpawn済みServerだけに制限し、`ActorHealthFeature.CurrentHealth`をServer-writeのNetworkVariableで全Clientへ同期します。Clientへの反映は`ActorHealthFeature.ApplyReplicatedHealth`を通して`HealthChanged`通知を維持します。Local CharacterにはGateを配置せず、従来どおり共通Combatを直接処理します。

HPが0になると`ActorRespawnFeature`が死亡を確定し、進行中SkillとMotor状態を解除してRigidbodyを停止します。3秒後に生成時の位置・向きへ戻し、HPを最大まで回復して制御を再開します。Network CharacterではServerだけがタイマー、座標復帰、HP回復を実行し、既存のNetwork TransformとHP NetworkVariableを通してClientへ反映します。Local／NetworkのPlayerとNPCは`CharacterAgentCore`から同じ機能を継承します。

生成位置の記録はフレーム待ちに依存しません。`PlayerSpawnService.Place`がTransform／RigidbodyへのPose適用と`Physics.SyncTransforms`を完了した直後、`ISpawnPoseAppliedReceiver.OnSpawnPoseApplied`で`ActorRespawnFeature`へ通知します。NPCのようにSpawn Serviceを経由せず指定PoseでInstantiateされるCharacterはAwake時のPoseを初期値として保持します。

`ActorDeathPresentation`は生存状態の変化を購読し、死亡時だけ各Character Rendererの元Materialを保存して共通Dissolve Shaderへ差し替えます。元MaterialのBase Map、Base Color、UV Scale／Offsetを引き継ぎ、発光境界を伴って1秒で完全に消失した直後に光粒子のVFX Graphを再生します。死亡VFXはリスポーン前に終了し、Pose適用完了コールバックを受けた場合は残存Effect Objectも明示的に破棄します。リスポーン時は固定時間を待たず、LocalではPose適用直後の`RespawnPresentationReady`、NetworkではServer同期されたRespawn PoseをClientが適用した直後の同コールバックで元Materialを復元します。これによりHealthとNetworkTransformの到着順に依存せず、死亡地点で再表示されません。

### 設定クラスの予定

```text
ActorSkillDefinition                                   [実装済]
├─ Id
└─ Display Name

PlayerSkillSettings                                     [予定]
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

固定IDと表示名は`ActorSkillDefinition`へ分離済みです。Cooldown、Active Duration、Damageなどの動作設定は現在も各Feature内のSerializeFieldに保持しています。複数Characterや装備間で設定を共有する段階で、上記の設定クラスまたはScriptableObjectへ分離します。

## 標準Character Prefab

`Runtime/Prefabs/Character/CharacterAgentCore.prefab`を共通基底とし、以下の4つをPrefab Variantとして提供します。

- `LocalPlayer`
- `LocalNPC`
- `NetworkPlayer`
- `NetworkNPC`

`CharacterAgentCore`はRigidbody、Collider、Motor、共通Traversal Coordinator、Skill／Combat、共通Targetingを保持します。Local／NetworkおよびPlayer／NPC固有のController、入力、NavMesh、所有権、同期Componentは各Variantの追加Componentとして保持します。Coreは継承専用でSceneへ配置せず、Network Prefab Listにも登録しません。

VariantではCore Componentを削除または無効化せず、固有Componentの追加と必要最小限のProperty Overrideだけを行います。Inspectorの設定補助は、同じGameObject上のControllerとSkill Featureから参照を取得します。

## 現在の基本構成

```text
ActorCharacterCoordinator
├─ ActorCompositeMotor
│  ├─ ActorMotor
│  └─ ActorTraversalCoordinator
│     ├─ WallTraversalFeature
│     ├─ LadderTraversalFeature
│     └─ WireTraversalFeature
├─ ActorSkillCoordinator
│  ├─ DashSkillFeature
│  ├─ SwordAttackSkillFeature
│  ├─ GuardSkillFeature
│  └─ HealSkillFeature
└─ ActorCombatCoordinator
   ├─ ActorHealthFeature
   ├─ ActorDamageReceiverFeature
   └─ ActorHitDetectionFeature
```

## 責務

- `ActorCharacterCoordinator`は3領域への共通アクセスポイントです。
- `ActorCompositeMotor`は通常移動、Traversal、外部モーション要求を処理します。
- `ActorSkillCoordinator`は装着済みSkillの検索、排他実行、キャンセルを処理します。
- `ActorCombatCoordinator`はHP、被ダメージ倍率、範囲Hit判定を仲介します。
- `Feature`はPlayerへ個別に付け外しできる機能単位です。
- `Action`はTraversal Feature内部の具体動作に使用します。

## Skillの呼び出し

`PlayerSkillInputBindings`はLocal／Network Playerで共通の`InputActionsConfig`からAttack、Dash、Guard、Healを解決し、`InputActionBinding`の購読とLeaseのライフタイムを一元管理します。Action Pathはこの共通Bindingが本番Input Actionsの固定契約として保持し、Prefabごとの文字列設定にはしません。Local Controllerは入力をCoordinatorへ直接渡し、Network ControllerはOwner入力をServer RPCへ渡す配送責務だけを個別に持ちます。

入力およびNetworkコードは具象Skillを直接操作せず、次の共通入口を使用します。

```csharp
characterCoordinator.TryActivateSkill(dashSkillDefinition, moveDirection);
characterCoordinator.TryActivateSkill(swordAttackSkillDefinition, aimDirection);
```

各Featureと入力Bindingは同じ`ActorSkillDefinition`を参照します。Definitionには変更されない固定IDと表示名を保持し、Definition未設定またはIDが空のSkillは発動できません。初期Skillには`skill.dash`、`skill.sword_attack`、`skill.guard`、`skill.heal`を割り当てています。Network通信やセーブデータではSO参照ではなく、この固定IDを使用します。

## 依存方向

```text
Input / Network
    ↓
ActorCharacterCoordinator
    ↓
ActorSkillCoordinator
    ↓
SkillFeature
    ├─→ IActorMotorMotionSink
    └─→ IActorCombatCoordinator
```

Dashは`Rigidbody`を直接更新せず、`ActorCompositeMotor`へ期限付きモーションを要求します。通常時は入力Controllerから渡された基準方向を使用し、Strafe中に移動入力がある場合は`IActorLocomotionState.MoveDirection`を優先します。これにより、注視方向を維持したまま前後左右および斜めへDashできます。攻撃、Guard、Healは`ActorCombatCoordinator`を経由します。

## 初期Skill

| Feature | 動作 |
|---|---|
| `DashSkillFeature` | 指定方向、またはStrafe中の移動方向へ期限付きモーションを要求する |
| `SwordAttackSkillFeature` | 前方の範囲内へ一度だけダメージを与える |
| `GuardSkillFeature` | 発動中の被ダメージ倍率を下げ、`GuardShieldVisual`を表示する |
| `HealSkillFeature` | 生存中かつHPが減っている場合に回復する |

各Skillには共通してSkill ID、Cooldown、Active Durationがあります。Dashだけは固有のDurationをモーション時間にも使用します。

## Guard表示

`GuardShieldVisual`は実行時にIcosphereを生成し、`Koiusa/Effects/GuardShield` Shaderで半透明膜、リム、均一な格子、Pulseを描画します。表示ObjectはPlayerの`Presentation`配下へ配置し、Character Modelと同じ補間座標を使用します。

Shield中心はCharacter ModelのRenderer boundsからGuard開始時に一度だけ計算します。直前のAttack／Dash／Heal VFX Rendererはbounds対象外とし、Skill Effectの位置へShield中心が引かれないようにします。

- Guard開始／終了時は中心から拡縮しながらFadeする
- 攻撃命中は`ActorDamageRequest.Point`を中心とするRingで表示する
- 環境との交差はURP Scene Depthを比較して表示する
- 環境交差のためPC／Mobile双方のURP AssetでDepth Textureを有効にする
- Network Playerでは`PlayerSkillSlot.Guard`の同期状態を全Clientへ反映する
- Attack／Dash／Healの一回限りのEffectは`ActivationSequence`でRemote Clientへ反映する

> [!CAUTION]
> Combat関連クラスとPrefab設定は存在しますが、現時点では動作未確認です。この節の動作説明はコード上の意図を示すもので、検証済み仕様ではありません。

## Player表示名

Player表示名は各Playerの`Presentation`配下にあるWorld Space `UIDocument`で描画します。位置はPlayerの表示補間Transformから継承し、スクリーン座標への変換やPlayer一覧走査を`Update`／`LateUpdate`で行いません。

表示名とHPゲージは別GameObject・別`UIDocument`・別Presenterとして管理します。`PlayerNameOverlayUiDocument`は表示名だけ、`ActorHealthOverlayUiDocument`はHPだけを所有し、表示位置や有効状態を個別に変更できます。カメラ正対、距離Fade、画面上のサイズ維持は共通の`PlayerWorldSpaceOverlay`を各Objectで再利用します。HP Presenterは`ActorHealthFeature.HealthChanged`を購読してDamage／Heal／Network同期時だけFill幅、赤から緑への残量色、数値を更新し、HP値を毎フレーム監視しません。

HPの表示先は`ActorHealthUiRouter`が所有権とCharacter種別に応じて切り替えます。Local Ownerは画面左下の固定HUD、Remote Playerは頭上ゲージを常時表示します。Local／Network NPCは頭上ゲージを通常は隠し、被ダメージ時に3秒間表示します。表示中に再度ダメージを受けた場合は表示時間を3秒へ延長し、回復だけでは表示しません。HPが0になった通知では全表示先を即座に隠し、リスポーン後はPlayerだけ通常の表示先へ戻します。

表示名の変更は`IPlayerDisplayNameNotifier.DisplayNameChanged`で通知します。カメラ正対と距離Fadeだけは、実際にCameraが描画される直前のRender Pipelineコールバックで更新します。Player表示名専用MaterialはDepth Testを無効化し、World Spaceの距離表現を保ったままシーンObjectより手前へ描画します。
