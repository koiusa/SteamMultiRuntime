using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Koiusa.SteamMultiRuntime.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class SteamLobbySceneLoader : SteamLobbySceneLoaderBase, ISteamLobbyTransitionScope
    {
        [Serializable]
        private class SceneCatalogSettings
        {
            [FormerlySerializedAs("defaultSceneName")]
            public string defaultSceneName = "";

            [Tooltip("プロジェクトで管理するScriptableObjectをアサイン")]
            [FormerlySerializedAs("projectStageSceneList")]
            public StageSceneList stageSceneList;
        }

        [Serializable]
        private class DefaultScenePolicySettings
        {
            [FormerlySerializedAs("loadDefaultSceneOnStart")]
            public bool loadOnStart = true;

            [FormerlySerializedAs("unloadDefaultSceneOnEntered")]
            public bool unloadOnLobbyEntered = true;

            [FormerlySerializedAs("loadDefaultSceneOnLeft")]
            public bool loadOnLobbyLeft;
        }

        [Serializable]
        private class LobbyScenePolicySettings
        {
            [FormerlySerializedAs("loadLobbySceneOnEntered")]
            public bool loadOnEntered = true;

            [FormerlySerializedAs("unloadLobbySceneOnLeft")]
            public bool unloadOnLeft = true;
        }

        [Header("References")]
        [SerializeField] private SteamLobbyService lobbyService;

        [Header("Scene Catalog")]
        [SerializeField] private SceneCatalogSettings sceneCatalog = new SceneCatalogSettings();

        [Header("Default Scene Policy")]
        [SerializeField] private DefaultScenePolicySettings defaultScenePolicy = new DefaultScenePolicySettings();

        [Header("Lobby Scene Policy")]
        [SerializeField] private LobbyScenePolicySettings lobbyScenePolicy = new LobbyScenePolicySettings();

        [Header("Loaded Scene Cameras")]
        [SerializeField] private bool disableCamerasInLoadedScenes = true;

        private static SteamLobbySceneLoader instance;

        private int loadingScopeCount;
        private bool didUnloadDefaultSceneForLobby;
        private int directLobbyTransitionScopeCount;
        private string lobbySceneName;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            var persistentRoot = transform.root != null ? transform.root.gameObject : gameObject;
            DontDestroyOnLoad(persistentRoot);

            ResolveLobbyService();
        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        private void Start()
        {
            if (!defaultScenePolicy.loadOnStart)
            {
                return;
            }

            _ = LoadDefaultSceneAsync();
        }

        private void ResolveLobbyService()
        {
            if (lobbyService != null)
            {
                return;
            }

            lobbyService = GetComponent<SteamLobbyService>();
        }

        public override async Task<bool> LoadLobbySceneOnEnteredAsync()
        {
            if (!lobbyScenePolicy.loadOnEntered || string.IsNullOrWhiteSpace(lobbySceneName))
            {
                await UnloadDefaultSceneOnEnteredAsync();
                return true;
            }

            var lobbySceneRef = lobbySceneName;
            if (!CanLoadScene(lobbySceneRef))
            {
                Debug.LogError($"[SteamLobbySceneLoader] Scene '{lobbySceneRef}' is not in Build Settings.");
                return false;
            }

            return await ExecuteWithLoadingScopeAsync(async () =>
            {
                var lobbyScene = GetLoadedScene(lobbySceneRef);
                if (!(lobbyScene.IsValid() && lobbyScene.isLoaded))
                {
                    var operation = SceneManager.LoadSceneAsync(lobbySceneRef, LoadSceneMode.Additive);
                    if (operation == null)
                    {
                        Debug.LogError($"[SteamLobbySceneLoader] Failed to start loading scene '{lobbySceneRef}'.",
                            this);
                        return false;
                    }

                    await WaitForOperationAsync(operation);
                    lobbyScene = GetLoadedScene(lobbySceneRef);
                }

                ApplyLoadedSceneCameraSettings(lobbyScene);
                ActivatePresentationScene(lobbyScene);
                await UnloadDefaultSceneOnEnteredAsync();
                return true;
            });
        }

        private async Task UnloadDefaultSceneOnEnteredAsync()
        {
            if (!defaultScenePolicy.unloadOnLobbyEntered || string.IsNullOrWhiteSpace(sceneCatalog.defaultSceneName))
            {
                return;
            }

            var defaultSceneRef = sceneCatalog.defaultSceneName;
            if (AreSameSceneReference(defaultSceneRef, lobbySceneName))
            {
                return;
            }

            var defaultScene = GetLoadedScene(defaultSceneRef);
            if (!defaultScene.IsValid() || !defaultScene.isLoaded)
            {
                return;
            }

            await WaitForOperationAsync(SceneManager.UnloadSceneAsync(defaultSceneRef));
            didUnloadDefaultSceneForLobby = true;
        }

        public override void UnloadLobbySceneOnLeft()
        {
            _ = ExecuteWithLoadingScopeAsync(() => UnloadLobbySceneOnLeftCoreAsync(null));
        }

        private async Task UnloadLobbySceneOnLeftCoreAsync(string sceneNameOverride)
        {
            var sceneNameToUnload = !string.IsNullOrWhiteSpace(sceneNameOverride)
                ? sceneNameOverride
                : lobbySceneName;

            if (!lobbyScenePolicy.unloadOnLeft || string.IsNullOrWhiteSpace(sceneNameToUnload))
            {
                return;
            }

            var lobbyScene = GetLoadedScene(sceneNameToUnload);
            if (!lobbyScene.IsValid() || !lobbyScene.isLoaded)
            {
                return;
            }

            await WaitForOperationAsync(SceneManager.UnloadSceneAsync(sceneNameToUnload));
        }

        public void LoadDefaultSceneOnLeft()
        {
            if (!defaultScenePolicy.loadOnLobbyLeft)
            {
                return;
            }

            _ = LoadDefaultSceneAsync();
        }

        private async Task LoadDefaultSceneAsync()
        {
            await ExecuteWithLoadingScopeAsync(LoadDefaultSceneCoreAsync);
        }

        private async Task LoadDefaultSceneCoreAsync()
        {
            if (string.IsNullOrWhiteSpace(sceneCatalog.defaultSceneName))
            {
                return;
            }

            var defaultSceneRef = sceneCatalog.defaultSceneName;
            if (!CanLoadScene(defaultSceneRef))
            {
                Debug.LogError($"[SteamLobbySceneLoader] Scene '{defaultSceneRef}' is not in Build Settings.");
                return;
            }

            var defaultScene = GetLoadedScene(defaultSceneRef);
            if (!(defaultScene.IsValid() && defaultScene.isLoaded))
            {
                var operation = SceneManager.LoadSceneAsync(defaultSceneRef, LoadSceneMode.Additive);
                if (operation == null)
                {
                    Debug.LogError($"[SteamLobbySceneLoader] Failed to start loading scene '{defaultSceneRef}'.",
                        this);
                    return;
                }

                await WaitForOperationAsync(operation);
                defaultScene = GetLoadedScene(defaultSceneRef);
            }

            ApplyLoadedSceneCameraSettings(defaultScene);
            ActivatePresentationScene(defaultScene);
            didUnloadDefaultSceneForLobby = false;
        }

        public void HandleLobbyLeft()
        {
            _ = HandleLobbyLeftAsyncInternal(null);
        }

        public override Task HandleLobbyLeftAsync(string sceneNameToUnload)
        {
            return HandleLobbyLeftAsyncInternal(sceneNameToUnload);
        }

        private async Task HandleLobbyLeftAsyncInternal(string sceneNameToUnload)
        {
            await ExecuteWithLoadingScopeAsync(async () =>
            {
                await UnloadLobbySceneOnLeftCoreAsync(sceneNameToUnload);

                if (ShouldLoadDefaultSceneAfterLobbyLeft())
                {
                    await LoadDefaultSceneCoreAsync();
                }
            });
        }

        private bool ShouldLoadDefaultSceneAfterLobbyLeft()
        {
            if (string.IsNullOrWhiteSpace(sceneCatalog.defaultSceneName))
            {
                return false;
            }

            if (defaultScenePolicy.loadOnLobbyLeft || didUnloadDefaultSceneForLobby)
            {
                return true;
            }

            if (!defaultScenePolicy.unloadOnLobbyEntered)
            {
                return false;
            }

            var defaultScene = GetLoadedScene(sceneCatalog.defaultSceneName);
            return !defaultScene.IsValid() || !defaultScene.isLoaded;
        }

        private async Task<bool> ExecuteWithLoadingScopeAsync(Func<Task<bool>> action)
        {
            BeginLoadingScope();
            try
            {
                return await action();
            }
            finally
            {
                EndLoadingScope();
            }
        }

        private async Task ExecuteWithLoadingScopeAsync(Func<Task> action)
        {
            BeginLoadingScope();
            try
            {
                await action();
            }
            finally
            {
                EndLoadingScope();
            }
        }

        private void BeginLoadingScope()
        {
            loadingScopeCount++;
            if (loadingScopeCount == 1)
            {
                RaiseLoadingStarted();
            }
        }

        private void EndLoadingScope()
        {
            if (loadingScopeCount <= 0)
            {
                loadingScopeCount = 0;
                return;
            }

            loadingScopeCount--;
            if (loadingScopeCount == 0)
            {
                RaiseLoadingFinished();
            }
        }

        private void ApplyLoadedSceneCameraSettings(Scene scene)
        {
            if (!disableCamerasInLoadedScenes || !scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (var rootGameObject in scene.GetRootGameObjects())
            {
                foreach (var camera in rootGameObject.GetComponentsInChildren<Camera>(true))
                {
                    camera.enabled = false;

                    if (camera.gameObject.activeSelf)
                    {
                        camera.gameObject.SetActive(false);
                    }
                }
            }
        }

        private static void ActivatePresentationScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            SceneManager.SetActiveScene(scene);
            DynamicGI.UpdateEnvironment();
        }

        private static bool AreSameSceneReference(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return string.Equals(SteamLobbySceneUtility.ToSceneName(left), SteamLobbySceneUtility.ToSceneName(right), StringComparison.OrdinalIgnoreCase);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public override string LobbySceneName => lobbySceneName;

        public override void SetLobbySceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            lobbySceneName = sceneCatalog.stageSceneList != null
                ? sceneCatalog.stageSceneList.ResolveSceneReference(sceneName)
                : sceneName;
        }

        public override IReadOnlyList<string> CreatableStageSceneNames =>
            (sceneCatalog.stageSceneList != null && sceneCatalog.stageSceneList.sceneNames != null)
                ? sceneCatalog.stageSceneList.sceneNames
                : Array.Empty<string>();

        public bool IsDirectLobbyTransitionInProgress => directLobbyTransitionScopeCount > 0;

        public void BeginDirectLobbyTransitionScope()
        {
            directLobbyTransitionScopeCount++;
        }

        public void EndDirectLobbyTransitionScope()
        {
            if (directLobbyTransitionScopeCount <= 0)
            {
                directLobbyTransitionScopeCount = 0;
                return;
            }

            directLobbyTransitionScopeCount--;
        }
    }
}
