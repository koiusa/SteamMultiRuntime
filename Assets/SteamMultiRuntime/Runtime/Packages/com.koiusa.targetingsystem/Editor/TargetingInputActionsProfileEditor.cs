using Koiusa.TargetingSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Editor
{
    [CustomEditor(typeof(TargetingInputActionsProfile))]
    public sealed class TargetingInputActionsProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty purpose;
        private SerializedProperty inputActionAsset;

        private void OnEnable()
        {
            purpose = serializedObject.FindProperty("purpose");
            inputActionAsset = serializedObject.FindProperty("inputActionAsset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "TargetingSystemパッケージ単体サンプル用です。ActionとBindingはInput Action Assetを開いて設定します。",
                MessageType.Info);

            EditorGUILayout.PropertyField(purpose);
            EditorGUILayout.PropertyField(inputActionAsset);
            serializedObject.ApplyModifiedProperties();

            var asset = inputActionAsset.objectReferenceValue as InputActionAsset;
            if (asset == null)
            {
                EditorGUILayout.HelpBox("Input Action Assetを設定してください。", MessageType.Error);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions in Asset", EditorStyles.boldLabel);
            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    EditorGUILayout.LabelField($"{map.name}/{action.name}");
                }
            }

            if (GUILayout.Button("Open Input Action Asset"))
            {
                AssetDatabase.OpenAsset(asset);
            }
        }
    }
}
