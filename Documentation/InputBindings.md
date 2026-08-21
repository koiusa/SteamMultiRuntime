# Input Bindings

本番用入力の正本は `Assets/SteamMultiRuntime/Runtime/Configs/Input/SteamMultiRuntime_InputActions.inputactions` です。

利用方法は[Keyconfig.md](Keyconfig.md)と[TargetingSystem.md](TargetingSystem.md)を参照してください。

## Gameplay

| 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|
| 移動 | W／S／A／D（方向ごとの独立Binding） | 左スティック |
| カメラ | マウス移動 | 右スティック |
| 攻撃（Combat） | 左クリック | X / □ |
| ダッシュ | Left Alt | B / ○ |
| ガード | G | LB / L1 |
| 回復 | H | D-pad 下 |
| インタラクト（Adventure） | E | X / □ |
| キャラクター切替（Adventure） | — | B / ○を押しながらD-pad 左右 |
| しゃがみ | C | D-pad 上 |
| ジャンプ | Space | A / × |
| 前／次のターゲット | 1 / 2 | D-pad 左／右（R3は右スティック方向を優先、ニュートラル時は次へ） |
| スプリント | Left Shift | 左スティック押し込み |
| ロックオン | 中クリック | Y / △ |
| グラップル | 右クリック | RB / R1 |
| リールイン／アウト | ホイール、Q / E | 右スティック上下 |
| ストライフ切替 | Left Ctrl | LT / L2 |
| グラップル射出 | 左クリック | RT / R2 |

グラップル入力を保持して照準している間はCamera操作を停止します。Wire接続後はグラップル入力を保持したままCameraを操作でき、入力を解放するとWireを切断します。

`Drag`、`CameraZoom`、`AimCursorDelta`、`AimCursorPosition`、`AimCursorMove` は物理デバイスの特性に応じた入力です。全デバイスへ同じBindingを設ける対象にはしません。

Keyboard移動は`Player/MoveUp`、`MoveDown`、`MoveLeft`、`MoveRight`のButton Actionへ分離し、キーコンフィグで方向ごとに変更します。`Player/Move`はGamepad、Joystick、XRのVector2入力だけを保持し、`PlayerGameplayInputReader`がデジタル方向入力と合成します。矢印キーの初期Bindingは廃止しています。

## UI

| 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|
| ナビゲーション | WASD（独立Binding） / 矢印キー（独立Binding） | 左スティック / D-pad |
| 決定／キャンセル | Enter / Escape | A / B、× / ○ |
| ポーズメニュー（キーコンフィグ／キャラクター選択） | Tab | Start / Options |
| キャラクター選択ショートカット | Backquote / C | — |
| 前／次のセクション | Q / E | LB / RB |

`UI/Navigate`のKeyboardは、Playerの移動と同様に`WASD`と`Arrow Keys`を別々の2D Vector Compositeとして保持します。キーコンフィグでは2つの論理Bindingとして個別に表示します。

`UI/Navigate`は`PassThrough`型で入力方向の変化を通知します。`UiNavigationInputSession`が現在方向を保持するため、単発のフォーカス移動に加えて、キーボードやゲームパッドの方向入力を押し続けたときのリピート移動を行います。

UIが共有Actionを一時的に借りる場合、取得前から有効だったActionは解放後も有効状態を維持します。Character Selectを閉じた後も、EventSystemとPause Menuの`Navigate`／`Submit`／`Cancel`は無効化しません。

## System／Debug

| Action | 操作 | Keyboard / Mouse | Gamepad |
|---|---|---|---|
| `System/DebugInputGuideToggle` | 入力ガイド表示切替 | F1 | DualShock Touchpad |
| `System/CharacterDebugToggle` | キャラクターデバッグUI表示切替 | F2 | L3ダブルクリック |
| `System/DebugSessionMenuToggle` | 実行モード別デバッグ画面（Local: Stage Select／Network: Steam Lobby） | F3 | Select / Share |
| `System/GameQuit` | ゲーム終了／EditorのPlay Mode終了 | Escape | — |

`System/GameQuit`は誤操作を避けるためEscapeのみです。ゲームパッドではUIのキャンセルとメニューを経由して終了します。
