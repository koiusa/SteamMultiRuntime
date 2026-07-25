using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface ICharacterPrefabLoader
    {
        GameObject LoadedPrefab { get; }
        GameObject InstantiatedCharacter { get; }
        bool IsLoaded { get; }
        bool IsCharacterReady { get; }
        void SetPrefabSource(CharacterPrefabSourceSettings sourceSettings);
        GameObject InstantiateLoaded(Vector3 position, Quaternion rotation, Transform parent = null);
    }
}
