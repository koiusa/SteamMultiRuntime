using UnityEditor;
using UnityEngine;

namespace Koiusa.InputGuide.Editor
{
    [CustomEditor(typeof(InputGuideOverlay))]
    internal sealed class InputGuideOverlayEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            var serializedChanged = EditorGUI.EndChangeCheck();
            serializedChanged |= serializedObject.ApplyModifiedProperties();

            if (!Application.isPlaying)
            {
                return;
            }

            var overlay = (InputGuideOverlay)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime", EditorStyles.boldLabel);
            var nextMode = (InputGuideDisplayMode)EditorGUILayout.EnumPopup(
                "Display Mode", overlay.DisplayMode);

            if (serializedChanged)
            {
                overlay.RefreshFromInspector();
            }

            if (nextMode != overlay.DisplayMode)
            {
                overlay.ApplyConfiguration(new InputGuideConfiguration(
                    nextMode,
                    overlay.LayoutPreset,
                    overlay.ToggleHintVisibility));
            }
        }
    }
}
