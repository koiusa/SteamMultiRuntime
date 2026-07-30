# SteamMultiRuntime 全体実装レビュー（2026-07-30）

レビュー対象: `future/target` / `855560a9`

この文書は現行実装に対する横断レビュー記録です。仕様の正本は[Documentation](Documentation/README.md)配下の各Architecture文書です。

## 結論

Local／NetworkでPlayer MotorとSkill Coordinatorを共有し、Netcode、Steam、URPをAdapterパッケージへ隔離する基本設計は良好です。入力・所有権・UIメニュー・Targetingもイベント駆動へ整理され、旧Reflection接続は残っていません。

一方、Network境界の入力検証、非同期処理のライフタイム、World Space UI基盤のフォールバックには、異常系や利用側Projectで顕在化する懸念があります。優先度順に以下を推奨します。

## 対応チェックリスト

- [ ] Dedicated ServerのScene切替を実行確認
- [ ] Steam LobbyのClient参加・退出・Stage切替を実行確認
- [ ] Client側のNetwork CameraとCharacter Selectを実行確認
- [ ] Local CameraがPlayer生成後に追従することを実行確認

## Findings

### 対応済み: Skill ServerRpcの方向ベクトル検証

対象:

- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.netcode/Runtime/Scripts/NetworkPlayerSkillController.cs:171`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player/Runtime/Scripts/DashSkillFeature.cs:25`

Ownerが送信する方向Vectorは、Server入口で各成分と二乗長の有限性を検証し、非ゼロ方向を正規化してからCoordinatorへ渡すよう修正しました。`NaN`／Infinityは拒否し、Zeroは各Skillの既存フォールバック処理で扱います。Player Netcode用PlayModeテストアセンブリを追加し、非有限成分、二乗長のOverflow、Zero、有限な非ゼロ方向の正規化を直接検証します。

残る改善候補:

1. Skillごとに水平面など許可する方向空間が決まった段階で、Server側の射影Policyを追加する。
2. Client単位のRPC頻度制限または診断カウンターを追加する。
3. Host／Clientを起動し、不正値がRPC経路からCoordinatorへ到達しないことを確認する統合テストを追加する。

### 対応中: Scene非同期処理のライフタイムと例外処理

対象:

- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby/Runtime/Scripts/LocalSceneFlowLoader.cs:61`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby/Runtime/Scripts/LocalStartupSceneLoader.cs:27`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby.steam/Runtime/Scripts/Network/SteamLobbyService/SteamLobbyService/SteamLobbyDedicatedServer.cs:74`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby/Runtime/Scripts/LocalStageSelectUIDocument.cs:161`

共通`AsyncOperation`待機へCancellationTokenを追加し、Local Loader、Dedicated Server、Stage UIは所有Objectの`destroyCancellationToken`へ接続しました。Local Stage切替はActive Scene変更によるUI無効化ではキャンセルせず、新Stageの有効化後に旧StageをUnloadするところまで完了します。起動系`async void`はキャンセルを正常終了として扱い、それ以外の例外を記録します。Loading通知も`try/finally`で対にしました。

Serialized構成では、`LocalManager.prefab`が`StageSceneList`（GUID `7ae2856614ff5574a8e2259452ab3c1d`）、Server Sample Sceneが`ServerSceneList`（GUID `a3476e62f9db54749a534eb4f3b3e3e5`）を参照します。両GUIDの実Asset解決と、一覧内の`PlayGroundScene`／`SandBoxScene`／`NPCVillage`／`ServerScene`がEditor Build Settingsへ登録済みであることを確認しました。

`Assets/Settings/Build Profiles/Windows_Alpha.asset`はDedicated Server用であることを確認し、固有Scene ListをServer用`SampleLobbyScene_Server`、`ServerSceneList`に登録された`ServerScene`／`NPCVillage`へ整合しました。Client用Lobby、Client用Stage、`UnityLogo`はDedicated Server Buildから除外しています。

残る確認事項:

- Sceneの`AsyncOperation`自体はUnity API上キャンセルできないため、Token取消後もUnity内部のLoad／Unload完了までは進行する。後続のScene操作だけを停止する設計である。
- Steam Lobby作成などScene API外の非同期処理は、各外部APIがCancellationTokenを受け取れる段階で別途接続する。

必要な検証:

1. Scene Load中のObject破棄、Stage UI Close、Application終了で例外が出ないこと。
2. 事前キャンセルとScene未設定の正常スキップで`LoadingStarted`／`LoadingFinished`が対になるPlayModeテストを追加した。実Sceneの成功・失敗・途中キャンセルでLoading Splashが残留しないことは実行確認が残る。
3. 二重開始を防ぐTaskまたは状態を共有し、同一Sceneの競合ロードをテストする。

### 対応中: World Space UIのLayer枯渇時フォールバック

対象:

- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.ui/Runtime/Scripts/WorldSpaceUiOverlayCamera.cs:179`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.ui/Runtime/Scripts/WorldSpaceUiOverlayCamera.cs:188`

未使用User Layerがなければ既存Layerを変更せず、専用Overlay Cameraを作成しないよう修正しました。Player Name Overlayは元のLayerと元Cameraによる通常描画を維持するため、利用側ProjectのLayer、Culling Mask、Physics設定を侵食しません。フォールバック時はDepth分離による常時前面表示を保証しない点を警告とArchitecture文書へ明記しています。

残る確認事項:

1. 全User Layer使用済みを注入したPlayModeテストで、既存ObjectのLayer、Camera Culling Mask、Camera数が変化しないことを確認済み。
2. Layer選択結果をEditor Validatorで事前確認できるようにする。

### 対応中: Render Pipeline Adapter差し替え時の解放

対象:

- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.ui/Runtime/Scripts/WorldSpaceUiOverlayCamera.cs:207`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.ui/Runtime/Scripts/WorldSpaceUiOverlayCamera.cs:215`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.player.ui/Runtime/Scripts/WorldSpaceUiOverlayCamera.cs:17`

Camera Stateへ実際に構成へ使用したAdapterを保持するよう修正しました。Registryの登録、解除、差し替え時は、現在のCamera Stateを適用済み旧AdapterでReleaseしてから、対象となるGame Cameraを新しいAdapterで再構築します。解放処理はRegistryの現在値に依存しません。

残る確認事項:

- Adapter差し替え時に旧AdapterをReleaseして新Overlay Cameraを構築し、最終解除時にLayerとCulling Maskを復元するPlayModeテストは、Enter Play Mode Options有効かつDomain Reload／Scene Reload無効（`m_EnterPlayModeOptions: 3`）のProject設定で成功した。同一Editor SessionでのPlay開始・停止反復とUI Scene再生成の確認は残る。
- URP Camera Stackに破棄済みOverlay Camera参照が残らないことを確認する。

### 対応中: Scene Loader契約の責務分割

対象:

- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby/Runtime/Scripts/ISteamLobbySceneLoader.cs`
- `Assets/SteamMultiRuntime/Runtime/Packages/com.koiusa.steammultiruntime.lobby/Runtime/Scripts/LocalSceneFlowLoader.cs:93`

Stage一覧だけを公開する`IStageSceneCatalog`を追加し、`ISteamLobbySceneLoader`はこの契約を継承してLobby Lifecycleを追加する構成へ分割しました。`LocalSceneFlowLoader`とLocal Stage UIはStage Catalogだけを使用し、警告して失敗していたLobby操作を削除しました。Steam UI／Serviceのfallback探索もSteam Lobby LoaderとDedicated Serverだけに限定しました。

残る確認事項:

- `FormerlySerializedAs("sceneLoader")`によるLocal Prefab参照の移行をUnity Importで確認する。
- Composition RootのValidatorで実行モードとLoader実装の組合せを確認する。

### 対応中: Scene-wide fallback探索の縮小

対象例:

- `CharacterSelectUiDocument`の`FindFirstObjectByType<PlayerModelProfileBase>()`
- `SteamLobbyUiDocument`の複数Loader探索
- `PlayerCompassHud`の`FindFirstObjectByType<Camera>()`
- Camera Context群のLocalManager／Controller探索

本番Prefabで参照済みのCharacter Profile、Menu、Lobby UI／Service、Loading Source、Local Camera Contextは、Scene-wide fallbackを削除して設定不備をエラーにしました。Focus MarkerはLocal／Network具象を探索せず`IFocusMarkerContext.PlayerObject`を正本とします。Compassは`Camera.main`を優先し、Tagがない場合も有効なGame Cameraが一意な場合だけ採用します。Dedicated Serverは明示参照、同一Composition Root、型付きRegistry、`NetworkManager.Singleton`だけを使用します。Loading Splashは他画面の`UIDocument`から設定を借りず、専用`PanelSettings`または自身が所有するRuntime設定を使用します。Runtime本体の`FindFirstObjectByType`／`FindAnyObjectByType`は0件です。

残る改善対象:

- 5種類のStandard Proxy Prefabは、Unity Import後のPrefab AssetからLocal／Network Model Syncと標準`CharacterModelIdList`参照を検証するEditorテストに成功した。GUID `53fb10e1957573c44be834f0809a3752`はAssetDatabaseで実Assetへ解決され、全Prefabの参照と一致する。Profile選択値の実Playerへの適用はLocal／Network PlayMode確認が残る。
- Character Select、Camera、Lobby Compositionの必須参照はPrefabの手動確認対象とし、設定値だけを重複確認するEditorテストは保持しない。

### P2: 自動テストが主要境界の回帰を十分に保護していない

優先して追加したいテスト:

1. Network Skill RPCの入力検証とServer Authority。
2. Host／Client／Dedicated ServerでのSpawn、Despawn、途中参加、Ownership変更。
3. Single／Multi TargetingとFacing PriorityのNetwork一致。
4. Domain Reload／Scene Reload無効時のWorld Space UI再登録。
5. Additive Sceneの通常Camera／AudioListener停止と`IPreservedLoadedSceneCamera`保護はPlayModeテスト追加済み。実Scene切替中のLoading Splashは実行確認が残る。
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
- Unity 6000.3.9f1 EditMode Test: 3/3成功（本件2件、依存Package 1件）
- Unity 6000.3.9f1 PlayMode Test: 14/14成功
- Enter Play Mode Options: Domain Reload／Scene Reload無効（設定値3）でPlayer UI PlayMode Test成功
- `CharacterModelIdList` GUID: AssetDatabaseによる実Asset解決とStandard Proxy Prefab 5種の参照一致を確認
- Stage Scene List GUID: Local／Server両Assetへの解決と全Scene名のEditor Build Settings登録を確認
- Build Profile: `Windows_Alpha`をDedicated Server用Lobby／Stage 2件へ整合
- Windows Dedicated Server Build: Unity 6000.3.9f1で成功（Server Lobby、`ServerScene`、`NPCVillage`）

## 未検証

- Host／Client／Dedicated Server通し動作
- Steam Lobby作成、参加、退出、再接続
- Windows Dedicated ServerのSteam Lobby作成とStage遷移を含む実行確認
