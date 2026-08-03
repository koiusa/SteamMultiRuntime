# Development Notes

開発中に確認した事象、再現可能な計測条件、環境依存の注意事項への索引です。現行仕様の正本は各Architecture文書とし、ここからリンクする事象別ノートには確認済みの事実と計測結果だけを記載します。途中で試して撤回した実装の数値を、現行性能や達成済み成果として扱いません。

| 事象 | 内容 | 関連する正本 |
|---|---|---|
| [NPC Crowdの性能計測](DevelopmentNotes/NpcCrowdPerformance.md) | 大規模NPCの現状、Headless／Client／Host計測、FPS診断、次の計測項目 | [NPC Architecture](NpcArchitecture.md) |
| [NPCの移動床追従](DevelopmentNotes/NpcMovingPlatforms.md) | Server側Binding、NetworkTransform閾値、補間方式 | [NPC Architecture](NpcArchitecture.md) |
| [NPC Spring Bone](DevelopmentNotes/NpcSpringBone.md) | 中央Burst Spring、モデル別回転契約、計測上の注意 | [NPC Architecture](NpcArchitecture.md) |
| [Steam Inputとテスト用App ID](DevelopmentNotes/SteamInputAppId.md) | App ID 480使用時のゲームパッド入力問題 | [Input Bindings](InputBindings.md) |

新しい事象はこのファイルへ直接追記せず、`DevelopmentNotes/`内に独立したノートを作成して上表からリンクします。複数事象に共通する現行仕様はDevelopment Notesへ重複させず、対応するArchitecture文書を更新します。
