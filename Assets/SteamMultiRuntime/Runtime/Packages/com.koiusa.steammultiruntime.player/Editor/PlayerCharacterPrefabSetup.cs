using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal static class PlayerCharacterPrefabSetup
    {
        private const string UpgradeKey = "Koiusa.SteamMultiRuntime.PlayerGameplayPrefabUpgradeV3";
        private const string GameplayInputConfigPath =
            "Assets/SteamMultiRuntime/Runtime/Configs/Input/GameplayInputActionsConfig.asset";
        private const string SkillDefinitionRoot =
            "Assets/SteamMultiRuntime/Runtime/Configs/Player/Skills/";
        private static readonly string[] CharacterPrefabPaths =
        {
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/LocalPlayer_NPC.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer_NPC.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer_WithAnimator.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/LocalPlayer_WithAnimator.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer_Runtime.prefab"
        };

        [InitializeOnLoadMethod]
        private static void ScheduleUpgrade()
        {
            if (!EditorPrefs.GetBool(UpgradeKey, false))
            {
                EditorApplication.delayCall += ApplyUpgradeOnce;
            }
        }

        private static void ApplyUpgradeOnce()
        {
            ApplyToCharacterPrefabs();
            EditorPrefs.SetBool(UpgradeKey, true);
        }

        [MenuItem("Tools/SteamMultiRuntime/Apply Player Gameplay Structure To Character Prefabs")]
        public static void ApplyToCharacterPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var changedCount = 0;
            foreach (var path in CharacterPrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"Player prefab was not found and was skipped: {path}");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!EnsureStructure(root)) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changedCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (changedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"Applied Player gameplay structure to {changedCount} character prefab(s).");
            }
        }

        private static bool EnsureStructure(GameObject root)
        {
            var changed = false;
            changed |= Ensure<PlayerCharacterCoordinator>(root);
            changed |= Ensure<PlayerSkillCoordinator>(root);
            changed |= Ensure<PlayerCombatCoordinator>(root);
            changed |= Ensure<DashSkillFeature>(root);
            changed |= Ensure<SwordAttackSkillFeature>(root);
            changed |= Ensure<GuardSkillFeature>(root);
            changed |= Ensure<HealSkillFeature>(root);
            changed |= Ensure<PlayerHealthFeature>(root);
            changed |= Ensure<PlayerDamageReceiverFeature>(root);
            changed |= Ensure<PlayerHitDetectionFeature>(root);
            changed |= AssignDefinition<DashSkillFeature>(root, "DashSkillDefinition.asset");
            changed |= AssignDefinition<SwordAttackSkillFeature>(root, "SwordAttackSkillDefinition.asset");
            changed |= AssignDefinition<GuardSkillFeature>(root, "GuardSkillDefinition.asset");
            changed |= AssignDefinition<HealSkillFeature>(root, "HealSkillDefinition.asset");
            if (root.GetComponent<LocalPlayerController>() != null)
            {
                changed |= ConfigureSkillInput(root);
            }
            return changed;
        }

        internal static bool ConfigureSkillInput(GameObject root)
        {
            var input = root.GetComponent<PlayerSkillInputController>();
            var changed = false;
            if (input == null)
            {
                input = root.AddComponent<PlayerSkillInputController>();
                changed = true;
            }

            var config = AssetDatabase.LoadAssetAtPath<Koiusa.Input.InputActionsConfig>(GameplayInputConfigPath);
            var serializedInput = new SerializedObject(input);
            var configProperty = serializedInput.FindProperty("inputActionsConfig");
            changed |= SetReference(configProperty, config);
            changed |= SetReference(serializedInput.FindProperty("attackSkill"), LoadDefinition("SwordAttackSkillDefinition.asset"));
            changed |= SetReference(serializedInput.FindProperty("dashSkill"), LoadDefinition("DashSkillDefinition.asset"));
            changed |= SetReference(serializedInput.FindProperty("guardSkill"), LoadDefinition("GuardSkillDefinition.asset"));
            changed |= SetReference(serializedInput.FindProperty("healSkill"), LoadDefinition("HealSkillDefinition.asset"));
            changed |= SetReference(serializedInput.FindProperty("directionReference"), root.transform);
            if (serializedInput.hasModifiedProperties) serializedInput.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static bool AssignDefinition<T>(GameObject root, string assetName) where T : PlayerSkillFeature
        {
            var feature = root.GetComponent<T>();
            if (feature == null) return false;
            var serializedFeature = new SerializedObject(feature);
            var changed = SetReference(serializedFeature.FindProperty("definition"), LoadDefinition(assetName));
            if (changed) serializedFeature.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static PlayerSkillDefinition LoadDefinition(string assetName)
        {
            return AssetDatabase.LoadAssetAtPath<PlayerSkillDefinition>(SkillDefinitionRoot + assetName);
        }

        private static bool SetReference(SerializedProperty property, Object value)
        {
            if (property == null || value == null || property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            return true;
        }

        private static bool Ensure<T>(GameObject root) where T : Component
        {
            if (root.GetComponent<T>() != null) return false;
            root.AddComponent<T>();
            return true;
        }
    }
}
