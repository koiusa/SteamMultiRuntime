using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal static class PlayerModelSyncUtility
    {
        public static void EnsureModelIdList(ref Network.CharacterModelIdList modelIdList)
        {
            if (modelIdList != null)
            {
                return;
            }

            var profile = Object.FindFirstObjectByType<RuntimeUserProfile>();
            if (profile != null)
            {
                modelIdList = profile.ModelIdList;
            }
        }

        public static void EnsurePrefabLoader(GameObject owner, ref CharacterPrefabLoader prefabLoader)
        {
            if (prefabLoader != null)
            {
                return;
            }

            prefabLoader = owner.GetComponent<CharacterPrefabLoader>();
            if (prefabLoader == null)
            {
                prefabLoader = owner.AddComponent<CharacterPrefabLoader>();
            }
        }

        public static string GetCurrentResourceId(Network.CharacterModelIdList modelIdList, int selectedModelIndex)
        {
            var ids = modelIdList != null ? modelIdList.modelIds : null;
            if (ids == null || selectedModelIndex < 0 || selectedModelIndex >= ids.Length)
            {
                return null;
            }

            var modelId = ids[selectedModelIndex];
            return modelIdList != null ? modelIdList.ResolveResourcePath(modelId) : modelId;
        }

        public static void ApplyCurrentModel(GameObject owner, ref CharacterPrefabLoader prefabLoader, string resourceId, string logPrefix)
        {
            EnsurePrefabLoader(owner, ref prefabLoader);
            if (prefabLoader == null || string.IsNullOrEmpty(resourceId))
            {
                return;
            }

            prefabLoader.SetPrefabSource(new CharacterPrefabSourceSettings
            {
                characterPrefab = null,
                resourcePath = resourceId
            });

            if (!prefabLoader.IsLoaded)
            {
                Debug.LogWarning($"[{logPrefix}] Prefab not found for resourceId: {resourceId}. Using default.");
                return;
            }

            prefabLoader.InstantiateLoaded(owner.transform.position, owner.transform.rotation, owner.transform);
        }
    }
}
