using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerDisplayName : NetworkBehaviour, IPlayerDisplayNameSource,
        IPlayerDisplayNameNotifier, ILocalPlayerOwnershipNotifier
    {
        private readonly NetworkVariable<FixedString64Bytes> displayName = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsAvailable => IsSpawned;
        public bool IsOwnershipResolved => IsSpawned;
        public bool IsLocalOwner => IsSpawned && IsOwner;
        public event System.Action OwnershipChanged;
        public event System.Action DisplayNameChanged;
        public ulong? PlayerId => IsSpawned ? OwnerClientId : null;

        public string DisplayName
        {
            get
            {
                var value = displayName.Value.ToString();
                return string.IsNullOrEmpty(value) ? "Player" : value;
            }
        }

        public override void OnNetworkSpawn()
        {
            displayName.OnValueChanged += OnDisplayNameChanged;
            OwnershipChanged?.Invoke();
            DisplayNameChanged?.Invoke();
            if (IsOwner)
                SubmitDisplayNameServerRpc(PlayerDisplayNameSettings.ResolveLocalDisplayName());
        }

        public override void OnNetworkDespawn()
        {
            displayName.OnValueChanged -= OnDisplayNameChanged;
            OwnershipChanged?.Invoke();
        }

        private void OnDisplayNameChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue) =>
            DisplayNameChanged?.Invoke();

        public override void OnGainedOwnership()
        {
            OwnershipChanged?.Invoke();
        }

        public override void OnLostOwnership()
        {
            OwnershipChanged?.Invoke();
        }

        public void SetLocalDisplayName(string newDisplayName)
        {
            if (!IsOwner)
                return;

            PlayerDisplayNameSettings.SetCustomDisplayName(newDisplayName);
            if (IsSpawned)
                SubmitDisplayNameServerRpc(PlayerDisplayNameSettings.ResolveLocalDisplayName());
        }

        [ServerRpc]
        private void SubmitDisplayNameServerRpc(string requestedName)
        {
            var sanitized = PlayerDisplayNameSettings.Sanitize(requestedName);
            displayName.Value = string.IsNullOrEmpty(sanitized) ? "Player" : sanitized;
        }
    }
}
