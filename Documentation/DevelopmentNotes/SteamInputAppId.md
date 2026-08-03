# Steam Inputとテスト用App ID

関連する本番入力仕様は[Input Bindings](../InputBindings.md)を参照してください。

Steamネットワークテストでテスト用App ID 480（Spacewar）を使うと、Steam Input設定によってゲームパッド入力がUnity Input Systemへ届かない場合があります。今回の環境では正規App IDへの切替で解消しました。ゲームパッドだけ反応しない場合は、通信処理より先にApp IDとSteam Input設定を確認します。
