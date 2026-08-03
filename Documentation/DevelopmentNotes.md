# Development Notes

この文書には、現在採用している構成、再現可能な計測条件、確認済みの事実だけを記載します。途中で試して撤回した実装の数値を、現行性能や達成済み成果として扱いません。

## 大規模NPCの現状

目標はNPC 1000体ですが、現時点で1000体を実用フレームレートで表示できることは確認できていません。Crowd BackendはNPCの移動・接地・回避・移動床追従を中央処理する標準経路です。Crowd OFFは互換性確認用の従来Rigidbody経路で、大規模運用向けではありません。

- Local NPCとServer上のNetwork NPCは`NpcCrowdSimulation`／`NpcCrowdMotor`で移動する。
- Network ClientはCrowd Simulationを実行せず、Serverから受信した状態を表示する。
- Boid／RVO近傍計算とCrowd移動計算にはSpatial GridとBurst Jobを使用する。
- UTJ／Legacy UnityChan Spring Boneは中央`NpcCrowdSpringSimulation`へ登録する。
- Springの元Managerは、中央SolverへBoneを登録できたモデルだけ停止する。
- Reflectionは使用しない。

## ベンチマークの解釈

過去に記録した高速な数値は、Unity 6000.3.9f1、`ServerScene`、Network Server、`-batchmode -nographics`、Warmup 180 frame、Sample 300 frame、Seed 481516で取得したヘッドレスServer計測です。この条件ではCamera、Game View、GPU描画、Client側NetworkTransform補間などが実際のプレイ環境と異なります。

そのため、次の用途に限定して扱います。

- Crowd ON／OFFや実装変更前後のServer CPU相対比較
- Main Thread、Fixed step、GC、Profiler Markerの退行検出
- Client表示性能、実プレイFPS、GPU負荷の根拠には使用しない

過去のヘッドレス計測では、Crowd ON 300体でFrame平均8.284ms、P95 39.345msという値を得ています。ただしこれは「Clientで120FPS出る」「300体を実用表示できる」という意味ではありません。P95が平均から大きく離れており、スパイクも残っています。

## 実Client計測（2026-08-02）

Game Viewを表示するClientで`RuntimeFrameRateLogger`を使用した定常区間は、次の範囲でした。

| 項目 | 実測範囲 |
|---|---:|
| 平均FPS | 27.3～29.4 FPS |
| 平均Frame Time | 33.98～36.67 ms |
| CPU Frame Time平均 | 33.83～36.87 ms |
| GPU Frame Time平均 | 3.01～3.41 ms |
| 最大Frame Time | 62.33～89.55 ms |

この計測ではCPUが律速で、GPUは律速ではありません。ロード直後に記録された約1.2秒のFrameは定常性能から除外します。現時点ではCPU内訳をMarker別に採取していないため、Animator、Transform階層、Spring、Skinning、NetworkBehaviourのどれが最大要因かは未確定です。「Springのボトルネックを完全に解消した」「Client側も300体で高FPSになった」とは結論しません。

## FPS診断ログ

Editor／Development Buildでは`RuntimeFrameRateLogger`が全描画フレームをサンプリングし、1秒ごとに`[FrameRate]`を出力します。`Tools > SteamMultiRuntime > Diagnostics > FPS Logging`で永続的にON／OFFでき、Play中の切替も即時反映します。初期値はONです。

- 平均／最低／最高FPS
- 平均／最大Frame Time
- 取得可能な環境でのCPU／GPU平均・最大Frame Time
- Network Role、VSync、Target Frame Rate

毎フレームの`Debug.Log`は計測対象そのものを重くするため行いません。Release BuildにはLoggerを含めません。Network RoleはNetcode上の実状態で、Hostは`role=Host`、Dedicated Serverは`role=Server`、接続側は`role=Client`です。

`Tools > SteamMultiRuntime > Diagnostics > Automatic Behaviour Profiler`はRaw Profiler FrameをEditor上で走査する調査用機能であり、実行中の定常FPS計測ではOFFにします。ONのままでは周期的な`EditorLoop`停止が計測結果へ混入します。定常計測はRaw Frameを走査しない`RuntimeFrameRateLogger`の`[FrameRate]`／`[FrameRateDetail]`を使用します。

## 移動床

Server側では、Overlap候補の選別、幾何距離によるBinding保持、移動床のPhysics姿勢適用直後にBinding中NPCだけを追従させる中央通知を採用しています。これによりServer側のすり抜けは大幅に減りましたが、全条件で解消したとは断定しません。

Network NPCは通常時の`NetworkTransform.PositionThreshold`を0.1mとし、移動床Binding中だけ0.02mへ下げ、解除時に戻します。Client側で床とActorへ別の座標上書きや決定論的時刻再生を行う案は、操作や表示の退行があったため採用していません。

Player、Network NPC、NetworkMoverは現在のPrefabに保存された通常の`NetworkTransform`補間を使用します。補間方式はNetcode 2.7.0の新しいBuffer Queue方式で速度変化も平滑化する`Smooth Dampening`を使用し、`PositionLerpSmoothing`はON、Position／Rotationの最大補間時間は0.1秒です。旧`LegacyLerp`は使用しません。床とNPCで最大補間時間を一致させ、相対的な表示遅延を作りません。補間はNetwork Tick間の見た目を滑らかにしますが、実描画FPSそのものは増やしません。

## Spring Bone

中央Burst SpringへのRig登録とUTJ／Legacy別の回転復元は動作確認済みです。Marie／TokoのUTJ Spring BoneとSD UnityChanのLegacy Spring Boneでは回転契約が異なるため、同じ復元式を使用しません。

ただし、ヘッドレスServerで得た改善値をClient表示性能へそのまま適用できません。ClientではAnimator評価、Transform姿勢伝播、Renderer／Skinningと組み合わさるため、CPU Markerを分離して再計測する必要があります。

## 次の計測

実Clientまたは実HostのProfiler Captureを取得し、少なくとも次を同じ区間で比較します。

1. `Animator.ProcessGraph`およびAnimator関連Marker
2. `LateBehaviourUpdate`
3. `BehaviourUpdate`／NetworkBehaviour関連Marker
4. Spring Snapshot／Schedule／Apply
5. Transform階層更新とSkinning
6. Main Thread、Render Thread、GPU Frame Time

Client表示性能とServer Simulation性能は別の表に記録します。Headless ServerのFPSをClient FPSとして掲載しません。

## Steam Inputとテスト用App ID

Steamネットワークテストでテスト用App ID 480（Spacewar）を使うと、Steam Input設定によってゲームパッド入力がUnity Input Systemへ届かない場合があります。今回の環境では正規App IDへの切替で解消しました。ゲームパッドだけ反応しない場合は、通信処理より先にApp IDとSteam Input設定を確認します。
