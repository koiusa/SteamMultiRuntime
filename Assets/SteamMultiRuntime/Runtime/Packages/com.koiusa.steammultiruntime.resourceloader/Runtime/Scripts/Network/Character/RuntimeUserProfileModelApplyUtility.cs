using Koiusa.SteamMultiRuntime.Network;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public static class RuntimeUserProfileModelApplyUtility
    {
        public static bool ApplyToLoader(GameObject target, IRuntimeUserProfileModelSource profile, string logPrefix)
        {
            if (target == null || profile == null)
            {
                return false;
            }

            var modelIdList = profile.ModelIdList;
            var selectedModelIndex = profile.SelectedModelIndex;
            var modelIds = modelIdList != null ? modelIdList.modelIds : null;
            if (modelIds == null || selectedModelIndex < 0 || selectedModelIndex >= modelIds.Length)
            {
                Debug.LogWarning($"[{logPrefix}] Invalid model index or modelIdList not set.");
                return false;
            }

            var sync = target.GetComponent<IPlayerModelSync>();
            if (sync != null)
            {
                sync.ModelIdList = modelIdList;
                sync.ApplyModelIndex(selectedModelIndex);
                return true;
            }

            var loader = target.GetComponent<ICharacterPrefabLoader>();
            if (loader == null)
            {
                return false;
            }

            var modelId = modelIds[selectedModelIndex];
            var resourceId = modelIdList != null ? modelIdList.ResolveResourcePath(modelId) : modelId;
            loader.SetPrefabSource(new CharacterPrefabSourceSettings { characterPrefab = null, resourcePath = resourceId });
            return true;
        }
    }
}
