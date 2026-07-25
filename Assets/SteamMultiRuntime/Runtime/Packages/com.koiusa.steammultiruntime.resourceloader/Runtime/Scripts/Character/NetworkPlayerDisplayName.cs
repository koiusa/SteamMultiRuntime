using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class NetworkPlayerDisplayName : NetworkBehaviour, IPlayerDisplayNameSource
    {
        private readonly NetworkVariable<FixedString64Bytes> displayName = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

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
            if (IsOwner)
                SubmitDisplayNameServerRpc(PlayerDisplayNameSettings.ResolveLocalDisplayName());
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
