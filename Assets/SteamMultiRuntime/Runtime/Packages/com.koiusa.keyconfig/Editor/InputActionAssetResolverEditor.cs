using Koiusa.Keyconfig.Runtime;
using UnityEditor;
using UnityEngine;

namespace Koiusa.Keyconfig.Editor
{
    [CustomEditor(typeof(InputActionAssetResolver))]
    public sealed class InputActionAssetResolverEditor : UnityEditor.Editor
    {
        private SerializedProperty inputActionAsset;
        private SerializedProperty inputActionReference;

        private void OnEnable()
        {
            inputActionAsset = serializedObject.FindProperty("inputActionAsset");
            inputActionReference = serializedObject.FindProperty("inputActionReference");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Input Action Assetは直接指定またはAsset Source Actionから解決します。" +
                "直接指定がNoneでも、Source Actionが設定されていれば正常です。",
                MessageType.Info);

            EditorGUILayout.PropertyField(inputActionAsset);
            EditorGUILayout.PropertyField(inputActionReference);
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
            else if (inputActionAsset.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    $"Asset Source Actionから「{resolved.name}」を解決しています。",
                    MessageType.Info);
            }
            else if (inputActionReference.objectReferenceValue != null)
            {
                EditorGUILayout.HelpBox(
                    "Direct Input Action Assetを使用しています。Asset Source Actionはフォールバックとして待機します。",
                    MessageType.None);
            }
        }
    }
}
