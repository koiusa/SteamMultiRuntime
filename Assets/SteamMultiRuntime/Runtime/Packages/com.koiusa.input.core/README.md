# Koiusa Input Core

Unity Input SystemのAction設定、共有ライフタイム、UIナビゲーションを提供します。

## InputActionPerformedTrigger

`InputActionPerformedTrigger`は`InputActionsConfig`から指定Actionを取得し、performed時にシリアライズ済みUnityEventを呼びます。特定ドメインへ依存しないため、終了、メニュー、デバッグ操作などへ共通利用できます。

1. `Input Actions Config`を設定します。
2. Inspectorの`Action`から対象Actionを選択します。
3. `Performed`へ呼び出すメソッドを設定します。

同じInputActionAssetにKeyconfigが適用したBinding Overrideもそのまま反映されます。
