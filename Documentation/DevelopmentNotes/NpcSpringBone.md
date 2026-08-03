# NPC Spring Bone

関連する現行仕様は[NPC Architecture](../NpcArchitecture.md)を参照してください。

中央Burst SpringへのRig登録とUTJ／Legacy別の回転復元は動作確認済みです。Marie／TokoのUTJ Spring BoneとSD UnityChanのLegacy Spring Boneでは回転契約が異なるため、同じ復元式を使用しません。

ただし、ヘッドレスServerで得た改善値をClient表示性能へそのまま適用できません。ClientではAnimator評価、Transform姿勢伝播、Renderer／Skinningと組み合わさるため、CPU Markerを分離して再計測する必要があります。計測条件と比較項目は[NPC Crowdの性能計測](NpcCrowdPerformance.md)に記録します。
