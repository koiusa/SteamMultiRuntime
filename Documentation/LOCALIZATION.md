# ローカライズ

Steam Multi Runtimeの日本語・英語ローカライズは、Unity LocalizationとAddressablesを使用します。

実装は内部パッケージ `com.koiusa.steammultiruntime.localization` に分離されています。

セットアップはパッケージ本体ではなく、**Steam Multi Runtimeを参照・導入する側のUnityプロジェクト**で行います。詳しい手順は、パッケージ同梱の `Documentation~/Localization.md` を参照してください。

導入先のUnity Editorで、次のメニューを一度実行します。

`Tools > SteamMultiRuntime > Localization > Install or Update Localization Tables`

パッケージを更新した場合も、このメニューを再実行してください。セットアップ確認は次のメニューから行えます。

`Tools > SteamMultiRuntime > Localization > Validate Installation`

生成された次のディレクトリは導入先プロジェクトのGitへコミットしてください。

- `Assets/SteamMultiRuntimeGenerated/Localization`
- `Assets/AddressableAssetsData`
