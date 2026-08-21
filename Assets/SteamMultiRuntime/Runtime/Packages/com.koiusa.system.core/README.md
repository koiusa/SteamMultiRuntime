# Koiusa System Core

Unityプロジェクト全般で再利用できるシステム機能を提供します。

## Installation

Scoped Registryへ`https://registry.npmjs.com`とスコープ`com.koiusa`を登録し、Package Managerから`com.koiusa.system.core`をインストールしてください。

## GameQuitter

`GameQuitter`をGameObjectへ追加し、ボタン、メニュー、入力アダプターなどから`RequestQuit()`を呼びます。Runtimeでは`Application.Quit()`を呼び、Unity Editorでは同梱のEditor bridgeがPlay Modeを終了します。

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
