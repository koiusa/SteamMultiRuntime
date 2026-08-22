# Koiusa Input Guide

Runtime操作ガイド、接続デバイス表示、Binding一覧、入力ハイライトを提供します。

`InputGuideOverlay`は`Koiusa.InputGuide` namespaceにあります。Input Actionsには`KeyConfigSettings`、アイコンには`com.koiusa.input.icons`の`KeyConfigIconSet`を使用します。

全Action Map、有効なMapのみ、または指定したMap群を表示できます。表示対象は `InputGuideSelectionController`、見た目は `IInputGuideOverlay` の `InputGuideConfiguration` が担当します。

```csharp
IInputGuideOverlay guide = overlay;
guide.ApplyConfiguration(new InputGuideConfiguration(
    InputGuideDisplayMode.Both,
    InputGuideLayoutPreset.Standard));
```

## コンパクト操作一覧プリセット

既定の `Standard` は従来どおり、操作一覧を画面上端の全幅で表示します。`CompactOperations` は操作一覧を右上の約440px幅の独立パネルとして表示します。背景、境界線、角丸、1 Map向けの文字サイズと行間はパッケージ内の公式USSに含まれるため、利用側でUSSを追加したり内部の `VisualElement` 名を参照したりする必要はありません。
`All`または複数Mapを指定した場合、コンパクト表示では上部のMapタブから1 Mapずつ切り替えます。選択したMapの操作がパネル高を超える場合は、操作一覧内を縦スクロールできます。

タブ切替と一覧スクロール入力は専用の`InputGuideNavigationController`が所有します。公式PrefabではOverlayを参照し、既存の`UI/PreviousSection`（Q／L1）、`UI/NextSection`（E／R1）、`UI/Navigate`（上下方向）を既定割当しています。各ActionはOverlayのInput Actions Configから選択できるため、Input Actions Configの二重設定やUnityEventの手動接続は不要です。タブ切替と入力スクロールはコンパクトな操作一覧の表示中だけ動作します。

```csharp
// ScreenLayout画面へ入る
IInputGuideOverlay guide = overlay;
var previousGuide = guide.CaptureConfiguration();
var previousSelection = selectionController.Current;
selectionController.ApplySelection(InputGuideSelection.Specified(new[] { "ScreenLayout" }));
guide.ApplyConfiguration(InputGuideConfiguration.CompactOperations());

// 手動キャリブレーションへ切り替える
selectionController.ApplySelection(InputGuideSelection.Specified(new[] { "Calibration" }));

// 画面終了時に通常設定へ戻す
selectionController.ApplySelection(previousSelection);
guide.ApplyConfiguration(previousGuide);
```

コンパクト表示では `F1 / TOUCH PAD` ヒントを既定で隠します。常に表示または常に非表示にする場合は、公開APIで指定できます。

```csharp
guide.ApplyConfiguration(new InputGuideConfiguration(
    InputGuideDisplayMode.OperationsOnly,
    InputGuideLayoutPreset.CompactOperations,
    InputGuideToggleHintVisibility.Visible));
```

レイアウトプリセットは `Both` / `DeviceOnly` / `OperationsOnly` / `Hidden` の表示モードとは独立しています。また、パッケージは `UIDocument.sortingOrder` を変更しません。必要な表示順は利用側の `UIDocument` で設定してください。

公式Prefabには `InputGuideSelectionController` が同梱され、Controllerから `InputGuideOverlay` を参照します。Action MapとBinding Groupの候補にはOverlay側の `Input Actions Config`を使用するため、Controllerへの二重設定は不要です。`Map Filter = Specified`ではAsset内のAction Mapをビットフラグ風の `Action Maps`フィールドから複数選択できます。保存値はMapの追加や並び替えで壊れないMap名リストです。`Binding Group`も同じInput Actions AssetのControl Schemeから選択します。

`IInputGuideOverlay` は表示側の公開境界です。`CaptureConfiguration()` と `ApplyConfiguration()` では表示モード、レイアウト、ヒント設定だけを切り替え、MapとBinding GroupはControllerが一貫したスナップショットとしてpushします。
