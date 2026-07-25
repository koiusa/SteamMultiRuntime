using Koiusa.Keyconfig.Runtime;
using UnityEditor;
using UnityEngine;

namespace Koiusa.Keyconfig.Editor
{
    [CustomEditor(typeof(InputActionAssetResolver))]
    public sealed class InputActionAssetResolverEditor : UnityEditor.Editor
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
            var resolver = (InputActionAssetResolver)target;
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
                    "InputActionAssetを解決できません。Direct Input Action AssetまたはAsset Source Actionを設定してください。",
                    MessageType.Error);
            }
        }
    }
}
