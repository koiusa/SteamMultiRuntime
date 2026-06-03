using Koiusa.SteamMultiRuntime.Network;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public static class PlayerModelSyncUtility
    {
        public static void EnsureModelIdList(ref Network.CharacterModelIdList modelIdList)
        {
            if (modelIdList != null)
            {
                return;
            }

            var profile = Object.FindFirstObjectByType<PlayerModelProfileBase>() as IRuntimeUserProfileModelSource;
            if (profile != null)
            {
                modelIdList = profile.ModelIdList;
            }
        }

        public static void EnsurePrefabLoader(GameObject owner, ref ICharacterPrefabLoader prefabLoaderBehaviour)
        {
            var prefabLoader = prefabLoaderBehaviour as ICharacterPrefabLoader;
            if (prefabLoader != null)
            {
                return;
            }

            prefabLoader = owner.GetComponent<ICharacterPrefabLoader>();
            if (prefabLoader == null)
            {
                prefabLoader = owner.AddComponent<CharacterPrefabLoader>();
            }

            prefabLoaderBehaviour = prefabLoader;
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

        public static void ApplyCurrentModel(GameObject owner, ref ICharacterPrefabLoader prefabLoaderBehaviour, string resourceId, string logPrefix)
        {
            EnsurePrefabLoader(owner, ref prefabLoaderBehaviour);
            var prefabLoader = prefabLoaderBehaviour as ICharacterPrefabLoader;
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
