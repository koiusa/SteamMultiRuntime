using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface ICharacterPrefabLoader
    {
        GameObject LoadedPrefab { get; }
        bool IsLoaded { get; }
        void SetPrefabSource(CharacterPrefabSourceSettings sourceSettings);
        GameObject InstantiateLoaded(Vector3 position, Quaternion rotation, Transform parent = null);
    }
}
