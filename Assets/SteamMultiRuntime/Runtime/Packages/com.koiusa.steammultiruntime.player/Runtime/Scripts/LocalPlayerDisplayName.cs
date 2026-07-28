using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class LocalPlayerDisplayName : MonoBehaviour, IPlayerDisplayNameSource
    {
        public bool IsAvailable => isActiveAndEnabled;
        public ulong? PlayerId => null;
        public string DisplayName => PlayerDisplayNameSettings.ResolveLocalDisplayName();
    }
}
