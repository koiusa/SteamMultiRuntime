using System;
using Koiusa.SteamMultiRuntime.Core;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Local プレイヤー用の IFocusMarkerContext 実装。
    /// LocalManager があればそれを優先し、なければ LocalPlayerController を参照する。
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalFocusMarkerContext : MonoBehaviour, IFocusMarkerContext
    {
        private LocalManager localManager;

        public GameObject PlayerObject { get; private set; }
        public bool IsActive => PlayerObject != null;

        public event Action StateChanged;

        private void OnEnable()
        {
            LocalPlayerProviderRegistry.CurrentChanged += OnProviderChanged;
            BindProvider(LocalPlayerProviderRegistry.Current);
        }

        private void OnDisable()
        {
            LocalPlayerProviderRegistry.CurrentChanged -= OnProviderChanged;
            if (localManager != null) localManager.PlayerSpawned -= OnPlayerSpawned;
            localManager = null;
            SetPlayer(null);
        }

        private void OnProviderChanged(ILocalPlayerProvider provider) => BindProvider(provider);

        private void BindProvider(ILocalPlayerProvider provider)
        {
            if (localManager != null) localManager.PlayerSpawned -= OnPlayerSpawned;
            localManager = provider as LocalManager;
            if (localManager != null) localManager.PlayerSpawned += OnPlayerSpawned;
            SetPlayer(provider?.LocalPlayerObject);
        }

        private void OnPlayerSpawned(GameObject player) => SetPlayer(player);

        private void SetPlayer(GameObject player)
        {
            if (PlayerObject == player) return;
            PlayerObject = player;
            StateChanged?.Invoke();
        }
    }
}
