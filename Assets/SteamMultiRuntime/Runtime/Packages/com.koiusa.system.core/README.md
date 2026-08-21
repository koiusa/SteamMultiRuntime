# Koiusa System Core

Unityプロジェクト全般で再利用できるシステム機能を提供します。

## Installation

Scoped Registryへ`https://registry.npmjs.com`とスコープ`com.koiusa`を登録し、Package Managerから`com.koiusa.system.core`をインストールしてください。

## GameQuitter

`GameQuitter`をGameObjectへ追加し、ボタン、メニュー、入力アダプターなどから`RequestQuit()`を呼びます。Runtimeでは`Application.Quit()`を呼び、Unity Editorでは同梱のEditor bridgeがPlay Modeを終了します。同じインスタンスへの終了要求は一度だけ処理され、`IsQuitRequested`で確認できます。

## Key configuration

Input Systemから終了要求を接続する場合は、任意パッケージ`com.koiusa.system.input`を追加してください。

`system.core`はInput System、`input.core`、`keyconfig`のいずれにも依存しません。

終了要求を記録したり、終了直前の処理を接続したりする場合は静的イベントを購読できます。

```csharp
using Koiusa.System.Core;

gameQuitter.RequestQuit();
GameQuitter.QuitRequested += SaveBeforeQuit;
```

購読側は自身のライフタイム終了時に必ず解除してください。

## ApplicationLifecycle

`ApplicationLifecycle`はUnityのフォーカス、一時停止、終了通知をインスタンス単位の型付きイベントとして公開します。状態は`IsFocused`、`IsPaused`、`IsQuitting`から随時確認できます。

```csharp
applicationLifecycle.FocusChanged += OnFocusChanged;
applicationLifecycle.PauseChanged += OnPauseChanged;
applicationLifecycle.Quitting += OnQuitting;
```

イベントは状態が実際に変化した場合だけ発火します。購読側は自身のライフタイム終了時に解除してください。
