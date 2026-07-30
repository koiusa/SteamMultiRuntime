# SteamMultiRuntime 全体実装レビュー（2026-07-30）

レビュー対象: `future/target` / `855560a9`

この文書は現行実装に対する横断レビュー記録です。仕様の正本は[Documentation](Documentation/README.md)配下の各Architecture文書です。

## 結論

Local／NetworkでPlayer MotorとSkill Coordinatorを共有し、Netcode、Steam、URPをAdapterパッケージへ隔離する基本設計は良好です。入力・所有権・UIメニュー・Targetingもイベント駆動へ整理され、旧Reflection接続は残っていません。

一方、Network境界の入力検証、非同期処理のライフタイム、World Space UI基盤のフォールバックには、異常系や利用側Projectで顕在化する懸念があります。優先度順に以下を推奨します。

## Findings

### P1: Skill ServerRpcが非有限方向ベクトルを受け入れる

対象:

- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.netcode/Runtime/Scripts/NetworkPlayerSkillController.cs:171`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player/Runtime/Scripts/DashSkillFeature.cs:25`

Ownerは`ActivateSkillServerRpc(int, Vector3)`へ任意の`Vector3`を送信できます。ServerはSkill Indexの範囲とDefinitionの存在を実質的に検証しますが、方向が有限値か、長さが妥当かを検証せずSkillへ渡します。`NaN`を含むVectorはゼロ判定を通過し、DashのMotor Motionへ到達し得ます。物理位置やNetwork同期状態へ非有限値が混入すると、そのPlayerの復旧が困難になります。

推奨対応:

1. RPC入口で各成分の`float.IsFinite`を検証する。
2. 水平面などSkillが許可する空間へ射影し、Server側で正規化する。
3. 不正値は拒否し、必要ならClient単位で頻度制限または診断カウンターを持つ。
4. `NaN`、Infinity、Zero、極端な長さを送るNetwork Testを追加する。

### P1: Scene起動処理の`async void`にキャンセルと統一的な例外処理がない

対象:

- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby/Runtime/Scripts/LocalSceneFlowLoader.cs:61`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby/Runtime/Scripts/LocalStartupSceneLoader.cs:27`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby.steam/Runtime/Scripts/Network/SteamLobbyService/SteamLobbyService/SteamLobbyDedicatedServer.cs:74`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby/Runtime/Scripts/LocalStageSelectUIDocument.cs:161`

Unity Messageの`Start`やUI CallbackからTaskを直接awaitしていますが、GameObject破棄・Scene切替・Application終了に連動するCancellationTokenがありません。`LocalStageSelectUIDocument`だけは例外を捕捉しますが、起動系3箇所は未捕捉です。また`SteamLobbyDedicatedServer.LoadLobbySceneOnEnteredAsync`は`finally`を使わないため、例外時に`LoadingFinished`が通知されません。

影響:

- Scene切替中に所有Objectが破棄されても後続処理が継続する。
- 例外が`async void`からSynchronizationContextへ送出され、呼出側が失敗を観測できない。
- Loading Splashが終了通知を受け取れず残留する可能性がある。

推奨対応:

1. 実処理を`Task`返却メソッドへ集約し、Unity Messageは例外を記録する薄い入口にする。
2. `destroyCancellationToken`または明示的なlifetime CTSをScene待機処理へ渡す。
3. `LoadingStarted`／`LoadingFinished`を必ず`try/finally`で対にする。
4. 二重開始を防ぐTaskまたは状態を共有し、同一Sceneの競合ロードをテストする。

### P1: World Space UIのLayer枯渇時にLayer 31を強制使用する

対象:

- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.ui/Runtime/Scripts/WorldSpaceUiOverlayCamera.cs:179`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.ui/Runtime/Scripts/WorldSpaceUiOverlayCamera.cs:188`

未使用User Layerがなければ警告後にLayer 31を選び、Player Name OverlayをそのLayerへ変更します。利用側ProjectがLayer 31をGameplay、Post Processing、Camera、Physicsに使用している場合、同LayerのObjectがOverlay Cameraへ映る、または元CameraのCulling Maskから除外される可能性があります。現在のRepositoryでは空きLayerがあるため直ちには発生しませんが、配布Packageとしては利用側設定を破壊し得ます。

推奨対応:

1. 空きLayerがない場合は専用Overlayを無効化し、元Camera描画へ安全にフォールバックする。
2. または明示的なLayer設定を要求し、起動時Validatorで競合をエラーにする。
3. Layer選択結果と競合Object数をEditor Validatorで確認できるようにする。

### P2: Render Pipeline Adapterの差し替え時に旧Adapterを解放できない

対象:

- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.ui/Runtime/Scripts/WorldSpaceUiOverlayCamera.cs:207`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.ui/Runtime/Scripts/WorldSpaceUiOverlayCamera.cs:215`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.ui/Runtime/Scripts/WorldSpaceUiOverlayCamera.cs:17`

CameraStateは`UsesDedicatedOverlay`だけを保持し、構成に使ったAdapter自体を保持しません。Registryの`Current`が解除・差し替えされた後は、旧Adapterではなく新しい`Current`へ`Release`を要求します。URP Adapterが消えた場合は`null`へReleaseするため、Base CameraのCamera StackからOverlay Cameraを明示的に除去できません。

推奨対応:

- CameraStateへ適用済みAdapterを保存し、差し替え前にそのInstanceへ`Release`する。
- RegistryのRegister／Unregister時に既存CameraStateを再構成する通知を発行する。
- Domain Reload無効、Adapter再登録、UI Scene再生成の組合せをPlayMode Testへ追加する。

### P2: `ISteamLobbySceneLoader`がLocal実装へ不要なLobby操作を要求する

対象:

- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby/Runtime/Scripts/ISteamLobbySceneLoader.cs`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby/Runtime/Scripts/LocalSceneFlowLoader.cs:93`

`LocalSceneFlowLoader`はStage一覧をUIへ提供するため同Interfaceを実装していますが、Lobby関連4操作は警告して失敗するだけです。一方、複数のLobby/UIクラスはScene内から同Interfaceを探索します。誤ったCompositionでもコンパイルが通り、実行時に初めてLobby遷移が失敗します。

推奨対応:

- Stage Catalog、Presentation Scene切替、Lobby Lifecycleを小さな契約へ分割する。
- Local UIはStage用契約だけを参照し、Steam LobbyはLobby Lifecycle契約を必須化する。
- Composition RootのValidatorで実行モードとLoader実装の組合せを確認する。

### P2: Scene-wide fallback探索が複数Runtime構成で曖昧になる

対象例:

- `CharacterSelectUiDocument`の`FindFirstObjectByType<PlayerModelProfileBase>()`
- `SteamLobbyUiDocument`の複数Loader探索
- `PlayerCompassHud`の`FindFirstObjectByType<Camera>()`
- Camera Context群のLocalManager／Controller探索

多くはInspector参照が欠けた場合のフォールバックですが、Local Player、Remote Player、Lobby Camera、Stage Cameraが同居する構成では「最初」の意味が安定しません。Prefabのシリアライズ参照が正常なら回避できますが、派生PrefabやAdditive Sceneで誤接続を隠す可能性があります。

推奨対応:

- 本番Prefabでは必須参照をValidatorで保証し、曖昧なfallbackを開発時エラーへ寄せる。
- Local Playerは既存の`ILocalPlayerProvider`／ownership registryを利用する。
- Cameraは役割を示す型付きRegistryまたはComposition Rootから注入する。

### P2: 自動テストが主要境界の回帰を十分に保護していない

優先して追加したいテスト:

1. Network Skill RPCの入力検証とServer Authority。
2. Host／Client／Dedicated ServerでのSpawn、Despawn、途中参加、Ownership変更。
3. Single／Multi TargetingとFacing PriorityのNetwork一致。
4. Domain Reload／Scene Reload無効時のWorld Space UI再登録。
5. Additive Scene切替中のCamera、AudioListener、Loading Splash。
6. UI Menu StackとInput Action Leaseの開閉反復。
7. NPC多数時のNavMesh／回避／Network同期負荷。

## 良い点

1. Player coreは`Unity.Netcode`を参照せず、Network実装が上位Adapterとして依存している。
2. Network Skillの適用、Damage、HealはServer側Coordinatorを通る。
3. Local所有権は`ILocalPlayerOwnership`とNotifierへ型付けされ、旧メンバー名探索がない。
4. UI NavigationとAction lifetimeが`UiNavigationInputSession`／`InputActionLease`へ集約されている。
5. Targeting状態、Camera表示、Facing Requestが分離され、Camera Weightの所有者も一元化されている。
6. RuntimeのReflection検索結果は0件で、既知のEditor例外とThirdpartyに限定されている。
7. 必要なFrame LoopはMotor、物理、Camera補間、表示追従など連続処理を中心に限定されている。
8. asmdefと`package.json`でNetcode、Steam、URPの技術依存を概ね隔離できている。

## 今回確認した範囲

- Production Input Actions、Keyconfig、Pause／Character／Stage／Lobby UI
- Local／Network Player入力、Motor、Traversal、Skill、Combat、Guard
- NPC NavMesh Module、疑似入力、Network同期
- Targeting、Facing Request、Camera Mixer、Indicator
- Character Profile、Model Sync、Player Name Overlay、Compass
- Lobby、Steam接続、Scene Loader、Loading Splash
- Runtime／Editor asmdef、package.json、Prefab／Scene／UXML／USSの主要参照
- Reflection、Scene-wide探索、Frame Loop、`async void`、RPC、購読解除の横断検索

## 検証結果

- `git diff --check`: 正常
- JSON／asmdef／Input Actionsの構文確認: 85ファイル正常
- Documentation内リンク確認: リンク切れなし
- First-party Runtimeの禁止Reflection: 検出なし

## 未検証

- Unity Editorでの再ImportとConsoleエラー
- EditMode／PlayMode Test Runner
- Host／Client／Dedicated Server通し動作
- Steam Lobby作成、参加、退出、再接続
- Domain Reload／Scene Reload無効での連続Play
- Windows／macOS／Linux Player Build
- 多数NPC、Target、World Space UIのProfiler計測
