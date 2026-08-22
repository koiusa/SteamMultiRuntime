using System;
using System.Collections.Generic;
using UnityEditor;

namespace Koiusa.InputGuide.Editor
{
    [CustomEditor(typeof(InputGuideNavigationController))]
    internal sealed class InputGuideNavigationControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var overlay = serializedObject.FindProperty("overlay");
            var previousMapAction = serializedObject.FindProperty("previousMapAction");
            var nextMapAction = serializedObject.FindProperty("nextMapAction");
            var scrollAction = serializedObject.FindProperty("scrollAction");
            var scrollStep = serializedObject.FindProperty("scrollStep");

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(overlay);
            var changed = EditorGUI.EndChangeCheck();
            changed |= serializedObject.ApplyModifiedProperties();
            serializedObject.Update();

            var controller = (InputGuideNavigationController)target;
            var availablePaths = controller.GetAvailableActionPaths();
            DrawActionPopup("Previous Map Action", availablePaths, previousMapAction);
            DrawActionPopup("Next Map Action", availablePaths, nextMapAction);
            DrawActionPopup("Scroll Action", controller.GetAvailableScrollActionPaths(), scrollAction);
            EditorGUILayout.PropertyField(scrollStep);
            changed |= serializedObject.ApplyModifiedProperties();
            if (changed && UnityEngine.Application.isPlaying)
            {
                controller.RefreshBindings();
            }
        }

        private static void DrawActionPopup(
            string label,
            string[] availablePaths,
            SerializedProperty actionPath)
        {
            var choices = new List<string> { "None" };
            var values = new List<string> { string.Empty };
            choices.AddRange(availablePaths);
            values.AddRange(availablePaths);

            var currentIndex = values.FindIndex(value =>
                string.Equals(value, actionPath.stringValue, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                choices.Add($"Missing: {actionPath.stringValue}");
                values.Add(actionPath.stringValue);
                currentIndex = values.Count - 1;
            }

            var nextIndex = EditorGUILayout.Popup(label, currentIndex, choices.ToArray());
            actionPath.stringValue = values[nextIndex];
        }
    }
}
