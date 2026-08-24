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

## Device diagnostics

入力デバイスの変更ログはデフォルトで無効であり、`InputSystem.onDeviceChange`を購読しません。
診断が必要なプラットフォームのScripting Define Symbolsに
`KOIUSA_KEYCONFIG_DEVICE_DIAGNOSTICS`を追加すると、GamepadとJoystickのデバイス変更がログ出力されます。
EditorまたはDevelopment Buildであるだけでは有効になりません。
Unity Editorの`Edit > Project Settings > Koiusa > Keyconfig`で、現在選択中のBuild Targetに対して有効／無効を切り替えられます。
