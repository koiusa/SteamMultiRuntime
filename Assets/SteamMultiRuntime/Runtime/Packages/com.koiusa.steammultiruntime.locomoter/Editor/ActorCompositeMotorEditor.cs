using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(ActorCompositeMotor))]
    public class ActorCompositeMotorEditor : UnityEditor.Editor
    {
        private bool traversalFeaturesExpanded = true;
        private bool wireFeatureExpanded = true;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Motorコンポーネントを追加してプレイヤーの移動機能を拡張できます。\n\n" +
                "• ActorMotor - 基本的な移動（自動でアタッチされます）\n" +
                "• WallTraversalFeature + Wall Actions - 壁移動（オプション）\n" +
                "• LadderTraversalFeature - 梯子昇降（オプション）\n" +
                "• WireTraversalFeature + Wire Actions - ワイヤー接続と操作（オプション）",
                MessageType.Info);

            EditorGUILayout.Space();

            var compositeMotor = (ActorCompositeMotor)target;
            var gameObject = compositeMotor.gameObject;

            EditorGUILayout.LabelField("Attached Components", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var actorMotor = gameObject.GetComponent<ActorMotor>();
            if (actorMotor != null)
            {
                EditorGUILayout.ObjectField("ActorMotor", actorMotor, typeof(ActorMotor), allowSceneObjects: true);
            }

            var traversalCoordinator = gameObject.GetComponent<ActorTraversalCoordinator>();
            DrawComponentRow<ActorTraversalCoordinator>(gameObject, "Traversal Coordinator");

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
                DrawComponentRow<WallTraversalFeature>(gameObject, "Wall Feature");
                DrawComponentRow<WallRunAction>(gameObject, "  Run Action");
                DrawComponentRow<WallJumpAction>(gameObject, "  Jump Action");
                DrawComponentRow<WallSlideAction>(gameObject, "  Slide Action");
                DrawComponentRow<LadderTraversalFeature>(gameObject, "Ladder Feature");
                DrawComponentRow<LadderClimbAction>(gameObject, "  Climb Action");
                DrawComponentRow<LadderDetachAction>(gameObject, "  Detach Action");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2f);
            wireFeatureExpanded = EditorGUILayout.Foldout(
                wireFeatureExpanded,
                "Wire Traversal Feature",
                true,
                EditorStyles.foldoutHeader);
            if (wireFeatureExpanded)
            {
                EditorGUI.indentLevel++;
                DrawComponentRow<WireTraversalFeature>(gameObject, "Feature");
                if (gameObject.GetComponent<WireTraversalFeature>() != null)
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

                    if (!WireTraversalFeatureEditor.HasCompleteStack(gameObject))
                    {
                        EditorGUILayout.HelpBox("Wire Traversal Featureの構成が不足しています。", MessageType.Error);
                        if (GUILayout.Button("Repair Wire Traversal Feature"))
                            WireTraversalFeatureEditor.EnsureStack(gameObject);
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
