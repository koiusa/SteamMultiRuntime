using Koiusa.Common.System;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class NetworkServerFallRecoveryProcessGate : MonoBehaviour, IFallRecoveryProcessGate
    {
        private NetworkObject networkObject;

        private void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
        }

        public bool ShouldProcess()
        {
            if (networkObject == null)
            {
                return true;
            }

            if (!networkObject.IsSpawned)
            {
                return false;
            }

            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        }
    }
}
