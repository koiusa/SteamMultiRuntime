# Input Bindings

本番用入力の正本は `Assets/SteamMultiRuntime/Runtime/Configs/Input/SteamMultiRuntime_InputActions.inputactions` です。

利用方法は[Keyconfig.md](Keyconfig.md)と[TargetingSystem.md](TargetingSystem.md)を参照してください。

## Gameplay

| 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|
| 移動 | WASD / 矢印キー | 左スティック |
| カメラ | マウス移動 | 右スティック |
| 攻撃 | 左クリック | X / □ |
| ダッシュ | Left Alt | RT / R2 |
| ガード | G | LB / L1 |
| 回復 | H | D-pad 下 |
| インタラクト | E | Y / △ |
| しゃがみ | C | B / ○ |
| ジャンプ | Space | A / × |
| 前／次のターゲット | 1 / 2 | D-pad 左／右 |
| スプリント | Left Shift | 左スティック押し込み |
| ロックオン | 中クリック | LT / L2 |
| グラップル | 右クリック | RB / R1 |
| リールイン／アウト | ホイール、Q / E | 右スティック上下 |
| ストライフ切替 | Left Ctrl | D-pad 上 |
| グラップル射出 | 左クリック | 右スティック押し込み |

`Drag`、`CameraZoom`、`AimCursorDelta`、`AimCursorPosition`、`AimCursorMove` は物理デバイスの特性に応じた入力です。全デバイスへ同じBindingを設ける対象にはしません。

## UI

| 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|
| ナビゲーション | WASD / 矢印キー | 左スティック / D-pad |
| 決定／キャンセル | Enter / Escape | A / B |
| メニュー | Tab | Start / Options |
| キャラクターメニュー | Backquote / C | Select / Share |
| 前／次のセクション | Q / E | LB / RB |

`System/GameQuit` は誤操作を避けるため Escape のみです。ゲームパッドではUIのキャンセルとメニューを経由して終了します。
