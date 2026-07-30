using System.Collections;
using System;
using Unity.Netcode;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Network（SteamLobby）用の IFocusMarkerContext 実装。
    /// SteamLobbyService の IsInLobby を IsActive として公開する。
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkFocusMarkerContext : MonoBehaviour, IFocusMarkerContext
    {
        [SerializeField] private SteamLobbyService lobbyService;

        public bool IsActive => lobbyService != null && (lobbyService.IsInLobby || lobbyService.HasLoadedStageScene);
        public GameObject PlayerObject { get; private set; }

        public event Action StateChanged;

        private bool wasActive;
        private NetworkManager subscribedNetworkManager;
        private Coroutine resolvePlayerRoutine;

        private void Awake()
        {
            if (lobbyService == null)
            {
                lobbyService = FindFirstObjectByType<SteamLobbyService>();
            }
        }

        private void OnEnable()
        {
            wasActive = IsActive;
            if (lobbyService != null)
            {
                lobbyService.StateChanged += OnLobbyStateChanged;
            }
            BindNetworkEvents();
            BeginResolvePlayer();
        }

        private void OnDisable()
        {
            if (lobbyService != null)
            {
                lobbyService.StateChanged -= OnLobbyStateChanged;
            }
            UnbindNetworkEvents();
            StopResolvePlayer();
        }

        private void OnLobbyStateChanged()
        {
            wasActive = IsActive;
            StateChanged?.Invoke();
            BindNetworkEvents();
            BeginResolvePlayer();
        }

        private void BindNetworkEvents()
        {
            var manager = NetworkManager.Singleton;
            if (subscribedNetworkManager == manager) return;
            UnbindNetworkEvents();
            subscribedNetworkManager = manager;
            if (subscribedNetworkManager == null) return;
            subscribedNetworkManager.OnClientConnectedCallback += OnClientConnected;
            subscribedNetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void UnbindNetworkEvents()
        {
            if (subscribedNetworkManager == null) return;
            subscribedNetworkManager.OnClientConnectedCallback -= OnClientConnected;
            subscribedNetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            subscribedNetworkManager = null;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (subscribedNetworkManager != null && clientId == subscribedNetworkManager.LocalClientId)
                BeginResolvePlayer();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (subscribedNetworkManager == null || clientId != subscribedNetworkManager.LocalClientId) return;
            StopResolvePlayer();
            SetPlayer(null);
        }

        private void BeginResolvePlayer()
        {
            StopResolvePlayer();
            resolvePlayerRoutine = StartCoroutine(ResolvePlayerAfterSpawn());
        }

        private IEnumerator ResolvePlayerAfterSpawn()
        {
            while (isActiveAndEnabled && IsActive)
            {
                var player = NetworkManager.Singleton?.LocalClient?.PlayerObject?.gameObject;
                if (player != null)
                {
                    SetPlayer(player);
                    resolvePlayerRoutine = null;
                    yield break;
                }
                yield return null;
            }
            SetPlayer(null);
            resolvePlayerRoutine = null;
        }

        private void StopResolvePlayer()
        {
            if (resolvePlayerRoutine == null) return;
            StopCoroutine(resolvePlayerRoutine);
            resolvePlayerRoutine = null;
        }

        private void SetPlayer(GameObject player)
        {
            if (PlayerObject == player) return;
            PlayerObject = player;
            wasActive = IsActive;
            StateChanged?.Invoke();
        }
    }
}
