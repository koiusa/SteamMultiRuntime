using System;
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

        public bool IsActive => lobbyService != null && lobbyService.IsInLobby;

        public event Action StateChanged;

        private void Awake()
        {
            if (lobbyService == null)
            {
                lobbyService = FindFirstObjectByType<SteamLobbyService>();
            }
        }

        private void OnEnable()
        {
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
            StateChanged?.Invoke();
        }
    }
}
