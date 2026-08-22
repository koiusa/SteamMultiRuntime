# Koiusa Input Guide

Input Systemの操作一覧、接続デバイス表示、入力ハイライトを提供します。現在のバージョンは`0.2.0`です。

## 構成

公式Prefab `Runtime/Resources/System/InputGuideOverlay.prefab` を使用します。

- `InputGuideOverlay`: Input Actions Configと表示
- `InputGuideSelectionController`: Action Map／Binding Group選択
- `InputGuideNavigationController`: Mapタブ切替／一覧スクロール

ControllerはOverlayを参照し、Input Actions Configを重複保持しません。`UIDocument.sortingOrder`は利用側で設定します。

## コンパクト表示

`CompactOperations`は右上約440pxの操作一覧です。複数Mapはタブ表示され、`UI/PreviousSection`、`UI/NextSection`、`UI/Navigate`で切替・スクロールできます。固定トグルヒントは既定で非表示です。

Inspectorで個別Mapを選ぶ場合は`InputGuideSelectionController`の`Map Filter`を`Specified`にします。選択値は並び替えに影響されないMap名として保存されます。

```csharp
IInputGuideOverlay guide = overlay;
var previousView = guide.CaptureConfiguration();
var previousSelection = selectionController.Current;

selectionController.ApplySelection(
    InputGuideSelection.Specified(new[] { "ScreenLayout" }));
guide.ApplyConfiguration(InputGuideConfiguration.CompactOperations());

// 終了時
selectionController.ApplySelection(previousSelection);
guide.ApplyConfiguration(previousView);
```

表示モードは`Both`、`DeviceOnly`、`OperationsOnly`、`Hidden`です。Play Mode中のInspector変更も即時反映されます。

変更内容は[CHANGELOG](CHANGELOG.md)、プロジェクトでの設定詳細は[Keyconfig](../../../../../Documentation/Keyconfig.md)を参照してください。
