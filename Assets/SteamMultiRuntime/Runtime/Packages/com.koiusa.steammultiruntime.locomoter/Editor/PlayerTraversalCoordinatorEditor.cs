using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(PlayerTraversalCoordinator))]
    [CanEditMultipleObjects]
    public sealed class PlayerTraversalCoordinatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "ControllerからTraversal入力を受け取り、各Featureの排他制御・状態照会・Wire同期を集約します。",
                MessageType.Info);

            if (serializedObject.isEditingMultipleObjects)
            {
                DrawDefaultInspector();
                return;
            }

            var coordinator = (PlayerTraversalCoordinator)target;
            var gameObject = coordinator.gameObject;

            DrawRuntimeState(coordinator);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Managed Traversal Features", EditorStyles.boldLabel);
            DrawFeatureRow<WallRunTraversalFeature>(gameObject, "Wall Run");
            DrawFeatureRow<WallJumpTraversalFeature>(gameObject, "Wall Jump");
            DrawFeatureRow<WallSlideTraversalFeature>(gameObject, "Wall Slide");
            DrawFeatureRow<LadderTraversalFeature>(gameObject, "Ladder");
            DrawFeatureRow<WireConnection>(gameObject, "Wire Connection");

            var wire = gameObject.GetComponent<WireConnection>();
            if (wire != null)
            {
                EditorGUI.indentLevel++;
                DrawFeatureRow<WireGrappleTargetingFeature>(gameObject, "Grapple Targeting", false);
                DrawFeatureRow<WireLineVisualFeature>(gameObject, "Line Visual", false);
                DrawFeatureRow<WireAttachAction>(gameObject, "Attach Action", false);
                DrawFeatureRow<WireSwingAction>(gameObject, "Swing Action", false);
                DrawFeatureRow<WireReelAction>(gameObject, "Reel Action", false);
                DrawFeatureRow<WireGroundAction>(gameObject, "Ground Action", false);
                EditorGUI.indentLevel--;

                if (gameObject.GetComponent<WireGrappleTargetingFeature>() == null
                    || gameObject.GetComponent<WireLineVisualFeature>() == null
                    || gameObject.GetComponent<WireAttachAction>() == null
                    || gameObject.GetComponent<WireSwingAction>() == null
                    || gameObject.GetComponent<WireReelAction>() == null
                    || gameObject.GetComponent<WireGroundAction>() == null)
                {
                    EditorGUILayout.HelpBox("Wireの内部Featureが不足しています。", MessageType.Error);
                    if (GUILayout.Button("Repair Wire Feature Stack"))
                    {
                        WireConnectionEditor.EnsureStack(gameObject);
                    }
                }
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private static void DrawRuntimeState(PlayerTraversalCoordinator coordinator)
        {
            EditorGUILayout.LabelField("Coordinator State", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Current State", coordinator.CurrentState);
                EditorGUILayout.Toggle("Traversal Active", coordinator.IsTraversalActive);
                EditorGUILayout.FloatField("State Time", coordinator.StateElapsedTime);
                EditorGUILayout.Toggle("Wire Attached", coordinator.IsWireAttached);
                EditorGUILayout.Toggle("Ground Object Action", coordinator.IsWireGroundActionActive);
                if (coordinator.IsWireAttached)
                {
                    EditorGUILayout.Vector3Field("Wire Anchor", coordinator.WireAnchorPoint);
                    EditorGUILayout.FloatField("Rope Length", coordinator.WireRopeLength);
                }
            }
        }

        private static void DrawFeatureRow<T>(GameObject gameObject, string label, bool allowAdd = true) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(label, component, typeof(T), true);
                if (allowAdd && component == null && GUILayout.Button("Add", GUILayout.Width(48f)))
                {
                    EnsureComponent<T>(gameObject);
                    MarkDirty(gameObject);
                }
            }
        }

        private static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            return gameObject.GetComponent<T>() ?? Undo.AddComponent<T>(gameObject);
        }

        private static void MarkDirty(GameObject gameObject)
        {
            EditorUtility.SetDirty(gameObject);
            if (!Application.isPlaying && gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
    }
}
