using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(WireConnectionFeature))]
    public sealed class WireConnectionFeatureEditor : UnityEditor.Editor
    {
        private bool actionsExpanded = true;
        private bool internalsExpanded;

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("入力を持たず、接続状態・ロープ制約・表示を管理します。操作は各Wire Actionが担当します。", MessageType.Info);
            DrawDefaultInspector();
            var connection = (WireConnectionFeature)target;
            if (Application.isPlaying)
            {
                using (new EditorGUI.DisabledScope(true)) { EditorGUILayout.Toggle("Attached", connection.IsAttached); EditorGUILayout.Vector3Field("Anchor", connection.AnchorPoint); EditorGUILayout.FloatField("Target Rope Length", connection.RopeLength); EditorGUILayout.FloatField("Actual Length", connection.ActualLength); }
            }
            EditorGUILayout.Space();
            DrawOwnedComponents(connection.gameObject);
            if (!HasCompleteStack(connection.gameObject)
                && GUILayout.Button("Repair Wire Connection Feature"))
                EnsureStack(connection.gameObject);
        }

        [MenuItem("Tools/SteamMultiRuntime/Setup Wire Actions On Selected Player", priority = 120)]
        private static void Setup() { if (Selection.activeGameObject != null) EnsureStack(Selection.activeGameObject); }
        [MenuItem("Tools/SteamMultiRuntime/Setup Wire Actions On Selected Player", true)]
        private static bool ValidateSetup() => Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<PlayerMotor>() != null;

        public static void EnsureStack(GameObject gameObject)
        {
            Ensure<PlayerTraversalCoordinator>(gameObject); Ensure<WireGrappleTargetingFeature>(gameObject); Ensure<WireLineVisualFeature>(gameObject);
            var connection = Ensure<WireConnectionFeature>(gameObject); Ensure<WireAttachAction>(gameObject); Ensure<WireSwingAction>(gameObject); Ensure<WireReelAction>(gameObject); Ensure<WireGroundAction>(gameObject);
            Selection.activeObject = connection; EditorUtility.SetDirty(gameObject);
            if (!Application.isPlaying && gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        public static bool HasCompleteStack(GameObject gameObject) =>
            gameObject.GetComponent<WireConnectionFeature>() != null
            && gameObject.GetComponent<WireAttachAction>() != null
            && gameObject.GetComponent<WireSwingAction>() != null
            && gameObject.GetComponent<WireReelAction>() != null
            && gameObject.GetComponent<WireGroundAction>() != null
            && gameObject.GetComponent<WireGrappleTargetingFeature>() != null
            && gameObject.GetComponent<WireLineVisualFeature>() != null;

        private void DrawOwnedComponents(GameObject gameObject)
        {
            actionsExpanded = EditorGUILayout.Foldout(actionsExpanded, "Actions", true, EditorStyles.foldoutHeader);
            if (actionsExpanded)
            {
                EditorGUI.indentLevel++;
                DrawComponent<WireAttachAction>(gameObject, "Attach");
                DrawComponent<WireSwingAction>(gameObject, "Swing");
                DrawComponent<WireReelAction>(gameObject, "Reel");
                DrawComponent<WireGroundAction>(gameObject, "Ground");
                EditorGUI.indentLevel--;
            }

            internalsExpanded = EditorGUILayout.Foldout(internalsExpanded, "Internal Features", true, EditorStyles.foldoutHeader);
            if (internalsExpanded)
            {
                EditorGUI.indentLevel++;
                DrawComponent<WireGrappleTargetingFeature>(gameObject, "Targeting");
                DrawComponent<WireLineVisualFeature>(gameObject, "Line Visual");
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawComponent<T>(GameObject gameObject, string label) where T : Component =>
            EditorGUILayout.ObjectField(label, gameObject.GetComponent<T>(), typeof(T), true);

        private static T Ensure<T>(GameObject gameObject) where T : Component => gameObject.GetComponent<T>() ?? Undo.AddComponent<T>(gameObject);
    }

    [CustomEditor(typeof(WireGrappleTargetingFeature))]
    public sealed class WireGrappleTargetingFeatureEditor : UnityEditor.Editor { public override void OnInspectorGUI() { EditorGUILayout.HelpBox("WireAttachActionが利用する接続先判定です。", MessageType.Info); DrawDefaultInspector(); } }
    [CustomEditor(typeof(WireLineVisualFeature))]
    public sealed class WireLineVisualFeatureEditor : UnityEditor.Editor { public override void OnInspectorGUI() { EditorGUILayout.HelpBox("WireConnectionFeatureが利用する表示設定です。", MessageType.Info); DrawDefaultInspector(); } }
    [CustomEditor(typeof(WireGroundAction))]
    public sealed class WireGroundActionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("接地中、Dynamic Rigidbodyは振り回し、環境接続ではMaximum Range内をストライフ移動できます。Jumpでは解除しません。", MessageType.Info);
            DrawDefaultInspector();
        }
    }
    [CustomEditor(typeof(WireReelAction))]
    public sealed class WireReelActionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Reel Axisはnegativeで巻き取り、positiveで繰り出します。既定操作はQで巻き取り、Eで繰り出しです。", MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
