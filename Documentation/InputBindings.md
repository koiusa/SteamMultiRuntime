# Input Bindings

本番用入力の正本は `Assets/SteamMultiRuntime/Runtime/Configs/Input/SteamMultiRuntime_InputActions.inputactions` です。

利用方法は[Keyconfig.md](Keyconfig.md)と[TargetingSystem.md](TargetingSystem.md)を参照してください。

## Gameplay

| 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|
| 移動 | WASD / 矢印キー | 左スティック |
| カメラ | マウス移動 | 右スティック |
| 攻撃（Combat） | 左クリック | X / □ |
| ダッシュ | Left Alt | B / ○ |
| ガード | G | LB / L1 |
| 回復 | H | D-pad 下 |
| インタラクト（Adventure） | E | X / □ |
| しゃがみ | C | D-pad 上 |
| ジャンプ | Space | A / ×、右スティック押し込み（R3） |
| 前／次のターゲット | 1 / 2 | D-pad 左／右 |
| スプリント | Left Shift | 左スティック押し込み |
| ロックオン | 中クリック | Y / △ |
| グラップル | 右クリック | RB / R1 |
| リールイン／アウト | ホイール、Q / E | 右スティック上下 |
| ストライフ切替 | Left Ctrl | LT / L2 |
| グラップル射出 | 左クリック | RT / R2 |

グラップル入力を保持して照準している間はCamera操作を停止します。Wire接続後はグラップル入力を保持したままCameraを操作でき、入力を解放するとWireを切断します。

`Drag`、`CameraZoom`、`AimCursorDelta`、`AimCursorPosition`、`AimCursorMove` は物理デバイスの特性に応じた入力です。全デバイスへ同じBindingを設ける対象にはしません。

## UI

| 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|
| ナビゲーション | WASD / 矢印キー | 左スティック / D-pad |
| 決定／キャンセル | Enter / Escape | A / B、× / ○ |
| ポーズメニュー（キーコンフィグ／キャラクター選択） | Tab | Start / Options |
| キャラクター選択ショートカット | Backquote / C | — |
| 前／次のセクション | Q / E | LB / RB |

## System／Debug

| Action | 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|---|
| `System/DebugInputGuideToggle` | 入力ガイド表示切替 | F1 | DualShock Touchpad |
| `System/CharacterDebugToggle` | キャラクターデバッグUI表示切替 | F2 | — |
| `System/DebugSessionMenuToggle` | 実行モード別デバッグ画面（Local: Stage Select／Network: Steam Lobby） | F3 | Select / Share |
| `System/GameQuit` | ゲーム終了／EditorのPlay Mode終了 | Escape | — |

`System/GameQuit`は誤操作を避けるためEscapeのみです。ゲームパッドではUIのキャンセルとメニューを経由して終了します。
