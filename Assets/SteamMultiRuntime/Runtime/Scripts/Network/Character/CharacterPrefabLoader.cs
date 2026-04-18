using System;
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
            string path = sourceSettings.resourcePath;
            // "Character/" で始まっていなければ自動付与
            if (!string.IsNullOrEmpty(path) && !path.StartsWith("Character/"))
                path = "Character/" + path;

            GameObject prefab = sourceSettings.characterPrefab;
            if (prefab == null && !string.IsNullOrEmpty(path))
            {
                prefab = Resources.Load<GameObject>(path);
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
