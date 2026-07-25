using Koiusa.Keyconfig.Runtime;
using UnityEditor;
using UnityEngine;

namespace Koiusa.Keyconfig.Editor
{
    [CustomEditor(typeof(KeyConfigInputActionsConfig))]
    public sealed class KeyConfigInputActionsConfigEditor : UnityEditor.Editor
    {
        private SerializedProperty inputActionAsset;

        private void OnEnable()
        {
            inputActionAsset = serializedObject.FindProperty("inputActionAsset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "キーコンフィグと入力ガイドが参照するInput Action Assetを指定します。",
                MessageType.Info);

            EditorGUILayout.PropertyField(inputActionAsset);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            DrawResolvedAsset();
        }

        private void DrawResolvedAsset()
        {
            var resolver = (KeyConfigInputActionsConfig)target;
            var resolved = resolver.Resolve();

            EditorGUILayout.LabelField("Resolution Result", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Resolved Input Action Asset",
                    resolved,
                    typeof(UnityEngine.InputSystem.InputActionAsset),
                    false);
            }

            if (resolved == null)
            {
                EditorGUILayout.HelpBox(
                    "Input Action Assetを設定してください。",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions in Asset", EditorStyles.boldLabel);
            foreach (var map in resolved.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    EditorGUILayout.LabelField($"{map.name}/{action.name}");
                }
            }

            if (GUILayout.Button("Open Input Action Asset"))
            {
                AssetDatabase.OpenAsset(resolved);
            }
        }
    }
}
