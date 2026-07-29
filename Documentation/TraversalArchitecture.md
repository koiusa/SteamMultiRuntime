# Traversal Architecture

この文書は、SteamMultiRuntimeのプレイヤー移動およびTraversal機能の現在のクラス構成をまとめた設計資料です。
READMEとは分離し、実装変更時にクラスの責務と依存方向を確認する目的で使用します。

プロジェクト全体のクラス配置は[CurrentClassStructure.md](CurrentClassStructure.md)、InspectorとRepair操作は[EditorSpecification.md](EditorSpecification.md)を参照してください。この文書をTraversalの詳細仕様の正本とします。

## 全体構成

Traversal関連コンポーネントは、同じPlayer GameObjectへ配置します。
以下の階層はTransformの親子関係ではなく、責務とInspector上の論理的な所有関係を表します。

```text
Controller
└─ PlayerCompositeMotor
   ├─ PlayerMotor
   └─ PlayerTraversalCoordinator
      ├─ WallTraversalFeature
      │  ├─ WallRunAction
      │  ├─ WallSlideAction
      │  └─ WallJumpAction
      ├─ LadderTraversalFeature
      │  ├─ LadderClimbAction
      │  └─ LadderDetachAction
      └─ WireTraversalFeature
         ├─ WireAttachAction
         ├─ WireSwingAction
         ├─ WireReelAction
         └─ WireGroundAction
```

補助コンポーネントは次のとおりです。

```text
WallTraversalFeature
└─ SlopeContactResolver

LadderTraversalFeature
└─ LadderVolume（シーン側のTrigger）

WireTraversalFeature
├─ WireGrappleTargetingFeature
└─ WireLineVisualFeature
```

## 命名と責務の規則

### Feature

Featureは、同じTraversalに属するActionが共有する状態や環境情報を管理します。

- 入力を直接読み取らない
- 接触先、接続先、現在状態を保持する
- Actionへ共有情報をインターフェース経由で提供する
- 個別操作の入力判断をできるだけ持たない

### Action

Actionは、入力やCoordinatorからの要求に対応する具体的な動作を担当します。

- 入力結果を受け取る
- 速度、加速度、拘束、離脱などを処理する
- Input Systemの`InputAction`を直接参照しない
- Featureまたは能力インターフェースを利用する

### Coordinator

`PlayerTraversalCoordinator`は、Traversal間の排他制御と状態遷移を担当します。

- Wall、Ladder、Wireの優先順位を決める
- Controllerから入力済みの値を受け取る
- 各Actionをインターフェース経由で呼び出す
- 個別Traversalの物理計算を持たない
- AnimatorやControllerへ現在状態を公開する

## 入力から物理処理まで

```text
InputActionsConfig
  ↓
PlayerGameplayInputReader
  ↓ PlayerInputState
LocalPlayerController / ServerDrivenPlayerController
  ↓
PlayerCompositeMotor
  ├─ PlayerMotor
  └─ PlayerTraversalCoordinator
       ↓
     各Traversal Action
```

`PlayerGameplayInputReader`だけがInput Systemを扱います。Traversal FeatureとActionは`InputAction`へ依存しません。

LocalではControllerが入力を直接Physics Tickへ渡します。NetworkではOwnerが`PlayerInputSyncState`を送信し、Server上のCoordinatorとActionが物理処理を実行します。

## Core Motor

### PlayerCompositeMotor

プレイヤー移動の外部窓口です。

- `PlayerMotor`と`PlayerTraversalCoordinator`を統合する
- Controllerが個別Traversal実装へ依存しないようにする
- Strafe設定や移動入力を基礎Motorへ転送する

### PlayerMotor

通常の地上移動、空中移動、ジャンプ、回転を担当します。

- 接地判定と通常移動
- Ground、Air、Steep Slopeの加速
- 通常StrafeとWire Ground Strafeのブレンド
- Wire Swing中の通常空中加速抑制
- WallRun中の向き維持

### PlayerTraversalCoordinator

現在状態は`PlayerTraversalState`で公開します。

```text
Grounded
Airborne
WallRun
WallSlide
Ladder
WallJump
WireSwing
Cooldown
```

主な公開契約は`IPlayerTraversalCoordinator`です。

`Tools > SteamMultiRuntime > Debug > Player Movement Debugger`から専用のEditorWindowを開けます。実行中の
`PlayerCompositeMotor`を対象に、Composite MotorとBase Motor、その配下の`PlayerTraversalCoordinator`の
State、Intent、Wall制限、Wire照準結果と、Wall／Ladder／Wireの
各FeatureおよびActionの状態をまとめて監視します。診断値はFeatureごとのinternalな
`GetDebugSnapshot()`で一括取得します。`InternalsVisibleTo`でlocomoterのEditor assemblyだけに公開し、
Gameplayのpublic APIを増やさず、Editor側からprivate実装も探索しません。
Windowは具体Componentを直接列挙せず、Editor側の`IPlayerMovementDebugTarget`を受け取ります。
`PlayerMovementDebugTarget` AdapterがComponent参照の解決とSnapshot取得を担当し、構成変更時はWindow上部の
`Refresh`で参照を再構築できます。
Coordinator全体ビューの`Console Log`を有効にすると、そのPlayerのState遷移だけを、遷移前の滞在時間と
現在Intent付きでUnity Consoleへ出力します。ログ操作もEditor assemblyに限定し、既定は無効です。
Ladder専用Intentは、Wire未接続かつ実際にLadderへ接続中の場合だけ生成します。State遷移ログには
`grounded`、`wire`、`ladder`の実状態も併記します。
Wireビューでは、権威側Actionが保持するReel Inputと、直近の巻き取り適用前後のTarget Rope Lengthも
`WireReelDebugSnapshot`から確認できます。

## Wall Traversal

### WallTraversalFeature

Wall Action共通の壁接触解決を担当します。

- `SlopeContactResolver`から障害物法線を取得する
- Actionごとの`WallMaxUpDot`条件を受け取って壁法線を返す
- `IWallTraversalFeature`を実装する

### WallRunAction

- WallRun開始・維持速度を判定する
- 壁方向の加速を計算する
- Arc、Maintain Height、Gravityの縦移動を処理する
- 入力解放猶予とWallRun継続状態を保持する
- `IWallRunAction`と`ITraversalSettingsSync`を実装する

### WallSlideAction

- WallSlide開始条件を判定する
- 落下速度を制限する
- 壁から離れる入力を処理する
- 横移動許可オプションを適用する
- `IWallSlideAction`と`ITraversalSettingsSync`を実装する

### WallJumpAction

- 壁法線からジャンプ方向を計算する
- Arc／Triangle Kickの軌道を生成する
- 同じ壁への連続Kickを一時的に制限する
- `IWallJumpAction`と`ITraversalSettingsSync`を実装する

## Ladder Traversal

### LadderTraversalFeature

梯子との接続状態と共有設定を管理します。

- `LadderVolume`からEnter／Exit通知を受け取る
- 現在の梯子と重複中の梯子を保持する
- 重力の有効・無効を管理する
- 再接続禁止時間を保持する
- `ILadderTraversalFeature`を実装する

### LadderClimbAction

- 上下入力を梯子方向の速度へ変換する
- 梯子面への吸着と横方向移動を適用する
- `ILadderClimbAction`を実装する

### LadderDetachAction

- Jumpによる離脱を処理する
- 横入力、梯子端、地上到達による離脱を処理する
- 離脱後の再接続猶予を適用する
- `ILadderDetachAction`を実装する

## Wire Traversal

### WireTraversalFeature

Wire機能全体の中心コンポーネントであり、接続状態とロープ制約を管理します。

- アンカー位置とアンカーRigidbodyを保持する
- Target Rope Lengthと実距離を管理する
- Rope／Elastic制約を適用する
- 遠距離アタッチ後の自動巻き取りを処理する
- `IWireConnection`を実装する

クラス名は機能全体を表す`WireTraversalFeature`ですが、Actionへ提供する能力は接続契約であるため、インターフェース名は`IWireConnection`を維持します。

### WireAttachAction

- Grapple入力の押下と解放を処理する
- Coordinatorが評価済みの`WireAimResult`を受け取る
- `Valid`と`Obstructed`の候補を、それぞれの実際のRaycast命中点へ接続する
- アタッチとデタッチを実行する

Grapple入力を保持している間、Wire未接続時は照準操作を優先するためCamera入力を停止します。`WireAttachAction`によって接続が成立するとCamera入力を再開し、Grapple入力を保持して接続を維持したまま視点を操作できます。Grapple入力を解放するとWireを切断し、Cameraは通常入力を継続します。Camera側の詳細は[CameraArchitecture.md](CameraArchitecture.md#camera入力)を参照してください。

### WireSwingAction

- 空中Swing中の接線方向加速を処理する
- 上限速度付近で加速を二次曲線的に弱める
- Ground Action中はSwing加速を停止する

### WireReelAction

- Q／負軸入力で巻き取る
- E／正軸入力で繰り出す
- 入力値の反映は独立した`FixedUpdate`ではなく、`PlayerTraversalCoordinator.ApplyTraversal`から呼ばれる権威側の物理Tickで行う
- Wire接続中の空中ジャンプ入力は、現在の実距離まで余剰Slackを除去してから1ステップ巻き取る
- 巻き取り中はElasticでも伸びない制約を要求する
- GroundからJumpした後も現在距離から巻き取る

### WireGroundAction

- Dynamic Rigidbody接続時は物理オブジェクトを振り回す
- 環境接続時はWire Ground Strafeを有効にする
- Ground Jumpではアタッチを解除しない
- 通常移動からStrafe移動へのブレンドを管理する
- アンカー方向へのFacingブレンドを独立して管理する

既定のブレンド時間は次のとおりです。

```text
Strafe Blend Damping: 0.30 sec
Facing Blend Damping: 0.12 sec
```

### WireGrappleTargetingFeature

- 照準原点からポインタが示す地点までRaycastし、手前側の最初の非Owner Colliderを評価する
- アタッチ可能距離を管理する
- `grappleLayers`外のColliderが最初に当たった場合は無効とする
- ポインタ地点より手前の接続可能Colliderは`Obstructed`、地点付近の接続可能Colliderは`Valid`とする
- Wire最大長とは独立している

照準評価は`WireAimResult`へ集約されます。

```text
PlayerPointerAim
  ↓ requested target point
PlayerTraversalCoordinator
  ↓ WireGrappleTargetingFeature.EvaluateTarget
WireAimResult
  ├─ Invalid: 接続不可
  ├─ Obstructed: ポインタ地点の手前へ接続可能
  └─ Valid: ポインタ地点へ接続可能
       ↓
WireAimCursorOverlay / WireAttachAction
```

`WireAimResult`は表示と発射で共有し、`RequestedPoint`、実際の`AttachPoint`、`AnchorTransform`を保持します。これにより、カーソル表示時と発射時で別々の判定結果を使うことを避けています。照準地点が変化した場合は、発射処理の直前にも再評価します。

`WireAimCursorOverlay`の既定表示は、`Valid`がシアンの十字、`Obstructed`がオレンジの斜線付きダイヤ、`Invalid`が赤いダイヤです。`Obstructed`も`CanAttach`であり、画面上のポインタ地点ではなく手前の障害物へ接続します。

### WireLineVisualFeature

- Wireの始点とアンカー間を描画する
- 接続物理や入力は扱わない

## Wire距離設定

アタッチ射程と維持可能なWire長は別の設定です。

```text
Attach Maximum Range: 45 m
Maximum Rope Length: 20 m
Minimum Rope Length: 2 m
```

20mより遠い対象にもアタッチでき、超過分は段階的に自動巻き取りされます。

## Animator

`PlayerAnimatorStateDriver`がMotorとCoordinatorの状態をAnimatorパラメータへ変換します。

接地Locomotionの`Speed`と`MotionSpeed`には水平速度だけでなく、水平速度と鉛直速度の合成値を使用します。

```text
Animation Move Speed = sqrt(HorizontalVelocity² + VerticalVelocity²)
```

この値は斜面に沿った実移動距離を反映するため、上り坂／下り坂でAnimation再生速度が遅くなることを防ぎます。空中では従来どおり水平速度を使用します。Gameplay上の`HorizontalVelocity`定義は変更しません。

`PlayerLocomotionAnimationMode`は次の値を使用します。

```text
Grounded = 0
Airborne = 1
Ladder = 2
WallRun = 3
WireSwing = 4
```

`WireSwing`は独立したAnimator Stateです。現在は`InAir`と同じAnimationClipを参照しており、専用Clip追加後は`WireSwing` StateのMotionだけを差し替えます。

## 物理表示補間

PlayerとNPCは同じ表示補間方式を使用します。

```text
Physics Root       Rigidbody interpolation = None
└─ Presentation   PhysicsPresentationSmootherで描画Frame間を補間
```

Motor、Collider、Network同期は補間前のPhysics Rootを参照します。Character Model、Camera Marker、World UI、Guard Shieldなどの表示Objectは`Presentation`配下に置きます。Rigidbody補間と`PhysicsPresentationSmoother`を同時使用して二重補間しないでください。Remote Network Objectは`NetworkTransform`の補間を使用します。

## 移動床

`PrototypeMotionMover`はPhysics tickでCollider Transformを更新し、子の`Presentation`だけを描画Frame間で補間します。Player／NPCの床追従は物理押し出しではなく`IGroundMotionSource`の変位を`PlayerMotor`が一度だけ適用します。

`IGroundMotionSnapshotSource`対応床は、前回／現在の移動行列、逆行列、回転差をPhysics tickごとに一度だけキャッシュします。`GroundMotionTracker`は速度、変位、回転を個別に再計算せず、1回のSnapshot取得で受け取ります。キャッシュは呼び出し時の`Time.fixedTime`で更新するため、Script Execution Orderには依存しません。

## Netcode

Networkプレイヤーの物理処理はServer Authorityです。

- Ownerが入力を読み取る
- `PlayerInputSyncState`でServerへ送信する
- Serverの`PlayerTraversalCoordinator`と各Actionが物理処理を行う
- Wire接続状態とロープ長をClientへ同期する
- Traversal設定は`ITraversalSettingsSync`から収集・適用する

巻き取り入力の変化は定期送信を待たず、入力変化時にも送信されます。

## InspectorとPrefab

FeatureとActionは子GameObjectへ分けず、Playerと同じGameObjectへ配置します。

Custom Inspectorの論理階層、Add／Repair操作の詳細は[EditorSpecification.md](EditorSpecification.md)に集約しています。

主要Player Prefabは次の構成へ対応しています。

- `LocalPlayer_WithAnimator`
- `NetworkPlayer_WithAnimator`
- `LocalPlayer_NPC`
- `NetworkPlayer_Runtime`

## 依存方向

許可する依存方向は次のとおりです。

```text
Controller
  → IPlayerCompositeMotor / IPlayerTraversalCoordinator

Coordinator
  → Feature・Actionのインターフェース

Action
  → Featureまたは能力インターフェース

Feature
  → 接触Resolver、Volume、Rigidbodyなどの基盤コンポーネント
```

避ける依存は次のとおりです。

- Controllerから具体的なActionクラスを直接操作する
- FeatureやActionがInput Systemを直接読む
- Action同士が具体クラスで相互参照する
- Animator Driverが入力状態からTraversalを再判定する
- Client側でServer AuthorityのTraversal物理を確定する

## 変更時の確認項目

新しいTraversalまたはActionを追加するときは、次を確認します。

1. 共有状態はFeatureに置かれているか
2. 入力に対応する動作はActionに置かれているか
3. Coordinatorはインターフェース経由で利用しているか
4. LocalとNetworkの両方に入力経路があるか
5. Server Authorityを維持しているか
6. Animator StateとNetworkAnimatorパラメータが必要か
7. Local／Network／NPC Prefabへコンポーネントを追加したか
8. Custom Inspectorの階層表示とRepair処理を更新したか
9. 既存スクリプトを改名する場合はmeta GUIDを維持したか
