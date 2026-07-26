using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(PlayerCompositeMotor))]
    public class PlayerCompositeMotorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Motorコンポーネントを追加してプレイヤーの移動機能を拡張できます。\n\n" +
                "• PlayerMotor - 基本的な移動（自動でアタッチされます）\n" +
                "• WallRunTraversalFeature - 壁走り（オプション）\n" +
                "• WallJumpTraversalFeature - 壁ジャンプ（オプション）\n" +
                "• WallSlideTraversalFeature - 壁滑り（オプション）\n" +
                "• LadderTraversalFeature - 梯子昇降（オプション）\n" +
                "• WireSwingTraversalFeature - ワイヤースイング物理（オプション）\n" +
                "  └ WireGrappleTargetingFeature / WireLineVisualFeature - 接続判定 / 描画",
                MessageType.Info);

            EditorGUILayout.Space();

            var compositeMotor = (PlayerCompositeMotor)target;
            var gameObject = compositeMotor.gameObject;

            EditorGUILayout.LabelField("Attached Components", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var playerMotor = gameObject.GetComponent<PlayerMotor>();
            if (playerMotor != null)
            {
                EditorGUILayout.ObjectField("PlayerMotor", playerMotor, typeof(PlayerMotor), allowSceneObjects: true);
            }

            var traversalCoordinator = gameObject.GetComponent<PlayerTraversalCoordinator>();
            DrawComponentRow<PlayerTraversalCoordinator>(gameObject, "Traversal Coordinator");

            if (Application.isPlaying && traversalCoordinator != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.EnumPopup("Current State", traversalCoordinator.CurrentState);
                    EditorGUILayout.FloatField("State Time", traversalCoordinator.StateElapsedTime);
                }
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Coordinator Features", EditorStyles.miniBoldLabel);
            DrawComponentRow<WallRunTraversalFeature>(gameObject, "WallRunTraversalFeature");
            DrawComponentRow<WallJumpTraversalFeature>(gameObject, "WallJumpTraversalFeature");
            DrawComponentRow<WallSlideTraversalFeature>(gameObject, "WallSlideTraversalFeature");
            DrawComponentRow<LadderTraversalFeature>(gameObject, "LadderTraversalFeature");
            DrawComponentRow<WireSwingTraversalFeature>(gameObject, "WireSwingTraversalFeature");
            DrawReadOnlyComponentRow<WireGrappleTargetingFeature>(gameObject, "  GrappleTargetingFeature");
            DrawReadOnlyComponentRow<WireLineVisualFeature>(gameObject, "  LineVisualFeature");

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            DrawDefaultInspector();
        }

        private static void DrawComponentRow<T>(GameObject gameObject, string label) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(label, component, typeof(T), allowSceneObjects: true);
                if (component == null && GUILayout.Button("Add", GUILayout.Width(50)))
                {
                    Undo.AddComponent<T>(gameObject);
                    EditorUtility.SetDirty(gameObject);

                    if (!Application.isPlaying && gameObject.scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(gameObject.scene);
                    }
                }
            }
        }

        private static void DrawReadOnlyComponentRow<T>(GameObject gameObject, string label) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            EditorGUILayout.ObjectField(label, component, typeof(T), allowSceneObjects: true);
        }
    }
}
