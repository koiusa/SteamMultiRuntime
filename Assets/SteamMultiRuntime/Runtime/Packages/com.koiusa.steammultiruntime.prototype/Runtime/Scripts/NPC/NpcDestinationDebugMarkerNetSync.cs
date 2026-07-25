using System.Collections.Generic;
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
        [Header("Client Visibility")]
        [SerializeField, Min(1f)] private float showDistance = 10f;
        [SerializeField, Min(1f)] private float hideDistance = 12f;
        [SerializeField, Min(0.1f)] private float visibilityUpdateInterval = 0.5f;

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
        private readonly HashSet<ulong> hiddenClients = new HashSet<ulong>();
        private readonly List<ulong> disconnectedClients = new List<ulong>();
        private float nextVisibilityUpdateTime;

        private void Awake()
        {
            controller = GetComponent<NpcNavMeshController>();
            marker = GetComponent<NpcDestinationDebugMarker>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            var interval = Mathf.Max(0.1f, visibilityUpdateInterval);
            nextVisibilityUpdateTime = Time.unscaledTime + interval * (NetworkObjectId % 97 / 97f);

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
            if (!IsSpawned || !IsServer)
                return;

            if (Time.unscaledTime >= nextVisibilityUpdateTime)
            {
                UpdateClientVisibility();
                nextVisibilityUpdateTime = Time.unscaledTime + Mathf.Max(0.1f, visibilityUpdateInterval);
            }

            if (syncedVisible.Value && marker != null && marker.HasArrived())
                syncedVisible.Value = false;
        }

        private void UpdateClientVisibility()
        {
            if (NetworkManager == null || NetworkObject == null)
                return;

            var showDistanceSqr = showDistance * showDistance;
            var effectiveHideDistance = Mathf.Max(showDistance, hideDistance);
            var hideDistanceSqr = effectiveHideDistance * effectiveHideDistance;

            foreach (var pair in NetworkManager.ConnectedClients)
            {
                var clientId = pair.Key;
                if (clientId == Unity.Netcode.NetworkManager.ServerClientId)
                    continue;

                var playerObject = pair.Value.PlayerObject;
                if (playerObject == null)
                    continue;

                var distanceSqr = (playerObject.transform.position - transform.position).sqrMagnitude;
                if (hiddenClients.Contains(clientId))
                {
                    if (distanceSqr <= showDistanceSqr)
                    {
                        NetworkObject.NetworkShow(clientId);
                        hiddenClients.Remove(clientId);
                    }
                }
                else if (distanceSqr >= hideDistanceSqr)
                {
                    NetworkObject.NetworkHide(clientId);
                    hiddenClients.Add(clientId);
                }
            }

            disconnectedClients.Clear();
            foreach (var clientId in hiddenClients)
            {
                if (!NetworkManager.ConnectedClients.ContainsKey(clientId))
                    disconnectedClients.Add(clientId);
            }

            for (var i = 0; i < disconnectedClients.Count; i++)
                hiddenClients.Remove(disconnectedClients[i]);
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
            hiddenClients.Clear();

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
