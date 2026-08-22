using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Koiusa.InputGuide.Editor
{
    [CustomEditor(typeof(InputGuideSelectionController))]
    internal sealed class InputGuideSelectionControllerEditor : UnityEditor.Editor
    {
        private const int MaskFieldLimit = 32;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var overlay = serializedObject.FindProperty("overlay");
            var filter = serializedObject.FindProperty("mapFilter");
            var mapNames = serializedObject.FindProperty("actionMapNames");
            var bindingGroup = serializedObject.FindProperty("bindingGroup");

            EditorGUILayout.PropertyField(overlay);
            var controller = (InputGuideSelectionController)target;
            EditorGUILayout.PropertyField(filter);

            var changed = serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            var availableMapNames = controller.GetAvailableActionMapNames();
            var mapFilter = (InputGuideMapFilter)filter.enumValueIndex;
            var mapsAreEditable = mapFilter == InputGuideMapFilter.Specified;
            using (new EditorGUI.DisabledScope(!mapsAreEditable))
            {
                DrawActionMapMask(availableMapNames, mapNames, mapsAreEditable);
            }

            if (!mapsAreEditable)
            {
                EditorGUILayout.HelpBox(
                    "Set Map Filter to Specified to select individual Action Maps.",
                    MessageType.Info);
            }

            DrawBindingGroup(controller.GetAvailableBindingGroups(), bindingGroup);
            changed |= serializedObject.ApplyModifiedProperties();
            if (changed && Application.isPlaying)
            {
                ((InputGuideSelectionController)target).PushSelection();
            }
        }

        private static void DrawActionMapMask(
            string[] availableNames,
            SerializedProperty selectedNames,
            bool editable)
        {
            if (availableNames.Length == 0)
            {
                EditorGUILayout.HelpBox("Assign an Input Guide Overlay with Input Actions Config to select Action Maps.", MessageType.Info);
                EditorGUILayout.PropertyField(selectedNames, true);
                return;
            }

            if (availableNames.Length > MaskFieldLimit)
            {
                EditorGUILayout.HelpBox(
                    $"Mask selection supports up to {MaskFieldLimit} maps. Edit the stored map-name list directly.",
                    MessageType.Warning);
                EditorGUILayout.PropertyField(selectedNames, true);
                return;
            }

            var currentMask = editable ? 0 : ~0;
            if (editable)
            {
                for (var i = 0; i < availableNames.Length; i++)
                {
                    if (Contains(selectedNames, availableNames[i])) currentMask |= 1 << i;
                }
            }

            var nextMask = EditorGUILayout.MaskField("Action Maps", currentMask, availableNames);
            if (nextMask == currentMask)
            {
                return;
            }

            selectedNames.arraySize = 0;
            for (var i = 0; i < availableNames.Length; i++)
            {
                if ((nextMask & (1 << i)) == 0) continue;
                var index = selectedNames.arraySize++;
                selectedNames.GetArrayElementAtIndex(index).stringValue = availableNames[i];
            }
        }

        private static void DrawBindingGroup(string[] availableGroups, SerializedProperty bindingGroup)
        {
            if (availableGroups.Length == 0)
            {
                EditorGUILayout.PropertyField(bindingGroup);
                return;
            }

            var choices = new List<string> { "All Control Schemes" };
            var values = new List<string> { string.Empty };
            foreach (var group in availableGroups)
            {
                choices.Add(group);
                values.Add(group);
            }

            var currentIndex = values.FindIndex(value =>
                string.Equals(value, bindingGroup.stringValue, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                choices.Add($"Missing: {bindingGroup.stringValue}");
                values.Add(bindingGroup.stringValue);
                currentIndex = values.Count - 1;
            }

            var nextIndex = EditorGUILayout.Popup("Binding Group", currentIndex, choices.ToArray());
            bindingGroup.stringValue = values[nextIndex];
        }

        private static bool Contains(SerializedProperty names, string value)
        {
            for (var i = 0; i < names.arraySize; i++)
            {
                if (string.Equals(names.GetArrayElementAtIndex(i).stringValue, value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
