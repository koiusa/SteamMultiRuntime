# Koiusa Keyconfig

Unity Input System向けのRuntimeキーコンフィグUIです。入力表示、リバインド、行単位／全体リセット、保存、読込を提供します。

## Installation

Scoped Registryへ`https://registry.npmjs.com`とスコープ`com.koiusa`を登録し、Package Managerから
`com.koiusa.keyconfig`をインストールしてください。ソース上の現在バージョンは[package.json](package.json)を正本とします。

`ButtonWithOneModifier` Compositeは`Ctrl+R`のように1操作として表示され、ModifierとButtonを順にリバインドできます。
各行の「修飾キー追加／削除」から、単一Binding、`ButtonWithOneModifier`、`ButtonWithTwoModifiers`を相互変換できます。

## Localization

Provider未設定時は日本語・英語の標準ローカライザーが使用されます。OSのUI言語を初期値とし、実行中は次のように切り替えられます。

```csharp
var localizer = new BuiltInKeyConfigLocalizer(KeyConfigLanguage.Japanese);
KeyConfigLocalization.SetLocalizer(localizer);
```

アプリ独自の翻訳を使う場合は`IKeyConfigLocalizer`を実装して設定します。

```csharp
KeyConfigLocalization.SetLocalizer(new MyKeyConfigLocalizer());
```

Action Map、Action、Composite、スキーム、プロファイルの元名称が翻訳キーとしてProviderへ渡されます。
言語変更時はProviderの`LocaleChanged`を発火すると、標準UIと動的生成されたタブ・見出しが更新されます。

入力アイコンは任意の`com.koiusa.input.icons`、操作ガイドは別パッケージの
`com.koiusa.inputguide`が所有します。

## Sample

Package ManagerのSamplesから`Basic Key Rebinding`をImportしてください。

`KeyConfigPanel`を実行時に追加する場合は、Reflectionではなく`Configure`で依存Assetを一括設定できます。
`Awake`後や非表示中にも呼び出せ、再設定時は既存のイベント購読が安全に置き換えられます。

```csharp
var settings = ScriptableObject.CreateInstance<KeyConfigSettings>();
settings.SetInputActionAsset(inputActionAsset);

var panel = gameObject.AddComponent<KeyConfigPanel>(); // UIDocumentも自動的に要求されます
panel.Configure(settings, layout, styleSheet, iconSet, "Keyboard&Mouse");
panel.SetPersistence(LoadBindingOverrides, SaveBindingOverrides);
panel.Open();
```

Inspectorで各フィールドを設定する従来方式も引き続き利用できます。`SetBindingGroup`、
`ClearBindingGroupFilter`、`SetPersistence`は`Configure`後も同様に利用できます。

Editor Toolは用途別に次のカテゴリへ分類されます。

- `Tools > KeyConfig > Assets`: Input Action Asset ResolverとInput Binding Icon Resolverの作成
- `Tools > KeyConfig > Configuration`: Input Binding Iconの編集
