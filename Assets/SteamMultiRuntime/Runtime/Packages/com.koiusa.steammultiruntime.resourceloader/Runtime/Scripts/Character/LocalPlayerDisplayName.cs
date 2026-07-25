using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class LocalPlayerDisplayName : MonoBehaviour, IPlayerDisplayNameSource
    {
        public string DisplayName => PlayerDisplayNameSettings.ResolveLocalDisplayName();
    }
}
