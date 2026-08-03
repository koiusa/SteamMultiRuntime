# NPC Crowdの性能計測

関連する現行仕様は[NPC Architecture](../NpcArchitecture.md)を参照してください。

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

この計測ではCPUが律速で、GPUは律速ではありません。ロード直後に記録された約1.2秒のFrameは定常性能から除外します。この後のHost比較により、フルキャラクターモデルではAnimator、Transform階層、Skinning、Renderer／Cullingが主要な追加負荷になることを確認しています。「Springのボトルネックを完全に解消した」「Client側も300体で高FPSになった」とは結論しません。

## Host 300体のモデル有無比較（2026-08-03）

同じCrowd Backendで、通常のスキンメッシュ／アニメーション付きモデルと、スキンメッシュを持たない単純なカプセル表示を比較しました。

| 表示構成 | 平均FPS | 平均Frame Time | PlayerLoop |
|---|---:|---:|---:|
| 通常スキンモデル | 24.6～24.8 FPS | 40.29～40.73 ms | 26.37～26.47 ms |
| 単純なカプセル | 69.5～73.8 FPS | 13.54～14.39 ms | 8.05～8.10 ms |

カプセル構成ではCrowdのGround Probeが約0.5ms、Movement適用が約0.44ms、Presentationが約0.5msであり、NPC数増加による大幅なFPS低下は通常モデルより起きにくいことを確認しました。通常モデルとの差は主にSkinning、Animator、Bone Transform伝播、Renderer Bounds、Cullingおよびモデル階層に付随するCollider同期です。したがって、カプセル構成の結果をそのままスキンメッシュ付きNPCの表示性能として扱いません。一方、Crowd移動Simulation単体のスケーリング確認にはカプセル構成を使用できます。

## FPS診断ログ

Editor／Development Buildでは`RuntimeFrameRateLogger`が全描画フレームをサンプリングし、1秒ごとに`[FrameRate]`を出力します。`Tools > SteamMultiRuntime > Diagnostics > Performance > FPS Logging`で永続的にON／OFFでき、Play中の切替も即時反映します。初期値はONです。

- 平均／最低／最高FPS
- 平均／最大Frame Time
- 取得可能な環境でのCPU／GPU平均・最大Frame Time
- Network Role、VSync、Target Frame Rate

毎フレームの`Debug.Log`は計測対象そのものを重くするため行いません。Release BuildにはLoggerを含めません。Network RoleはNetcode上の実状態で、Hostは`role=Host`、Dedicated Serverは`role=Server`、接続側は`role=Client`です。

`Tools > SteamMultiRuntime > Diagnostics > Performance > Automatic Behaviour Profiler`はRaw Profiler FrameをEditor上で走査する調査用機能であり、実行中の定常FPS計測ではOFFにします。ONのままでは周期的な`EditorLoop`停止が計測結果へ混入します。定常計測はRaw Frameを走査しない`RuntimeFrameRateLogger`の`[FrameRate]`／`[FrameRateDetail]`を使用します。

## 次の計測

実Clientまたは実HostのProfiler Captureを取得し、少なくとも次を同じ区間で比較します。

1. `Animator.ProcessGraph`およびAnimator関連Marker
2. `LateBehaviourUpdate`
3. `BehaviourUpdate`／NetworkBehaviour関連Marker
4. Spring Snapshot／Schedule／Apply
5. Transform階層更新とSkinning
6. Main Thread、Render Thread、GPU Frame Time

Client表示性能とServer Simulation性能は別の表に記録します。Headless ServerのFPSをClient FPSとして掲載しません。
