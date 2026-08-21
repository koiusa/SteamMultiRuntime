using UnityEditor;
using UnityEngine;
using Koiusa.Input.Icons;

namespace Koiusa.Input.Icons.Editor
{
    [CustomEditor(typeof(KeyConfigIconSet))]
    public sealed class KeyConfigIconSetEditor : UnityEditor.Editor
    {
        private SerializedProperty customBindingsProperty;

        private void OnEnable()
        {
            customBindingsProperty = serializedObject.FindProperty("customBindings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((ScriptableObject)target), typeof(MonoScript), false);
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(customBindingsProperty, true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
