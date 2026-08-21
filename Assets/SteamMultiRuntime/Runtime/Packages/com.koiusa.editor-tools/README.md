# Koiusa Editor Tools

Unityプロジェクト全般で再利用できるEditor専用の診断・Asset調査ツールです。Runtime assemblyは含みません。

このパッケージはSteamMultiRuntime本体へ同梱する内部パッケージです。単独ではnpm公開しません。

## Tools

- `Tools/Koiusa/Diagnostics/Animation Events/Event Finder`
  - 指定名のAnimation Eventを持つAnimationClipを検索します。
- `Tools/Koiusa/Diagnostics/Animation Events/Receiver Visualizer`
  - Animatorで利用されるAnimation Eventと受信候補メソッドを可視化します。
- `Tools/Koiusa/Diagnostics/Performance/Automatic Behaviour Profiler`
  - Play Mode中のBehaviourUpdate内訳を一定間隔でConsoleへ記録します。

Receiver Visualizerは、メソッド名で受信先を解決するUnity Animation Eventの仕組みを調査するため、Editor内に限定してメソッドを列挙します。
