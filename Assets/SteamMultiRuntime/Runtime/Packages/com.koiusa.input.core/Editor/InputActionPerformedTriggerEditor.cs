using System.Collections.Generic;
using Koiusa.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Input.Editor
{
    [CustomEditor(typeof(InputActionPerformedTrigger))]
    internal sealed class InputActionPerformedTriggerEditor : UnityEditor.Editor
    {
        private SerializedProperty inputActionsConfigProperty;
        private SerializedProperty actionPathProperty;
        private SerializedProperty performedProperty;

        private void OnEnable()
        {
            inputActionsConfigProperty = serializedObject.FindProperty("inputActionsConfig");
            actionPathProperty = serializedObject.FindProperty("actionPath");
            performedProperty = serializedObject.FindProperty("performed");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(inputActionsConfigProperty);

            var config = inputActionsConfigProperty.objectReferenceValue as InputActionsConfig;
            var inputActionAsset = GetInputActionAsset(config);
            if (inputActionAsset == null)
            {
                EditorGUILayout.PropertyField(actionPathProperty);
                EditorGUILayout.PropertyField(performedProperty);
                EditorGUILayout.HelpBox(
                    config == null
                        ? "Input Actions Configを設定してください。"
                        : "Input Actions ConfigにInput Action Assetを設定してください。",
                    MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawActionPopup(inputActionAsset);
            EditorGUILayout.PropertyField(performedProperty);
            if (GUILayout.Button("Open Input Action Asset"))
            {
                AssetDatabase.OpenAsset(inputActionAsset);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawActionPopup(InputActionAsset inputActionAsset)
        {
            var actionPaths = new List<string>();
            foreach (var map in inputActionAsset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    actionPaths.Add($"{map.name}/{action.name}");
                }
            }

            var currentPath = actionPathProperty.stringValue;
            var selectedIndex = actionPaths.IndexOf(currentPath);
            if (selectedIndex < 0)
            {
                actionPaths.Insert(0, currentPath);
                selectedIndex = 0;
            }

            var nextIndex = EditorGUILayout.Popup("Action", selectedIndex, actionPaths.ToArray());
            var selectedPath = actionPaths[nextIndex];
            actionPathProperty.stringValue = selectedPath;

            if (string.IsNullOrWhiteSpace(selectedPath) || inputActionAsset.FindAction(selectedPath, false) == null)
            {
                EditorGUILayout.HelpBox("選択したActionがInput Action Assetに存在しません。", MessageType.Error);
            }
        }

        private static InputActionAsset GetInputActionAsset(InputActionsConfig config)
        {
            if (config == null)
            {
                return null;
            }

            var configObject = new SerializedObject(config);
            return configObject.FindProperty("inputActionAsset").objectReferenceValue as InputActionAsset;
        }
    }
}
