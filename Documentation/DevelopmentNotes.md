# Development Notes

## Steam Inputとテスト用App ID

Steamネットワークテストでテスト用App ID（480 / Spacewar）を使うと、Steam Inputの設定によってゲームパッド入力がUnity Input Systemへ届かなくなる場合があります。

今回の環境では正規のゲームApp IDに切り替えると解消しました。ネットワーク開始後にゲームパッドだけ反応しなくなる場合は、InputGuideOverlayや通信処理より先に、使用中のApp IDとSteam Input設定を確認してください。
