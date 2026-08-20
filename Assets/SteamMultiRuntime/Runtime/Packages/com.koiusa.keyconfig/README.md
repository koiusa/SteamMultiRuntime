# Koiusa Keyconfig

Unity Input System向けのRuntimeキーコンフィグUIです。入力表示、リバインド、行単位／全体リセット、保存、読込を提供します。

## Installation

Scoped Registryへ`https://registry.npmjs.com`とスコープ`com.koiusa`を登録し、Package Managerから
`com.koiusa.keyconfig`の`0.1.3`をインストールしてください。

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

## Sample

Package ManagerのSamplesから`Basic Key Rebinding`をImportしてください。
