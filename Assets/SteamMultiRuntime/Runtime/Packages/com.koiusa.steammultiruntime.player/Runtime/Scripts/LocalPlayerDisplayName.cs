using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class LocalPlayerDisplayName : MonoBehaviour, IPlayerDisplayNameSource, ILocalPlayerOwnership
    {
        public bool IsAvailable => isActiveAndEnabled;
        public bool IsOwnershipResolved => true;
        public bool IsLocalOwner => true;
        public ulong? PlayerId => null;
        public string DisplayName => PlayerDisplayNameSettings.ResolveLocalDisplayName();
    }
}
