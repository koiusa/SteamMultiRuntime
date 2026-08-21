using System.Collections.Generic;
using Koiusa.Input;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.System.Input.Editor
{
    [CustomEditor(typeof(GameQuitInputTrigger))]
    internal sealed class GameQuitInputTriggerEditor : UnityEditor.Editor
    {
        private SerializedProperty gameQuitterProperty;
        private SerializedProperty inputActionsConfigProperty;
        private SerializedProperty quitActionPathProperty;

        private void OnEnable()
        {
            gameQuitterProperty = serializedObject.FindProperty("gameQuitter");
            inputActionsConfigProperty = serializedObject.FindProperty("inputActionsConfig");
            quitActionPathProperty = serializedObject.FindProperty("quitActionPath");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(gameQuitterProperty);
            EditorGUILayout.PropertyField(inputActionsConfigProperty);

            if (gameQuitterProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("GameQuitterを設定してください。", MessageType.Error);
            }

            var config = inputActionsConfigProperty.objectReferenceValue as InputActionsConfig;
            var inputActionAsset = GetInputActionAsset(config);
            if (inputActionAsset == null)
            {
                EditorGUILayout.PropertyField(quitActionPathProperty);
                EditorGUILayout.HelpBox(
                    config == null
                        ? "Input Actions Configを設定してください。"
                        : "Input Actions ConfigにInput Action Assetを設定してください。",
                    MessageType.Error);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawActionPopup(inputActionAsset);
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

            var currentPath = quitActionPathProperty.stringValue;
            var selectedIndex = actionPaths.IndexOf(currentPath);
            if (selectedIndex < 0)
            {
                actionPaths.Insert(0, currentPath);
                selectedIndex = 0;
            }

            var nextIndex = EditorGUILayout.Popup("Quit Action", selectedIndex, actionPaths.ToArray());
            var selectedPath = actionPaths[nextIndex];
            quitActionPathProperty.stringValue = selectedPath;

            if (string.IsNullOrWhiteSpace(selectedPath) || inputActionAsset.FindAction(selectedPath, false) == null)
            {
                EditorGUILayout.HelpBox("選択したQuit ActionがInput Action Assetに存在しません。", MessageType.Error);
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
