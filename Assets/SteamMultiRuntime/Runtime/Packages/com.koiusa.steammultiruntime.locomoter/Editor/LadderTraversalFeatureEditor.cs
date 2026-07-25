using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(LadderTraversalFeature))]
    public class LadderTraversalFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "梯子昇降を処理するコンポーネントです。\n\n" +
                "シーン上の LadderVolume（Trigger Collider）に侵入すると自動的に昇降モードに入ります。\n" +
                "• 上下入力: 梯子を昇降\n" +
                "• 梯子から離れると通常移動に戻ります",
                MessageType.Info);

            EditorGUILayout.Space();

            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(LadderVolume))]
    public class LadderVolumeEditor : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var ladder = (LadderVolume)target;
            var col = ladder.GetComponent<Collider>();
            if (col == null) return;

            // 梯子の上方向を矢印で表示
            var bounds = col.bounds;
            var center = bounds.center;
            var top = center + ladder.UpDirection * (bounds.extents.y + 0.3f);

            Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.9f);
            Handles.ArrowHandleCap(0, center, Quaternion.LookRotation(ladder.UpDirection), 1.0f, EventType.Repaint);

            Handles.color = new Color(0.2f, 0.8f, 0.2f, 0.3f);
            Handles.DrawWireCube(bounds.center, bounds.size);

            Handles.Label(top, "Ladder\n↑ Up", EditorStyles.boldLabel);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "梯子の当たり判定ボリュームです。\n\n" +
                "• Collider は自動的に isTrigger = true に設定されます\n" +
                "• 上方向は Transform の Up 方向に従います\n" +
                "• LadderTraversalFeature を持つオブジェクトが侵入すると昇降を開始します",
                MessageType.Info);

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
