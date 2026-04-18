using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Network
{
    [CreateAssetMenu(fileName = "CharacterModelIdList", menuName = "SteamMultiRuntime/Character Model Id List", order = 100)]
    public class CharacterModelIdList : ScriptableObject
    {
        public string[] modelIds;
    }
}
