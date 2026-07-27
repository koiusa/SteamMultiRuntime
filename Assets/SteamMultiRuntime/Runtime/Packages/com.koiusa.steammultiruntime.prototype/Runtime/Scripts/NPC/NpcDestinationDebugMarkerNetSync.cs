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

        private readonly NetworkVariable<bool> syncedVisible = new NetworkVariable<bool>(
            false,
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
            syncedVisible.OnValueChanged += OnSyncedVisibleChanged;
            if (marker != null && syncedVisible.Value)
                marker.SetDestination(syncedDestination.Value);
            else
                marker?.ClearDestination();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !syncedVisible.Value || marker == null)
                return;

            if (marker.HasArrived())
                syncedVisible.Value = false;
        }

        public override void OnNetworkDespawn()
        {
            if (controller != null)
            {
                controller.DestinationSet -= OnDestinationSet;
            }

            syncedDestination.OnValueChanged -= OnSyncedDestinationChanged;
            syncedVisible.OnValueChanged -= OnSyncedVisibleChanged;
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
            syncedVisible.Value = true;
        }

        private void OnSyncedDestinationChanged(Vector3 previousValue, Vector3 newValue)
        {
            if (marker != null && syncedVisible.Value)
                marker.SetDestination(newValue);
        }

        private void OnSyncedVisibleChanged(bool previousValue, bool newValue)
        {
            if (marker == null)
                return;

            if (newValue)
                marker.SetDestination(syncedDestination.Value);
            else
                marker.ClearDestination();
        }
    }
}
