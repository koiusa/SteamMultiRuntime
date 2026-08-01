# Development Notes

## Steam Inputとテスト用App ID

Steamネットワークテストでテスト用App ID（480 / Spacewar）を使うと、Steam Inputの設定によってゲームパッド入力がUnity Input Systemへ届かなくなる場合があります。

今回の環境では正規のゲームApp IDに切り替えると解消しました。ネットワーク開始後にゲームパッドだけ反応しなくなる場合は、InputGuideOverlayや通信処理より先に、使用中のApp IDとSteam Input設定を確認してください。

## 大規模NPC Crowd化

NPC 1000体表示を目標とし、Crowd Backendを標準経路とする。現状の実用目安は200体で、Crowd OFFは互換性確認とデバッグ用であり、大規模運用向けではない。

- Local NPCとServer Network NPCは、移動、重力、接地、壁接触、移動床を`NpcCrowdSimulation`と`NpcCrowdMotor`で一括処理する。
- Boid／RVO近傍計算はSpatial GridとBurst Jobを使用する。
- Network ClientはCrowd Simulationを実行せず、Serverの同期結果を表示する。
- AI判断、FutureAction、Coordinator、疑似入力はCrowd ON／OFFで共通とする。
- Playerは従来Motorを使用し、NPC Crowd Motorとは分離する。

### 大量スポーン時の経路制御

大量NPCではUnityのNavMesh経路計算キューが飽和し、経路計算待ちのNPCが停止していた。Crowdでは`NavMeshAgent`を経路探索専用とし、個別Obstacle Avoidance／Stuck再経路／`autoRepath`を使用しない。経路計算予算は登録NPC数に応じて調整し、再経路計算中も直前の進行方向を維持する。

### Network Crowd計測（2026-08-02）

個体別の空振りCallbackを中央Schedulerまたはpush通知へ移した後のNetwork NPC、Crowd ON計測。`NetworkRigidbody`は標準構成を維持し、`NetworkTransform`のPosition Thresholdは0.5m、Y Rotation Thresholdは6度とした。

実行条件はUnity 6000.3.9f1、`ServerScene`、Network Server、Crowd ON、100／200／300体、Warmup 180 frame、Sample 300 frame、乱数Seed 481516、Subsystem Recorder ON、`-batchmode -nographics`である。ヘッドレス実行のためGPU Frame、Render Thread、Draw Callは評価対象外とする。

| NPC | Frame平均 | P95 | FPS | Main Thread | Fixed steps/frame | 移動中 |
|---:|---:|---:|---:|---:|---:|---:|
| 100 | 10.028 ms | 19.290 ms | 99.7 | 10.005 ms | 0.50 | 66 / 100 |
| 200 | 27.341 ms | 50.758 ms | 36.6 | 27.297 ms | 1.34 | 160 / 200 |
| 300 | 46.791 ms | 74.358 ms | 21.4 | 46.758 ms | 2.32 | 231 / 300 |

モデル生成後に再有効化されていた`AutoBlinkforSD`など、揺れもの以外の不要Componentは型付きの一回限りガードで停止する。Spring Manager停止時には300体8.400msまで低下したが、揺れものが動作しないためこの構成は採用しない。

300体の主要Markerは次の通り。

| Marker | 平均時間 |
|---|---:|
| `LateBehaviourUpdate` | 18.844 ms |
| `BehaviourUpdate` | 10.636 ms |
| `Physics.NpcCrowd.PrepareProbes` | 3.990 ms |
| `MeshSkinning.Skin` | 2.999 ms |
| `FixedUpdate.PhysicsFixedUpdate` | 2.577 ms |
| `Physics.SyncColliderTransformBatchJob` | 2.309 ms |

Spring Managerを停止すると300体8.400msまで低下するが、揺れものが動作しないため採用しない。採用構成では`LateBehaviourUpdate`が主要負荷として残る。

### Spring Bone Burst化（2026-08-02）

NPCモデルのUTJ／Legacy UnityChan Spring Boneを`NpcCrowdSpringSimulation`へ登録し、元の個体別Manager／Bone更新を中央のBurst Jobへ置き換えた。親と子を同時に更新すると姿勢が崩れるため、Transform階層の深さごとにJobを直列依存させ、同一深さのNPC／Branchを並列処理する。登録とComponent制御はすべて型付きで行い、Reflectionは使用しない。

同じ条件で再計測した結果は次の通り。

| NPC | Frame平均 | P95 | FPS | Main Thread | Fixed steps/frame |
|---:|---:|---:|---:|---:|---:|
| 100 | 10.126 ms | 19.456 ms | 98.8 | 10.101 ms | 0.51 |
| 200 | 25.299 ms | 46.391 ms | 39.5 | 25.249 ms | 1.26 |
| 300 | 44.531 ms | 71.657 ms | 22.5 | 44.498 ms | 2.22 |

300体では従来構成の46.791msから44.531msへ約4.8%改善した。

続いて階層深度ごとのTransform Jobを廃止し、全姿勢の一括Snapshot、NPC Rig単位の親→子Burst演算、全回転の一括Applyへ変更した。中間Transformを含むチェーンは、直近Spring親のアニメーション姿勢と解決済み姿勢の差分からワールド姿勢を再構築する。Job同期点はSnapshot後とApply前に集約した。

| NPC | Frame平均 | P95 | FPS | Main Thread | Fixed steps/frame |
|---:|---:|---:|---:|---:|---:|
| 100 | 9.818 ms | 19.582 ms | 101.9 | 9.788 ms | 0.49 |
| 200 | 24.805 ms | 44.342 ms | 40.3 | 24.791 ms | 1.23 |
| 300 | 44.251 ms | 71.179 ms | 22.6 | 44.216 ms | 2.21 |

深度別Burst版の300体44.531msから44.251msへ追加で約0.6%改善した。`LateBehaviourUpdate`は18.816msで計測揺れの範囲に留まり、残りはSnapshot／Applyそのもの、Animator、または計測Marker内の別処理を分離確認する必要がある。ヘッドレス計測では揺れものの見た目を判定できないため、各採用モデルのAngle／Length Limit、Ground Collisionを含む目視確認を別途行う。

Spring Colliderだけを停止した通常パイプラインは300体44.322msで、Collider ONの44.251msとの差は計測揺れの範囲だった。診断用に各段階を強制同期した場合もCollider停止による改善は約0.79msに留まる。したがってSpring Colliderは約19msの主要因ではなく、表示互換性を落としてまで一律停止しない。

同じChain Burst Solver、Network NPC、Spring Collider ONでCrowd OFFも計測した。

| NPC | Frame平均 | P95 | FPS | Main Thread | Fixed steps/frame |
|---:|---:|---:|---:|---:|---:|
| 100 | 14.023 ms | 25.093 ms | 71.3 | 13.998 ms | 0.70 |
| 200 | 29.799 ms | 49.857 ms | 33.6 | 29.746 ms | 1.49 |
| 300 | 60.813 ms | 97.524 ms | 16.4 | 60.771 ms | 3.02 |

Crowd OFF 300体の主要Markerは`LateBehaviourUpdate` 19.037ms、`FixedUpdate.PhysicsFixedUpdate` 11.614ms、`Physics.SendContactEvents` 7.462ms、`BehaviourUpdate` 4.811ms、`MeshSkinning.Skin` 2.919msだった。Crowd ON/OFFでLateUpdateが約19msのため揺れもの／Animator後処理は共通負荷であり、OFF固有の追加負荷はDynamic RigidbodyのFixed Physicsと床接触である。

### Spring Job非同期化とAnimator連携（2026-08-02）

Spring JobはScheduleしたフレームに完了待ちせず、次フレームで完了済みの場合だけ結果を適用する。未完了時は前回姿勢を維持してメインスレッドを待機させない。Animator Schedulerが状態更新したRigだけをSpring対象とし、対象RigのボーンだけをSnapshot／Applyする。更新頻度は近距離30Hz、中距離15Hz、遠距離5Hzとした。

最終構成のNetwork NPC計測は次の通り。

| Backend | NPC | Frame平均 | P95 | FPS | Main Thread | Fixed steps/frame |
|---|---:|---:|---:|---:|---:|---:|
| Crowd ON | 100 | 10.552 ms | 19.715 ms | 94.8 | 10.534 ms | 0.53 |
| Crowd ON | 200 | 23.904 ms | 45.348 ms | 41.8 | 23.876 ms | 1.20 |
| Crowd ON | 300 | 43.124 ms | 70.446 ms | 23.2 | 43.094 ms | 2.16 |
| Crowd OFF | 100 | 11.558 ms | 21.029 ms | 86.5 | 11.519 ms | 0.58 |
| Crowd OFF | 200 | 29.614 ms | 48.132 ms | 33.8 | 29.596 ms | 1.48 |
| Crowd OFF | 300 | 55.326 ms | 77.875 ms | 18.1 | 55.335 ms | 2.75 |

同期Chain版に対してCrowd ON 300体は44.251msから43.124ms、Crowd OFF 300体は60.813msから55.326msへ改善した。Crowd OFFではFrame短縮によりFixed steps/frameも3.02から2.75へ下がり、Physics catch-upの増幅も抑えられた。`LateBehaviourUpdate`はCrowd ON 18.396ms、Crowd OFF 18.826msで依然として最大負荷であり、残りはTransform適用後の階層再評価やAnimator／Skinning側にある。

0.25度未満の回転Applyを省略する案も比較したが、Crowd ON 300体は43.124msから47.696msへ悪化した。省略できるボーンが少ない一方、全対象ボーンの現在`localRotation`読取と差分判定が増えるため採用しない。

Spring階層からChainを構築し、中距離で長いChainの半数だけ更新、遠距離で停止する案も比較した。近距離Fast Pathを追加した最終比較でもCrowd ON 300体は43.124msから45.586msへ悪化したため採用しない。Chain選択と共有親の重複除外コストに対して、このベンチ配置で削減できる中・遠距離ボーンが不足していた。

結果ApplyをRig単位で1フレーム最大64体へ分散する案も比較したが、Crowd ON 300体は43.124msから46.413ms、P95は70.446msから74.013msへ悪化した。Apply完了まで次のSpring更新を保留する構成では更新待ちが増え、負荷平準化の効果を得られないため採用しない。

NPC補助状態の3個の`NetworkVariable`について、代入前に明示的な同値比較を行う案も比較した。Crowd ON 300体は43.967ms、P95 71.500ms、`BehaviourUpdate` 9.614msで、基準の43.124ms、70.446ms、9.333msから改善しなかった。同期周期は既に5Hzで、Netcode側も値の変化を判定するため、比較処理の重複となる実装は採用しない。

近距離／中距離Animatorも更新予定フレームだけ有効化し、`Animator.Update`による明示評価後に無効化する案を比較した。Crowd ON 100体は10.552msから9.620msへ改善した一方、300体は45.209ms、P95 73.349msへ悪化し、`LateBehaviourUpdate`も18.583msのままだった。300体時は約22FPSのため近距離30Hzの期限を毎フレーム超え、評価を間引けず有効／無効切替だけが追加される。全距離への明示評価は採用せず、遠距離だけの既存方式を維持する。

通常の非同期Spring経路へ診断時間計測を入れたところ、中央`NpcCrowdSpringSimulation.LateUpdate`は300体で0.001ms/frame、結果ApplyはSample 300 frame中0回だった。100／200／300体のLateUpdate呼出回数は300／600／900回で、Runを跨いで`DontDestroyOnLoad` Instanceが残っている。したがって約19msの`LateBehaviourUpdate`は中央Burst SolverのApplyではなく、現状はモデルRigが中央Solverへ有効登録されていない。モデル生成通知の順序、UTJ `SpringManager.Awake`前のBone収集、元Manager／Boneの有効状態を次に確認する。

続けて型付きComponent列挙で実動作経路を確認した。100／200／300体の全Runで中央Simulation、Rig、登録Boneはいずれも0だった。一方300体ではUTJ Manager 149個（`automaticUpdates` 149）、UTJ Bone 3,502本、Legacy Manager 85個、Legacy Bone 2,295本が存在し、Manager／Boneはすべて有効だった。約19msは元のUTJ／Legacy Spring実装が個別更新している負荷であり、中央化処理はモデル生成経路へ接続されていない。次は`NpcCrowdModelPresentation.Configure`の通知購読順序を修正し、生成済みモデルを型付きで明示登録してから再計測する。

Network NPCでは`CharacterPrefabLoader`がPrefabに存在せず`OnNetworkSpawn`で動的追加されるため、NPC `Awake`時の個別購読がモデル生成通知を取りこぼしていた。Loaderの共通生成通知を中央Solverが1回だけ購読し、事前登録済みNPC Rootに属するモデルだけを処理するpush経路へ変更した。300体で234 Rig／5,797 Boneを中央Solverへ登録し、UTJ Manager 149個とLegacy Manager 85個は全て停止した。診断時にAnimator連携条件を解除してもSample中186回の非同期Applyを確認でき、中央Solverが実際に演算・適用している。

診断コード撤去後の最終構成はCrowd ONが100体1.689ms、200体3.126ms、300体8.414ms（P95 37.699ms）、Crowd OFFが100体2.343ms、200体5.748ms、300体13.308ms（P95 42.647ms）だった。中央Springを強制Scheduleした診断でもCrowd ON 300体8.268ms、P95 37.946msである。旧個別Spring構成のCrowd ON 300体43.124ms、Crowd OFF 55.326msから大幅に改善し、`LateBehaviourUpdate`約19msも上位Markerから消えた。CameraがないDedicated ServerではAnimator連携によりSpring評価も停止する。

画面確認で旧`UnityChan.SpringBone`のSDモデルは正常だった一方、TokoChanz版`UTJ.SpringBone`を使うMarie／Tokoは揺れ方向が逆だった。UTJ元実装は初期Local Rotationを基準にBone AxisからTip方向へのAim Rotationを作るため、現在のAnimator姿勢差分を使うLegacy式との共通化をやめ、型別に回転復元した。修正後のCrowd ONは100体1.988ms、200体3.352ms、300体7.171ms（P95 18.951ms）で、性能退行はない。向きはMarie／Tokoの画面付きPlay Modeで再確認する。

### 運用上の注意

- `ServerScene`のNavMeshはScene全体を収集してベイクする。壁や梯子の追加後は再ベイクする。
- 移動床は`NavMeshModifier.Ignore From Build`で静的NavMeshから除外する。Collider、接地、床上歩行、移動追従は維持し、自律的な乗降経路が必要な場合は動的Linkを別途用意する。
- NPC同士のsolid Colliderは無効化するが、Player、床、移動床、Network Physics Objectとの接触は維持する。
- Wire／Wall／Ladder Actionは疑似入力または継続中Actionがある間だけ起動する。
- `NpcCrowdTraversalTestDriver`は既定OFFとし、特殊移動を手動検証する時だけ有効化する。

### 次の課題

次はNPCごとの`NetworkTransform`内部走査を置き換える一括Transform同期の試作を優先する。ただし標準`NetworkTransform`／`NetworkRigidbody`を単に無効化するのではなく、Server Snapshotの一括収集、Client補間、Spawn／Despawn／途中参加、Teleport、Authorityを同じ契約で満たす専用経路として比較する。Animator、Renderer、スキニングの方式変更は難易度が高いため、その後の課題とする。
