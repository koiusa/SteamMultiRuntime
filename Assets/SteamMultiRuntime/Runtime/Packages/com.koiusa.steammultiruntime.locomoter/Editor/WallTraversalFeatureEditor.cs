using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(WallTraversalFeature))]
    public sealed class WallTraversalFeatureEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("壁接触を共有し、各Wall Actionへ提供するFeatureです。", MessageType.Info);
            var feature = (WallTraversalFeature)target;
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.ObjectField("Run", feature.GetComponent<WallRunAction>(), typeof(WallRunAction), true);
            EditorGUILayout.ObjectField("Slide", feature.GetComponent<WallSlideAction>(), typeof(WallSlideAction), true);
            EditorGUILayout.ObjectField("Jump", feature.GetComponent<WallJumpAction>(), typeof(WallJumpAction), true);
            EditorGUI.indentLevel--;

            if (!HasCompleteStack(feature.gameObject))
            {
                EditorGUILayout.HelpBox("Wall Actionが不足しています。", MessageType.Error);
                if (GUILayout.Button("Repair Wall Feature")) EnsureStack(feature.gameObject);
            }
        }

        public static bool HasCompleteStack(GameObject gameObject) =>
            gameObject.GetComponent<WallTraversalFeature>() != null
            && gameObject.GetComponent<WallRunAction>() != null
            && gameObject.GetComponent<WallSlideAction>() != null
            && gameObject.GetComponent<WallJumpAction>() != null;

        public static void EnsureStack(GameObject gameObject)
        {
            Ensure<WallTraversalFeature>(gameObject);
            Ensure<WallRunAction>(gameObject);
            Ensure<WallSlideAction>(gameObject);
            Ensure<WallJumpAction>(gameObject);
            EditorUtility.SetDirty(gameObject);
        }

        private static T Ensure<T>(GameObject gameObject) where T : Component =>
            gameObject.GetComponent<T>() ?? Undo.AddComponent<T>(gameObject);
    }
}
