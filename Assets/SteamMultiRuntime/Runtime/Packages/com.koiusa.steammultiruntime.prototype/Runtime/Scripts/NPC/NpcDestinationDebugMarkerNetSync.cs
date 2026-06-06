using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(NpcNavMeshController))]
    [RequireComponent(typeof(NpcDestinationDebugMarker))]
    public class NpcDestinationDebugMarkerNetSync : NetworkBehaviour
    {
        private readonly NetworkVariable<Vector3> syncedDestination = new NetworkVariable<Vector3>(
            Vector3.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NpcNavMeshController controller;
        private NpcDestinationDebugMarker marker;

        private void Awake()
        {
            controller = GetComponent<NpcNavMeshController>();
            marker = GetComponent<NpcDestinationDebugMarker>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (controller != null)
            {
                controller.DestinationSet += OnDestinationSet;
            }

            syncedDestination.OnValueChanged += OnSyncedDestinationChanged;
            if (marker != null)
            {
                marker.SetDestination(syncedDestination.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (controller != null)
            {
                controller.DestinationSet -= OnDestinationSet;
            }

            syncedDestination.OnValueChanged -= OnSyncedDestinationChanged;
            if (marker != null)
            {
                marker.ClearDestination();
            }

            base.OnNetworkDespawn();
        }

        private void OnDestinationSet(Vector3 destination)
        {
            if (!IsServer)
            {
                return;
            }

            syncedDestination.Value = destination;
        }

        private void OnSyncedDestinationChanged(Vector3 previousValue, Vector3 newValue)
        {
            if (marker != null)
            {
                marker.SetDestination(newValue);
            }
        }
    }
}
