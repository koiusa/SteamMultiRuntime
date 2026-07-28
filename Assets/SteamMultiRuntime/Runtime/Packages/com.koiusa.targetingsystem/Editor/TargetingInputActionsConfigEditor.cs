using System.Collections.Generic;
using Koiusa.TargetingSystem.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Editor
{
    [CustomEditor(typeof(TargetingInputActionsConfig))]
    public sealed class TargetingInputActionsConfigEditor : UnityEditor.Editor
    {
        private static readonly string[] ActionPathProperties =
        {
            "lookActionPath",
            "soloLockActionPath",
            "multiLockActionPath",
            "clearLockActionPath",
            "bulkLockActionPath",
            "previousTargetActionPath",
            "nextTargetActionPath",
            "focusActionPath"
        };

        private SerializedProperty inputActionAsset;

        private void OnEnable()
        {
            inputActionAsset = serializedObject.FindProperty("inputActionAsset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Input Action Assetと各ターゲティング操作のAction Pathを設定します。空のPathはその操作を無効化します。",
                MessageType.Info);

            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            var asset = inputActionAsset.objectReferenceValue as InputActionAsset;
            if (asset == null)
            {
                EditorGUILayout.HelpBox("Input Action Assetを設定してください。", MessageType.Error);
                return;
            }

            var missingActions = new List<string>();
            foreach (var propertyName in ActionPathProperties)
            {
                var path = serializedObject.FindProperty(propertyName)?.stringValue;
                if (!string.IsNullOrWhiteSpace(path) && asset.FindAction(path, false) == null)
                {
                    missingActions.Add(path);
                }
            }

            if (missingActions.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Input Action Assetに存在しないAction Pathがあります:\n{string.Join("\n", missingActions)}",
                    MessageType.Warning);
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
