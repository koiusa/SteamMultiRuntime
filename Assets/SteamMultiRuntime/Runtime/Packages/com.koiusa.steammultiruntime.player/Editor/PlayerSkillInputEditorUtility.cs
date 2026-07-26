using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal static class PlayerSkillInputEditorUtility
    {
        internal static bool ConfigureSkillInput(GameObject root)
        {
            var input = root.GetComponent<PlayerSkillInputController>();
            var controller = root.GetComponent<LocalPlayerController>();
            if (input == null || controller == null) return false;

            var serializedInput = new SerializedObject(input);
            var changed = false;
            changed |= SetReference(serializedInput.FindProperty("inputActionsConfig"), controller.InputActionsConfig);
            changed |= SetDefinition<SwordAttackSkillFeature>(root, serializedInput, "attackSkill");
            changed |= SetDefinition<DashSkillFeature>(root, serializedInput, "dashSkill");
            changed |= SetDefinition<GuardSkillFeature>(root, serializedInput, "guardSkill");
            changed |= SetDefinition<HealSkillFeature>(root, serializedInput, "healSkill");
            changed |= SetReference(serializedInput.FindProperty("directionReference"), root.transform);
            if (serializedInput.hasModifiedProperties) serializedInput.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool SetDefinition<T>(
            GameObject root,
            SerializedObject serializedInput,
            string propertyName) where T : PlayerSkillFeature
        {
            var definition = root.GetComponent<T>()?.Definition;
            return SetReference(serializedInput.FindProperty(propertyName), definition);
        }

        private static bool SetReference(SerializedProperty property, Object value)
        {
            if (property == null || value == null || property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            return true;
        }
    }
}
