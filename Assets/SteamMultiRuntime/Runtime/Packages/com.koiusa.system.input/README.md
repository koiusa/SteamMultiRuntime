# Koiusa System Input

`com.koiusa.system.core`をUnity Input Systemへ接続する任意アダプターパッケージです。

## Installation

Scoped Registryへ`https://registry.npmjs.com`とスコープ`com.koiusa`を登録し、Package Managerから`com.koiusa.system.input`をインストールしてください。`system.core`と`input.core`は推移依存として導入されます。

## GameQuitInputTrigger

1. `GameQuitter`と`GameQuitInputTrigger`をGameObjectへ追加します。
2. Triggerの`Game Quitter`へ対象の`GameQuitter`を設定します。
3. `Input Actions Config`を設定し、`Quit Action`を一覧から選択します。

Actionがperformedになると`GameQuitter.RequestQuit()`を呼びます。`com.koiusa.keyconfig`が同じInputActionAssetへ適用したBinding Overrideもそのまま反映されます。
