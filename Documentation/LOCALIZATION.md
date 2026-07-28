# ローカライズ

Steam Multi Runtimeの日本語・英語ローカライズは、Unity LocalizationとAddressablesを使用します。

セットアップはパッケージ本体ではなく、**Steam Multi Runtimeを参照・導入する側のUnityプロジェクト**で行います。詳しい手順は、パッケージ同梱の `Documentation~/Localization.md` を参照してください。

導入先のUnity Editorで、次のメニューを一度実行します。

`Tools > Steam Multi Runtime > Localization > Install or Update Japanese-English Tables`

生成された次のディレクトリは導入先プロジェクトのGitへコミットしてください。

- `Assets/SteamMultiRuntimeGenerated/Localization`
- `Assets/AddressableAssetsData`
