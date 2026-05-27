using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime.Network
{
    [DisallowMultipleComponent]
    public class SteamLobbyDedicatedServer : MonoBehaviour, ISteamLobbySceneLoader, IStartupStageSceneLoaderContext
    {
        [Header("Auto Start")]
        [SerializeField] private bool autoStartOnPlay = true;

        [Header("Network")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Lobby")]
        [SerializeField] private SteamLobbyService lobbyService;
        [SerializeField] private bool createLobbyOnStart = true;
        [SerializeField] private string lobbyName = "";

        [Header("Startup Scene")]
        [SerializeField] private bool loadStartupScene = true;
        [SerializeField] private StageSceneList stageSceneList;
        [SerializeField, Min(0)] private int startupStageSceneIndex;
        [SerializeField] private LoadSceneMode sceneLoadMode = LoadSceneMode.Single;
        [SerializeField] private bool setLoadedSceneAsActive = true;
        [SerializeField] private bool failStartupWhenSceneCannotLoad = true;

        [Header("Debug")]
        [SerializeField] private bool enableLogging = true;

        private bool started;

        public event Action LoadingStarted;
        public event Action LoadingFinished;

        public StageSceneList StageSceneList => stageSceneList;
        public int StartupStageSceneIndex => startupStageSceneIndex;
        public LoadSceneMode SceneLoadMode => sceneLoadMode;
        public bool SetLoadedSceneAsActive => setLoadedSceneAsActive;

        public string LobbySceneName => ResolveStartupSceneReference();
        public IReadOnlyList<string> CreatableStageSceneNames => stageSceneList?.sceneNames ?? Array.Empty<string>();

        public async Task<bool> LoadLobbySceneOnEnteredAsync()
        {
            LoadingStarted?.Invoke();
            var result = await LoadStartupSceneIfConfiguredAsync();
            LoadingFinished?.Invoke();
            return result;
        }

        public void UnloadLobbySceneOnLeft() { }

        public Task HandleLobbyLeftAsync(string sceneNameToUnload) => Task.CompletedTask;

        public void SetLobbySceneName(string sceneName)
        {
            if (stageSceneList == null || stageSceneList.sceneNames == null)
                return;
            for (int i = 0; i < stageSceneList.sceneNames.Length; i++)
            {
                if (stageSceneList.sceneNames[i] == sceneName)
                {
                    startupStageSceneIndex = i;
                    break;
                }
            }
        }

        private async void Start()
        {
            if (!autoStartOnPlay || started)
            {
                return;
            }

            started = true;
            ResolveLobbyService();
            await BootstrapDedicatedServerAsync();
        }

        private void ResolveLobbyService()
        {
            if (lobbyService != null)
            {
                return;
            }

            lobbyService = GetComponent<SteamLobbyService>();
            if (lobbyService == null)
            {
                lobbyService = FindFirstObjectByType<SteamLobbyService>(FindObjectsInactive.Include);
            }
        }

        private async Task BootstrapDedicatedServerAsync()
        {
            if (loadStartupScene)
            {
                var loaded = await LoadStartupSceneIfConfiguredAsync();
                if (!loaded && failStartupWhenSceneCannotLoad)
                {
                    return;
                }
            }

            if (createLobbyOnStart)
            {
                await CreateLobbyAsServerAsync();
            }
            else
            {
                TryStartDedicatedServer();
            }
        }

        private async Task CreateLobbyAsServerAsync()
        {
            if (lobbyService == null)
            {
                Debug.LogError("[SteamLobbyDedicatedServer] LobbyService is not found. Cannot create lobby.", this);
                return;
            }

            var name = string.IsNullOrWhiteSpace(lobbyName) ? $"Server_{SystemInfo.deviceName}" : lobbyName;
            var stageSceneName = ResolveStartupSceneReference();

            Log($"Creating lobby as server: {name}");
            var success = await lobbyService.CreateLobbyAsServerAsync(name, stageSceneName);
            if (success)
            {
                Log($"Lobby created as server: {name}");
            }
            else
            {
                Debug.LogError("[SteamLobbyDedicatedServer] Failed to create lobby as server.", this);
            }
        }

        private Task<bool> LoadStartupSceneIfConfiguredAsync()
        {
            return StageStartupSceneLoader.LoadStartupSceneAsync(this, this, nameof(SteamLobbyDedicatedServer), Log);
        }

        private void TryStartDedicatedServer()
        {
            var targetNetworkManager = ResolveNetworkManager();
            if (targetNetworkManager == null)
            {
                Debug.LogError("[SteamLobbyDedicatedServer] NetworkManager is not found.", this);
                return;
            }

            if (targetNetworkManager.IsListening)
            {
                Log("Skip auto start because NetworkManager is already listening.");
                return;
            }

            var success = targetNetworkManager.StartServer();
            if (success)
            {
                Log("Dedicated server started.");
            }
            else
            {
                Debug.LogError("[SteamLobbyDedicatedServer] Failed to start Server.", this);
            }
        }

        private NetworkManager ResolveNetworkManager()
        {
            if (networkManager != null)
            {
                return networkManager;
            }

            if (NetworkManager.Singleton != null)
            {
                return NetworkManager.Singleton;
            }

            networkManager = FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            return networkManager;
        }

        private string ResolveStartupSceneReference()
        {
            return StageStartupSceneLoader.ResolveStartupSceneReference(this, this, nameof(SteamLobbyDedicatedServer));
        }

        private void Log(string message)
        {
            if (enableLogging)
            {
                Debug.Log($"[SteamLobbyDedicatedServer] {message}");
            }
        }
    }
}
