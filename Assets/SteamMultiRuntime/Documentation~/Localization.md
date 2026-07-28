# ローカライズの導入

Steam Multi Runtime は、Unity Localization 1.5.9とAddressablesを使用して日本語・英語のUIを提供します。

ローカライズ機能は内部パッケージ `com.koiusa.steammultiruntime.localization` に分離されています。ランタイムAPI、固定キーのカタログ、UI Toolkit連携、導入先設定を生成するEditorツールをこのパッケージへ集約し、`com.koiusa.ui.common` はLocalizationへ依存しません。

## どのプロジェクトで設定するか

以下の作業は、Steam Multi Runtimeパッケージ本体ではなく、**このパッケージを参照・導入する側のUnityプロジェクト**で行います。

Localization SettingsとAddressables設定はUnityプロジェクト単位のアセットです。インストール済みUPMパッケージ内は読み取り専用になる場合があり、導入先に既存のAddressables設定が存在する可能性もあるため、パッケージ本体には生成済み設定を含めていません。

Steam Multi Runtime自身の開発プロジェクトでサンプルを実行する場合は、この開発プロジェクトが参照側を兼ねるため、同じセットアップが必要です。

## 初回セットアップ

1. 参照側プロジェクトへ `com.koiusa.steammultiruntime` をインストールします。
2. 参照側プロジェクトのUnity Editorで、次のメニューを一度実行します。

   `Tools > SteamMultiRuntime > Localization > Install or Update Localization Tables`

3. 次のアセットが参照側プロジェクトに生成されます。

   - `Assets/SteamMultiRuntimeGenerated/Localization`
   - `Assets/AddressableAssetsData`

4. 生成された両ディレクトリを、参照側プロジェクトのGitへコミットします。

再度メニューを実行すると、既存のエントリIDを維持したまま既知の翻訳が更新されます。日本語・英語の文字列テーブルはAddressablesのプリロード対象として登録されます。

パッケージ更新後に翻訳キーが追加された場合も、同じメニューを再実行してください。文字列テーブル名は他パッケージと衝突しにくい `SteamMultiRuntime UI` を使用します。

セットアップ状態を確認するには、次を実行します。

`Tools > SteamMultiRuntime > Localization > Validate Installation`

不足しているLocale、テーブル、固定キーがConsoleへ表示されます。

## AddressablesのPlay Mode設定

Editor上で確認する場合は、通常、Addressables GroupsウィンドウのPlay Mode Scriptを `Use Asset Database` にします。

`Use Existing Build` を使用する場合は、先に次を実行してください。

`Window > Asset Management > Addressables > Groups > Build > New Build > Default Build Script`

## 実行時の言語切替

コードから切り替える場合:

```csharp
GameLocalization.SelectLocale("ja");
GameLocalization.SelectLocale("en");
```

選択した言語はPlayerPrefsへ保存されます。

UI Toolkitでは、`UIDocument`と同じGameObjectへ `LocaleSelector` を追加し、`locale-dropdown` という名前の `DropdownField` を用意できます。ボタンから `SetJapanese` / `SetEnglish` を呼び出す方法にも対応しています。

動的文字列には `GameLocalization.Get(key, arguments)` を使用します。パッケージに含まれる画面の静的なLabelとButtonは、言語変更時に自動更新されます。

コードでは表示文ではなく、`character.title`、`keyconfig.save`、`lobby.create` のような固定キーを使用してください。UXMLの初期表示文だけは、UIツリーのローカライザーが固定キーへ変換します。
