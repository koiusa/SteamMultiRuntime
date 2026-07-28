# Samples

サンプルは目的別に配置します。

```text
Samples/
├─ Features/                     # 1機能を単独で確認するサンプル
│  ├─ Keyconfig/
│  └─ TargetingSystem/
├─ Gameplay/
│  ├─ Stages/                    # ゲームプレイ用Stage
│  └─ Startup/                   # ゲームの起動・ロゴ表示用Scene
├─ SteamMultiPlayer_QuarterView/ # 複数機能を組み合わせたゲームサンプル
├─ SteamMultiPlayer_Server/
└─ SteamMultiPlayer_ThirdPersonView/
```

## Feature Samples

| 機能 | シーン | 確認できること |
|---|---|---|
| Keyconfig | `Features/Keyconfig/Keyconfig_ProductionInput.unity` | 本番Input Actionsを使った入力表示、リバインド、保存、復元 |
| TargetingSystem | `Features/TargetingSystem/TargetingSystem_ProductionInput.unity` | 本番Input Actionsを使った移動、視点操作、Solo Lock-On、ターゲット切り替え |

各シーンは単独で開いてPlayできます。詳しい操作方法はリポジトリの
`Documentation/Keyconfig.md`および`Documentation/TargetingSystem.md`を参照してください。

## Gameplay Stages

| シーン | 用途 |
|---|---|
| `Gameplay/Stages/PlayGroundScene.unity` | Player移動とNetwork物理Objectの検証 |
| `Gameplay/Stages/SandBoxScene.unity` | Player SpawnとNetwork物理Objectの最小Stage |
| `Gameplay/Stages/NPCVillage.unity` | NavMesh上のNPC自動Spawn検証 |
| `Gameplay/Stages/ServerScene.unity` | NPCとNetwork物理Objectを含むServer向けStage |

`Gameplay/Startup`にはゲーム起動フローで使用するLogoとWelcome Sceneを配置します。
Package Managerでは`Gameplay`フォルダー全体を1つの「Gameplay Sample」としてインポートします。

## 新しい機能サンプルの追加規約

1. `Features/<FeatureName>/`を作成します。
2. 1シーンで1機能を実演し、複数機能を前提にしない構成にします。
3. 本番設定を使う場合は`<FeatureName>_ProductionInput.unity`と命名します。
4. このファイルのFeature Samples表へ、シーンと確認項目を追記します。
