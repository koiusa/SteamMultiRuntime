using UnityEditor;
using UnityEngine;

namespace Koiusa.Input.Editor
{
    [CustomEditor(typeof(InputActionAssetProfile))]
    public sealed class InputActionAssetProfileEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "通常のActionとBindingはInput Action Assetを開いて設定します。" +
                "Action Overridesは、コードが要求するAction PathがAssetに存在しない場合の例外設定だけです。",
                MessageType.Info);

            DrawDefaultInspector();

            var assetProperty = serializedObject.FindProperty("inputActionAsset");
            var asset = assetProperty.objectReferenceValue as UnityEngine.InputSystem.InputActionAsset;
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
