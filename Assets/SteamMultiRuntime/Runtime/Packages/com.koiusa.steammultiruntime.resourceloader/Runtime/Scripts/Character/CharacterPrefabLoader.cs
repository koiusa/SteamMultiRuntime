using System;
using Koiusa.SteamMultiRuntime.Character;
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
    public class CharacterPrefabLoader : MonoBehaviour, ICharacterPrefabLoader
    {
        public GameObject LoadedPrefab { get; private set; }
        public GameObject LastInstantiatedObject { get; private set; }
        public GameObject InstantiatedCharacter => LastInstantiatedObject;
        public bool IsLoaded => LoadedPrefab != null;
        public bool IsCharacterReady => LastInstantiatedObject != null && LastInstantiatedObject.activeInHierarchy;

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

            var isSamePrefab = LoadedPrefab == prefab;
            LoadedPrefab = prefab;

            if (LastInstantiatedObject != null)
            {
                Destroy(LastInstantiatedObject);
                LastInstantiatedObject = null;
            }

            if (isSamePrefab)
            {
                return;
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

            if (LastInstantiatedObject != null)
            {
                Destroy(LastInstantiatedObject);
                LastInstantiatedObject = null;
            }

            var instance = Instantiate(LoadedPrefab, position, rotation, parent);
            LastInstantiatedObject = instance;
            PrefabInstantiated?.Invoke(instance);
            return instance;
        }

    }
}
