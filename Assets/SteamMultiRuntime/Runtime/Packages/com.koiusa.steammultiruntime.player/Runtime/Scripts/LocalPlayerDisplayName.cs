using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class LocalPlayerDisplayName : MonoBehaviour, IPlayerDisplayNameSource,
        IPlayerDisplayNameNotifier, ILocalPlayerOwnershipNotifier
    {
        public bool IsAvailable => isActiveAndEnabled;
        public bool IsOwnershipResolved => true;
        public bool IsLocalOwner => true;
        public event System.Action OwnershipChanged
        {
            add { }
            remove { }
        }
        public ulong? PlayerId => null;
        public string DisplayName => PlayerDisplayNameSettings.ResolveLocalDisplayName();
        public event System.Action DisplayNameChanged
        {
            add => PlayerDisplayNameSettings.DisplayNameChanged += value;
            remove => PlayerDisplayNameSettings.DisplayNameChanged -= value;
        }
    }
}
