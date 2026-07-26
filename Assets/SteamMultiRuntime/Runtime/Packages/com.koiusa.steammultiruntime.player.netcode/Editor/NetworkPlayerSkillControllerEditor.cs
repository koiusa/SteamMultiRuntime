using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [CustomEditor(typeof(NetworkPlayerSkillController))]
    public sealed class NetworkPlayerSkillControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            var input = (NetworkPlayerSkillController)target;
            var owner = input.gameObject;
            if (owner.GetComponent<ServerDrivenPlayerController>() == null)
            {
                EditorGUILayout.HelpBox(
                    "ServerDrivenPlayerController is required to copy the gameplay input configuration.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Configure From Player Components"))
                Configure(input);
        }

        private static void Configure(NetworkPlayerSkillController input)
        {
            var owner = input.gameObject;
            var controller = owner.GetComponent<ServerDrivenPlayerController>();
            if (controller == null) return;

            Undo.RecordObject(input, "Configure Network Player Skill Input");
            var serializedInput = new SerializedObject(input);
            SetReference(serializedInput.FindProperty("inputActionsConfig"), controller.InputActionsConfig);
            SetDefinition<SwordAttackSkillFeature>(owner, serializedInput, "attackSkill");
            SetDefinition<DashSkillFeature>(owner, serializedInput, "dashSkill");
            SetDefinition<GuardSkillFeature>(owner, serializedInput, "guardSkill");
            SetDefinition<HealSkillFeature>(owner, serializedInput, "healSkill");
            SetReference(serializedInput.FindProperty("directionReference"), owner.transform);
            serializedInput.ApplyModifiedProperties();

            EditorUtility.SetDirty(input);
            if (!Application.isPlaying && owner.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(owner.scene);
        }

        private static void SetDefinition<T>(
            GameObject owner,
            SerializedObject serializedInput,
            string propertyName) where T : PlayerSkillFeature
        {
            SetReference(
                serializedInput.FindProperty(propertyName),
                owner.GetComponent<T>()?.Definition);
        }

        private static void SetReference(SerializedProperty property, Object value)
        {
            if (property != null && value != null) property.objectReferenceValue = value;
        }
    }
}
