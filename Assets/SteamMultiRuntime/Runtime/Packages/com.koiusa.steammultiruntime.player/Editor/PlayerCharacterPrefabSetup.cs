using UnityEditor;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal static class PlayerCharacterPrefabSetup
    {
        private const string UpgradeKey = "Koiusa.SteamMultiRuntime.PlayerGameplayPrefabUpgradeV1";
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
            return changed;
        }

        private static bool Ensure<T>(GameObject root) where T : Component
        {
            if (root.GetComponent<T>() != null) return false;
            root.AddComponent<T>();
            return true;
        }
    }
}
