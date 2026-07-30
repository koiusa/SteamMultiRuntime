using System;
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
            localManager = LocalManager.Singleton;
            if (localManager != null)
            {
                localManager.PlayerSpawned -= OnPlayerSpawned;
                localManager.PlayerSpawned += OnPlayerSpawned;
                SetPlayer(localManager.LocalPlayerObject);
                return;
            }

            Debug.LogError("LocalFocusMarkerContext requires an active LocalManager.", this);
            SetPlayer(null);
        }

        private void OnDisable()
        {
            if (localManager != null) localManager.PlayerSpawned -= OnPlayerSpawned;
            localManager = null;
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
