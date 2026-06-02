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
    public class SteamLobbySceneLoader : MonoBehaviour, ISteamLobbySceneLoader, ISteamLobbyTransitionScope, ISceneLoadContext
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

        public event Action LoadingStarted;
        public event Action LoadingFinished;

        public string DefaultSceneName => sceneCatalog.defaultSceneName;
        public bool DisableCamerasInLoadedScenes => disableCamerasInLoadedScenes;
        public bool UnloadDefaultSceneOnLobbyEntered => defaultScenePolicy.unloadOnLobbyEntered;
        public bool LoadDefaultSceneOnLobbyLeft => defaultScenePolicy.loadOnLobbyLeft;
        public bool ShouldUnloadLobbySceneOnLeft => lobbyScenePolicy.unloadOnLeft;

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

        public async Task<bool> LoadLobbySceneOnEnteredAsync()
        {
            if (!lobbyScenePolicy.loadOnEntered || string.IsNullOrWhiteSpace(lobbySceneName))
            {
                await UnloadDefaultSceneOnEnteredAsync();
                return true;
            }

            var lobbySceneRef = lobbySceneName;
            return await ExecuteWithLoadingScopeAsync(async () =>
            {
                var loaded = await SceneLoadUtility.LoadPresentationSceneAsync(lobbySceneRef, this, this, nameof(SteamLobbySceneLoader));
                if (!loaded)
                {
                    return false;
                }

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
            if (SceneLoadUtility.AreSameSceneReference(defaultSceneRef, lobbySceneName))
            {
                return;
            }

            if (await SceneLoadUtility.UnloadSceneAsync(defaultSceneRef))
            {
                didUnloadDefaultSceneForLobby = true;
            }
        }

        public void UnloadLobbySceneOnLeft()
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

            await SceneLoadUtility.UnloadSceneAsync(sceneNameToUnload);
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

            var loaded = await SceneLoadUtility.LoadPresentationSceneAsync(sceneCatalog.defaultSceneName, this, this, nameof(SteamLobbySceneLoader));
            if (loaded)
            {
                didUnloadDefaultSceneForLobby = false;
            }
        }

        public void HandleLobbyLeft()
        {
            _ = HandleLobbyLeftAsyncInternal(null);
        }

        public Task HandleLobbyLeftAsync(string sceneNameToUnload)
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

            var defaultScene = SceneUtilityEx.GetLoadedScene(sceneCatalog.defaultSceneName);
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
                LoadingStarted?.Invoke();
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
                LoadingFinished?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public string LobbySceneName => lobbySceneName;

        public void SetLobbySceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            lobbySceneName = sceneCatalog.stageSceneList != null
                ? sceneCatalog.stageSceneList.ResolveSceneReference(sceneName)
                : sceneName;
        }

        public IReadOnlyList<string> CreatableStageSceneNames =>
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
