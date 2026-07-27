using System;
using Steamworks;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class SteamConnection : MonoBehaviour
    {
        [SerializeField] private bool loginOnStart = true;
        [SerializeField] private bool logoutOnDestroy = false;

        public bool IsInitialized => SteamClient.IsValid;
        public event Action Initialized;

        private bool hasPublishedInitialized;

        private void Awake()
        {
            PlayerDisplayNameSettings.PlatformDisplayNameResolver = ResolveSteamDisplayName;
        }

        private void Start()
        {
            if (loginOnStart)
            {
                Initialize();
            }
        }

        private void OnDestroy()
        {
            if (PlayerDisplayNameSettings.PlatformDisplayNameResolver == ResolveSteamDisplayName)
            {
                PlayerDisplayNameSettings.PlatformDisplayNameResolver = null;
            }

            if (logoutOnDestroy)
            {
                Shutdown();
            }
        }

        private static string ResolveSteamDisplayName()
        {
            return SteamClient.IsValid ? SteamClient.Name : string.Empty;
        }

        public bool Initialize()
        {
            if (SteamClient.IsValid)
            {
                if (!hasPublishedInitialized)
                {
                    hasPublishedInitialized = true;
                    Initialized?.Invoke();
                }
                Debug.Log($"Steam session detected: {SteamClient.Name} ({SteamClient.SteamId})");
                return true;
            }

            Debug.LogWarning("Steam is not initialized. Initialize it from NetworkManager/FacepunchTransport first.");
            return false;
        }

        public void Shutdown()
        {
            if (!SteamClient.IsValid)
            {
                return;
            }

            SteamClient.Shutdown();
            Debug.Log("Steam logout.");
        }
    }
}
