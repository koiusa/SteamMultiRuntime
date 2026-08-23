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

// 左側へ配置する場合
guide.ApplyConfiguration(InputGuideConfiguration.CompactOperations(
    InputGuidePanelAnchor.TopLeft));

// 中央へ配置する場合
guide.ApplyConfiguration(InputGuideConfiguration.CompactOperations(
    InputGuidePanelAnchor.Center));

`InputGuidePanelAnchor`は`TopLeft`、`TopCenter`、`TopRight`、`MiddleLeft`、
`Center`、`MiddleRight`、`BottomLeft`、`BottomCenter`、`BottomRight`の9方向です。
公式PrefabにはDevice本体、Mouse表示、Operations用の`InputGuidePanelLayout`が含まれ、
`InputGuidePanelCollection`がそれらを明示的な一覧として保持します。Device本体とMouse表示は
どちらも`InputGuidePanelSlot.Device`に属しますが、アンカーは各コンポーネントが個別に保持します。
`InputGuideConfiguration.DevicePanelAnchor`は一覧先頭の代表Deviceパネルだけを制御し、追加した
Deviceパネルのアンカーを上書きしません。
Mouse専用の固定スロットや専用コンポーネント型はありません。`InputGuideOverlay`は
このCollectionだけを参照します。各パネルは`Default Layout`、`Layout Override`、Anchorを
所有し、VisualTree再構築後も同じ設定が再適用されます。Override未指定時は
Default Layoutを使用します。

```csharp
guide.SetPanelAnchor(InputGuidePanelSlot.Device, InputGuidePanelAnchor.BottomCenter);
guide.SetPanelAnchor(InputGuidePanelSlot.Operations, InputGuidePanelAnchor.TopLeft);
```

`CompactOperations(anchor)`の引数はOperationsパネルのアンカーです。全パネルの位置を
一括適用・復元する場合は`InputGuideConfiguration`を使用します。実行中のUXML差し替えは
`SetPanelLayoutOverride(panelSlot, layout)`を使用し、共有Asset自体は変更しません。

## 公開API

ランタイム制御は`IInputGuideOverlay`を入口にします。`InputGuideConfiguration`は表示モード、
プリセット、ヒント、2パネルのAnchorをまとめてCapture／Applyする不変の表示設定です。
パネルUXMLは`GetPanelLayoutOverride`／`SetPanelLayoutOverride`で個別に差し替えます。

`InputGuidePanelLayout`と`InputGuidePanelCollection`はPrefab／Inspector構成用コンポーネントです。
同じスロットへ複数の`InputGuidePanelLayout`を登録できるため、Mouseや左右XR表示などを
Deviceの一部として視覚的に分離しつつ、利用側の固定スロットを増やさず構成できます。
各`InputGuideDeviceLayout`の描画先は`Host Element Name`で指定できます。MouseもIDによる
特別分岐はなく、標準Prefabでは描画先に`mouse-device-layouts-host`を指定した通常のDeviceです。
Layoutの構築、Target再適用、Collection検索はパッケージ内部APIであり、利用側から直接呼びません。
特に実行中のAnchorやLayout Overrideをコンポーネントへ直接代入せず、Overlay APIを使用します。

## 疑似デバイスレイアウト

`InputGuideDeviceLayoutCollection`は疑似デバイスを文字列IDの一覧として保持します。標準Prefabは
`keyboard`、`mouse`、`gamepad`をDeviceパネルへ登録しています。各項目にはDefault Layout、
Layout Override、対応するInput System Control Layout、排他グループ、初期表示を設定できます。

同じControl Layoutを持つ左右XRコントローラーなどは、必要な場合だけ`Required Usages`へ
`LeftHand`または`RightHand`を指定します。条件に一致する非排他レイアウトはすべて表示されるため、
左右コントローラーやMouseを同時表示できます。通常のKeyboard／Mouse／Gamepadでは設定不要です。

KeyboardとGamepadは標準で同じ`primary`排他グループ、Mouseは排他グループなしのため、
Keyboard＋Mouseを同時表示できます。Joystickなどは新しいIDとUXMLを一覧へ追加するだけで、
InputGuide本体のenumやBinding Groupを変更せず対応できます。

```csharp
guide.ShowDeviceLayout("joystick");
guide.SetDeviceLayoutVisible("mouse", true);
guide.SetDeviceLayoutOverride("joystick", customJoystickLayout);
```

配置は各`InputGuidePanelLayout`のInspectorにある3×3グリッドで設定します。
操作行生成、Mapタブ、Keyboard／Gamepad切替、スクロールは`InputGuideOverlay`が
現在のVisualTreeに接続して制御します。

## 疑似デバイス表示のカスタマイズ

DeviceとOperationsはそれぞれパネル単位でUXMLを差し替えられます。この表示は
`Both`と`DeviceOnly`の両方で使われます。カスタムDevice UXMLでも既存の入力ハイライトを
使う場合は、標準UXMLと同じ`control-*`名を付けます。

// 終了時
selectionController.ApplySelection(previousSelection);
guide.ApplyConfiguration(previousView);
```

表示モードは`Both`、`DeviceOnly`、`OperationsOnly`、`Hidden`です。Play Mode中のInspector変更も即時反映されます。

変更内容は[CHANGELOG](CHANGELOG.md)、プロジェクトでの設定詳細は[Keyconfig](../../../../../Documentation/Keyconfig.md)を参照してください。
