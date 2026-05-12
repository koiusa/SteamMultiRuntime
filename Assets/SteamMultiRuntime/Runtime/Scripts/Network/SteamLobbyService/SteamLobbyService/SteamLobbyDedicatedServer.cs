using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Koiusa.SteamMultiRuntime.Network;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Koiusa.SteamMultiRuntime;

namespace Koiusa.SteamMultiRuntime.Network
{
    [DisallowMultipleComponent]
    public class SteamLobbyDedicatedServer : SteamLobbySceneLoaderBase
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

        public override string LobbySceneName => ResolveStartupSceneReference();
        public override IReadOnlyList<string> CreatableStageSceneNames => stageSceneList?.sceneNames ?? Array.Empty<string>();

        public override async Task<bool> LoadLobbySceneOnEnteredAsync()
        {
            RaiseLoadingStarted();
            var result = await LoadStartupSceneIfConfiguredAsync();
            RaiseLoadingFinished();
            return result;
        }

        public override void UnloadLobbySceneOnLeft() { }

        public override Task HandleLobbyLeftAsync(string sceneNameToUnload) => Task.CompletedTask;

        public override void SetLobbySceneName(string sceneName)
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

        private async Task<bool> LoadStartupSceneIfConfiguredAsync()
        {
            var startupScene = ResolveStartupSceneReference();
            if (string.IsNullOrWhiteSpace(startupScene))
            {
                Log("Startup scene is empty. Skip scene loading.");
                return true;
            }

            if (!CanLoadScene(startupScene))
            {
                Debug.LogError($"[SteamLobbyDedicatedServer] Scene '{startupScene}' is not in Build Settings.", this);
                return false;
            }

            var loadedScene = GetLoadedScene(startupScene);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                if (setLoadedSceneAsActive)
                {
                    SceneManager.SetActiveScene(loadedScene);
                }

                Log($"Startup scene already loaded: {startupScene}");
                return true;
            }

            var operation = SceneManager.LoadSceneAsync(startupScene, sceneLoadMode);
            if (operation == null)
            {
                Debug.LogError($"[SteamLobbyDedicatedServer] Failed to start loading scene '{startupScene}'.", this);
                return false;
            }

            await WaitForOperationAsync(operation);

            if (setLoadedSceneAsActive)
            {
                var scene = GetLoadedScene(startupScene);
                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.SetActiveScene(scene);
                }
            }

            Log($"Startup scene loaded: {startupScene}");
            return true;
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
            if (stageSceneList == null || stageSceneList.sceneNames == null || stageSceneList.sceneNames.Length == 0)
            {
                return string.Empty;
            }

            if (startupStageSceneIndex < 0 || startupStageSceneIndex >= stageSceneList.sceneNames.Length)
            {
                Debug.LogError($"[SteamLobbyDedicatedServer] startupStageSceneIndex '{startupStageSceneIndex}' is out of range.", this);
                return string.Empty;
            }

            var sceneName = stageSceneList.sceneNames[startupStageSceneIndex];
            var resolved = stageSceneList.ResolveSceneReference(sceneName);
            return !string.IsNullOrWhiteSpace(resolved) ? resolved : sceneName;
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
