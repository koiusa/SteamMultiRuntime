using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal static class NetworkPlayerSkillPrefabSetup
    {
        private const string UpgradeKey = "Koiusa.SteamMultiRuntime.PlayerNetcodePrefabUpgradeV1";
        private const string GameplayInputConfigPath =
            "Assets/SteamMultiRuntime/Runtime/Configs/Input/GameplayInputActionsConfig.asset";
        private const string SkillDefinitionRoot =
            "Assets/SteamMultiRuntime/Runtime/Configs/Player/Skills/";

        private static readonly string[] NetworkPlayerPrefabPaths =
        {
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer_WithAnimator.prefab",
            "Assets/SteamMultiRuntime/Runtime/Resources/Character/Proxy/NetworkPlayer_Runtime.prefab"
        };

        [InitializeOnLoadMethod]
        private static void ScheduleUpgrade()
        {
            if (!EditorPrefs.GetBool(UpgradeKey, false))
                EditorApplication.delayCall += ApplyUpgradeOnce;
        }

        private static void ApplyUpgradeOnce()
        {
            ApplyToNetworkPlayerPrefabs();
            EditorPrefs.SetBool(UpgradeKey, true);
        }

        [MenuItem("Tools/SteamMultiRuntime/Apply Network Player Skill Input To Prefabs")]
        public static void ApplyToNetworkPlayerPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var changedCount = 0;
            foreach (var path in NetworkPlayerPrefabPaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"Network player prefab was not found and was skipped: {path}");
                    continue;
                }

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (!Configure(root)) continue;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changedCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (changedCount <= 0) return;
            AssetDatabase.SaveAssets();
            Debug.Log($"Applied Network player Skill input to {changedCount} prefab(s).");
        }

        private static bool Configure(GameObject root)
        {
            var input = root.GetComponent<NetworkPlayerSkillController>();
            var changed = false;
            if (input == null)
            {
                input = root.AddComponent<NetworkPlayerSkillController>();
                changed = true;
            }

            var serializedInput = new SerializedObject(input);
            changed |= SetReference(
                serializedInput.FindProperty("inputActionsConfig"),
                AssetDatabase.LoadAssetAtPath<Koiusa.Input.InputActionsConfig>(GameplayInputConfigPath));
            changed |= SetReference(serializedInput.FindProperty("attackSkill"), LoadDefinition("SwordAttackSkillDefinition.asset"));
            changed |= SetReference(serializedInput.FindProperty("dashSkill"), LoadDefinition("DashSkillDefinition.asset"));
            changed |= SetReference(serializedInput.FindProperty("guardSkill"), LoadDefinition("GuardSkillDefinition.asset"));
            changed |= SetReference(serializedInput.FindProperty("healSkill"), LoadDefinition("HealSkillDefinition.asset"));
            changed |= SetReference(serializedInput.FindProperty("directionReference"), root.transform);
            if (serializedInput.hasModifiedProperties) serializedInput.ApplyModifiedPropertiesWithoutUndo();
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
    }
}
