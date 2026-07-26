using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(PlayerCompositeMotor))]
    public class PlayerCompositeMotorEditor : UnityEditor.Editor
    {
        private bool traversalFeaturesExpanded = true;
        private bool wireFeatureExpanded = true;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Motorコンポーネントを追加してプレイヤーの移動機能を拡張できます。\n\n" +
                "• PlayerMotor - 基本的な移動（自動でアタッチされます）\n" +
                "• WallRunTraversalFeature - 壁走り（オプション）\n" +
                "• WallJumpTraversalFeature - 壁ジャンプ（オプション）\n" +
                "• WallSlideTraversalFeature - 壁滑り（オプション）\n" +
                "• LadderTraversalFeature - 梯子昇降（オプション）\n" +
                "• WireConnectionFeature + Wire Actions - ワイヤー接続と操作（オプション）",
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
            traversalFeaturesExpanded = EditorGUILayout.Foldout(
                traversalFeaturesExpanded,
                "Traversal Features",
                true,
                EditorStyles.foldoutHeader);
            if (traversalFeaturesExpanded)
            {
                EditorGUI.indentLevel++;
                DrawComponentRow<WallRunTraversalFeature>(gameObject, "Wall Run");
                DrawComponentRow<WallJumpTraversalFeature>(gameObject, "Wall Jump");
                DrawComponentRow<WallSlideTraversalFeature>(gameObject, "Wall Slide");
                DrawComponentRow<LadderTraversalFeature>(gameObject, "Ladder");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2f);
            wireFeatureExpanded = EditorGUILayout.Foldout(
                wireFeatureExpanded,
                "Wire Connection Feature",
                true,
                EditorStyles.foldoutHeader);
            if (wireFeatureExpanded)
            {
                EditorGUI.indentLevel++;
                DrawComponentRow<WireConnectionFeature>(gameObject, "Feature");
                if (gameObject.GetComponent<WireConnectionFeature>() != null)
                {
                    EditorGUILayout.LabelField("Actions", EditorStyles.miniBoldLabel);
                    EditorGUI.indentLevel++;
                    DrawReadOnlyComponentRow<WireAttachAction>(gameObject, "Attach");
                    DrawReadOnlyComponentRow<WireSwingAction>(gameObject, "Swing");
                    DrawReadOnlyComponentRow<WireReelAction>(gameObject, "Reel");
                    DrawReadOnlyComponentRow<WireGroundAction>(gameObject, "Ground");
                    EditorGUI.indentLevel--;

                    EditorGUILayout.LabelField("Internal Features", EditorStyles.miniBoldLabel);
                    EditorGUI.indentLevel++;
                    DrawReadOnlyComponentRow<WireGrappleTargetingFeature>(gameObject, "Targeting");
                    DrawReadOnlyComponentRow<WireLineVisualFeature>(gameObject, "Line Visual");
                    EditorGUI.indentLevel--;

                    if (!WireConnectionFeatureEditor.HasCompleteStack(gameObject))
                    {
                        EditorGUILayout.HelpBox("Wire Connection Featureの構成が不足しています。", MessageType.Error);
                        if (GUILayout.Button("Repair Wire Connection Feature"))
                            WireConnectionFeatureEditor.EnsureStack(gameObject);
                    }
                }
                EditorGUI.indentLevel--;
            }

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
