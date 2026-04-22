using System;
using Koiusa.SteamMultiRuntime.Network;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [Serializable]
    public struct CharacterPrefabSourceSettings
    {
        public GameObject characterPrefab;
        public string resourcePath;
    }

    [DisallowMultipleComponent]
    public class CharacterPrefabLoader : MonoBehaviour
    {
        public GameObject LoadedPrefab { get; private set; }
        public GameObject LastInstantiatedObject { get; private set; }
        public bool IsLoaded => LoadedPrefab != null;

        public event Action<GameObject> PrefabLoaded;
        public event Action<GameObject> PrefabInstantiated;
        public event Action<string> LoadFailed;

        public void SetPrefabSource(CharacterPrefabSourceSettings sourceSettings)
        {
            var path = sourceSettings.resourcePath;

            var prefab = sourceSettings.characterPrefab;
            if (prefab == null && !string.IsNullOrEmpty(path))
            {
                var resourcesPath = CharacterModelIdList.ToResourcesRelativePath(path);
                prefab = Resources.Load<GameObject>(resourcesPath);
            }

            if (LoadedPrefab == prefab)
                return;

            LoadedPrefab = prefab;
            if (LastInstantiatedObject != null)
            {
                Destroy(LastInstantiatedObject);
                LastInstantiatedObject = null;
            }
            PrefabLoaded?.Invoke(LoadedPrefab);
        }

        public GameObject InstantiateLoaded(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (LoadedPrefab == null)
            {
                var message = "[CharacterPrefabLoader] Prefab is not loaded. SetPrefabSource first.";
                Debug.LogError(message, this);
                LoadFailed?.Invoke(message);
                return null;
            }
            var instance = Instantiate(LoadedPrefab, position, rotation, parent);
            LastInstantiatedObject = instance;
            PrefabInstantiated?.Invoke(instance);
            return instance;
        }
    }
}
