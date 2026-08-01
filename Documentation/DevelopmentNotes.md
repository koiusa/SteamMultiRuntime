# Development Notes

## Steam Inputとテスト用App ID

Steamネットワークテストでテスト用App ID（480 / Spacewar）を使うと、Steam Inputの設定によってゲームパッド入力がUnity Input Systemへ届かなくなる場合があります。

今回の環境では正規のゲームApp IDに切り替えると解消しました。ネットワーク開始後にゲームパッドだけ反応しなくなる場合は、InputGuideOverlayや通信処理より先に、使用中のApp IDとSteam Input設定を確認してください。

## 大規模NPC Crowd化の到達点と残課題

NPC 1000体表示を目標として、Local NPCとNetwork Server NPCの通常移動、重力、接地、壁接触、移動床、Boid／RVO近傍計算を`NpcCrowdSimulation`と`NpcCrowdMotor`へ集約した。個別Rigidbody MotorやNPCごとのPhysics callbackへの依存を減らし、Spatial Grid、Burst Job、一括Physics Queryを使用することで、NPCの移動Simulationについては一定の改善を得られた。Network ClientはCrowd Simulationを重複実行せず、Serverの結果だけを表示する。

一方、NPC数を増やしたときのフレーム負荷は移動Simulationだけでは解消しない。通常のGameObject Characterでは、NPCごとにAnimatorの評価、ボーンTransform更新、`SkinnedMeshRenderer`のスキニング、複数Renderer／Materialの描画が残る。Animatorの距離別更新頻度やSpring Boneの一括Job化はCPU負荷の削減に有効だが、1000体規模の最終的なボトルネックはスキンメッシュアニメーションと描画方式になる。

GPU InstancingによるCrowd Rendererも試作したが、モデルごとの次の差異を一律に処理できず、現時点では採用していない。

- 顔のBlend Shapeと表情Animation
- Skinned MeshごとのRenderer、root bone、bind poseの基準座標
- FBXの上方向／前方向と軸変換。特にSD Unitychanはメッシュと腰骨に90度の変換を持つ
- 頭、髪、装飾、Spring Boneを含む複数メッシュの骨階層
- Humanoid Retargeting、遷移、特殊移動Animationとの同期
- Characterモデルごとに異なるMaterial、Texture、Submesh構成

したがって次の大規模化では、Crowd Motorの追加最適化より先に、Unity標準の描画結果を正解として比較できるスキニング検証基盤を用意する。`SkinnedMeshRenderer.BakeMesh`との頂点比較、bind pose時の単位行列検証、ボーンインデックス／ウェイト形式の検査、モデル別の軸変換テストを行ったうえで、GPU Skinning、Animation Texture、Compute SkinningまたはEntities Graphicsのいずれを採用するか判断する。

NPC Crowd化は移動・接地・回避Simulationの基盤として維持する。ただし「Crowd Motor化だけで1000体を達成できる」とは扱わず、スキンメッシュアニメーションと描画の再設計を独立した主要課題として扱う。
