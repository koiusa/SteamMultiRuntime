# Koiusa Keyconfig

Unity Input System向けのRuntimeキーコンフィグUIです。入力表示、リバインド、行単位／全体リセット、保存、読込を提供します。

## Installation

Scoped Registryへ`https://registry.npmjs.com`とスコープ`com.koiusa`を登録し、Package Managerから
`com.koiusa.keyconfig`の`0.1.38`をインストールしてください。

`ButtonWithOneModifier` Compositeは`Ctrl+R`のように1操作として表示され、ModifierとButtonを順にリバインドできます。
各行の「修飾キー追加／削除」から、単一Binding、`ButtonWithOneModifier`、`ButtonWithTwoModifiers`を相互変換できます。

## Localization

Provider未設定時は日本語・英語の標準ローカライザーが使用されます。OSのUI言語を初期値とし、実行中は次のように切り替えられます。

```csharp
KeyConfigLocalization.BuiltInLocale = KeyConfigLocale.Japanese;
KeyConfigLocalization.BuiltInLocale = KeyConfigLocale.English;
```

アプリ独自の翻訳を使う場合は`IKeyConfigLocalizer`を実装して設定します。

```csharp
KeyConfigLocalization.Provider = new MyKeyConfigLocalizer();
```

Action Map、Action、Composite、スキーム、プロファイルの元名称が翻訳キーとしてProviderへ渡されます。
言語変更時はProviderの`LocaleChanged`を発火すると、標準UIと動的生成されたタブ・見出しが更新されます。

## Input GuideのAction Map

`InputGuideOverlay`は全Map、有効なMapのみ、指定Map群を操作一覧へ同時表示できます。Map名がセクション見出し、Action名が各行として`KeyConfigLocalization`へ渡されます。

```csharp
overlay.SetActionMaps(new[] { "Global", "Calibration" });
overlay.SetMapFilter(InputGuideMapFilter.EnabledOnly);
overlay.Refresh();

// 全Map
overlay.SetMapFilter(InputGuideMapFilter.All);
```

従来の`actionMapName`は、Map Filterが`Specified`かつ`actionMapNames`が空の場合に引き続き使用されます。両方が空の場合は全Mapを表示します。

## Sample

Package ManagerのSamplesから`Basic Key Rebinding`をImportしてください。
