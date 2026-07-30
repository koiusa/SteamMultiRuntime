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
        private bool resolveLobbyServiceFromRegistry;

        private void Awake()
        {
            resolveLobbyServiceFromRegistry = lobbyService == null;
        }

        private void OnEnable()
        {
            if (resolveLobbyServiceFromRegistry)
            {
                SteamLobbyServiceRegistry.CurrentChanged += OnLobbyServiceChanged;
                SetLobbyService(SteamLobbyServiceRegistry.Current);
            }

            wasActive = IsActive;
            if (lobbyService != null)
            {
                lobbyService.StateChanged -= OnLobbyStateChanged;
                lobbyService.StateChanged += OnLobbyStateChanged;
            }
            BindNetworkEvents();
            BeginResolvePlayer();
        }

        private void OnDisable()
        {
            SteamLobbyServiceRegistry.CurrentChanged -= OnLobbyServiceChanged;
            if (lobbyService != null)
            {
                lobbyService.StateChanged -= OnLobbyStateChanged;
            }
            UnbindNetworkEvents();
            StopResolvePlayer();
        }

        private void OnLobbyServiceChanged(SteamLobbyService service)
        {
            if (!resolveLobbyServiceFromRegistry)
                return;

            SetLobbyService(service);
            OnLobbyStateChanged();
        }

        private void SetLobbyService(SteamLobbyService service)
        {
            if (lobbyService == service)
                return;

            if (isActiveAndEnabled && lobbyService != null)
                lobbyService.StateChanged -= OnLobbyStateChanged;

            lobbyService = service;

            if (isActiveAndEnabled && lobbyService != null)
                lobbyService.StateChanged += OnLobbyStateChanged;
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
