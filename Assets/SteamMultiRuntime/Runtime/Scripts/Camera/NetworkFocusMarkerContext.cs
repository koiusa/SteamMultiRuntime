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
        public GameObject PlayerObject => NetworkManager.Singleton?.LocalClient?.PlayerObject?.gameObject;

        public event Action StateChanged;

        private bool wasActive;

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
        }

        private void OnDisable()
        {
            if (lobbyService != null)
            {
                lobbyService.StateChanged -= OnLobbyStateChanged;
            }
        }

        private void OnLobbyStateChanged()
        {
            wasActive = IsActive;
            StateChanged?.Invoke();
        }

        private void Update()
        {
            var isActive = IsActive;
            if (isActive == wasActive)
            {
                return;
            }

            wasActive = isActive;
            StateChanged?.Invoke();
        }
    }
}
