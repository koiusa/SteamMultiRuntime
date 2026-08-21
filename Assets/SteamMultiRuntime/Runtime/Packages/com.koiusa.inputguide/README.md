# Koiusa Input Guide

Runtime操作ガイド、接続デバイス表示、Binding一覧、入力ハイライトを提供します。

`InputGuideOverlay`は`Koiusa.InputGuide` namespaceにあります。Input Actionsには`KeyConfigSettings`、アイコンには`com.koiusa.input.icons`の`KeyConfigIconSet`を使用します。

全Action Map、有効なMapのみ、または指定したMap群を表示できます。

```csharp
overlay.SetActionMaps(new[] { "Global", "Calibration" });
overlay.SetMapFilter(InputGuideMapFilter.EnabledOnly);
overlay.Refresh();
```
