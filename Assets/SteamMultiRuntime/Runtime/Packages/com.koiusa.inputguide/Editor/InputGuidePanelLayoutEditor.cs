using UnityEditor;
using UnityEngine;

namespace Koiusa.InputGuide.Editor
{
    [CustomEditor(typeof(InputGuidePanelLayout))]
    internal sealed class InputGuidePanelLayoutEditor : UnityEditor.Editor
    {
        private static readonly InputGuidePanelAnchor[,] Anchors =
        {
            { InputGuidePanelAnchor.TopLeft, InputGuidePanelAnchor.TopCenter, InputGuidePanelAnchor.TopRight },
            { InputGuidePanelAnchor.MiddleLeft, InputGuidePanelAnchor.Center, InputGuidePanelAnchor.MiddleRight },
            { InputGuidePanelAnchor.BottomLeft, InputGuidePanelAnchor.BottomCenter, InputGuidePanelAnchor.BottomRight }
        };

        private static readonly GUIContent[,] Contents =
        {
            { new("↖", "Top Left"), new("↑", "Top Center"), new("↗", "Top Right") },
            { new("←", "Middle Left"), new("●", "Center"), new("→", "Middle Right") },
            { new("↙", "Bottom Left"), new("↓", "Bottom Center"), new("↘", "Bottom Right") }
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("panelSlot"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("hostElementName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultLayout"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("layoutOverride"));
            DrawAnchorGrid(serializedObject.FindProperty("anchor"));
            var changed = EditorGUI.EndChangeCheck();
            changed |= serializedObject.ApplyModifiedProperties();

            if (!changed || !Application.isPlaying) return;
            foreach (var inspectedTarget in targets)
            {
                var layout = (InputGuidePanelLayout)inspectedTarget;
                layout.Refresh();
                layout.GetComponent<InputGuideOverlay>()?.RefreshFromInspector();
            }
        }

        private static void DrawAnchorGrid(SerializedProperty property)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Anchor", EditorStyles.boldLabel);
            var current = (InputGuidePanelAnchor)property.intValue;
            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(current.ToString()), EditorStyles.miniLabel);
            EditorGUILayout.BeginVertical(GUILayout.Width(96f));
            for (var row = 0; row < 3; row++)
            {
                EditorGUILayout.BeginHorizontal();
                for (var column = 0; column < 3; column++)
                {
                    var value = Anchors[row, column];
                    var style = column == 0 ? EditorStyles.miniButtonLeft
                        : column == 2 ? EditorStyles.miniButtonRight : EditorStyles.miniButtonMid;
                    if (GUILayout.Toggle(current == value, Contents[row, column], style,
                            GUILayout.Width(32f), GUILayout.Height(24f)) && current != value)
                    {
                        property.intValue = (int)value;
                        current = value;
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }
    }
}
