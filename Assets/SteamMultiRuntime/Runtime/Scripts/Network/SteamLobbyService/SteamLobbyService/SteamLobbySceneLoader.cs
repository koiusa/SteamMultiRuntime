using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Koiusa.SteamMultiRuntime.Network;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class SteamLobbySceneLoader : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SteamLobbyService lobbyService;


        [Header("Default Scene")]
        [SerializeField] private bool loadDefaultSceneOnStart = true;
        [SerializeField] private bool unloadDefaultSceneOnEntered = true;
        [SerializeField] private bool loadDefaultSceneOnLeft = false;
        [SerializeField] private string defaultSceneName = "";


        [Header("Stage Scenes")]
        [SerializeField] private bool loadLobbySceneOnEntered = true;
        [SerializeField] private bool unloadLobbySceneOnLeft = true;
        private string lobbySceneName;

        [Tooltip("プロジェクトで管理するScriptableObjectをアサイン")] 
        [SerializeField] private StageSceneList projectStageSceneList;

        [Header("Loaded Scene Cameras")]
        [SerializeField] private bool disableCamerasInLoadedScenes = true;

        private static SteamLobbySceneLoader instance;

        private int loadingScopeCount;
        private bool didUnloadDefaultSceneForLobby;
        private int directLobbyTransitionScopeCount;

        public event Action LoadingStarted;
        public event Action LoadingFinished;

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
            if (!loadDefaultSceneOnStart)
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

        private void SubscribeLobbyServiceEvents()
        {
        }

        private void UnsubscribeLobbyServiceEvents()
        {
        }

        public async Task<bool> LoadLobbySceneOnEnteredAsync()
        {
            if (!loadLobbySceneOnEntered || string.IsNullOrWhiteSpace(lobbySceneName))
            {
                await UnloadDefaultSceneOnEnteredAsync();
                return true;
            }

            if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
            {
                Debug.LogError($"[SteamLobbySceneLoader] Scene '{lobbySceneName}' is not in Build Settings.");
                return false;
            }

            return await ExecuteWithLoadingScopeAsync(async () =>
            {
                var lobbyScene = SceneManager.GetSceneByName(lobbySceneName);
                if (!(lobbyScene.IsValid() && lobbyScene.isLoaded))
                {
                    var operation = SceneManager.LoadSceneAsync(lobbySceneName, LoadSceneMode.Additive);
                    if (operation == null)
                    {
                        Debug.LogError($"[SteamLobbySceneLoader] Failed to start loading scene '{lobbySceneName}'.");
                        return false;
                    }

                    await WaitForOperationAsync(operation);
                    lobbyScene = SceneManager.GetSceneByName(lobbySceneName);
                }

                ApplyLoadedSceneCameraSettings(lobbyScene);
                ActivatePresentationScene(lobbyScene);
                await UnloadDefaultSceneOnEnteredAsync();
                return true;
            });
        }

        private async Task UnloadDefaultSceneOnEnteredAsync()
        {
            if (!unloadDefaultSceneOnEntered || string.IsNullOrWhiteSpace(defaultSceneName))
            {
                return;
            }

            if (defaultSceneName == lobbySceneName)
            {
                return;
            }

            var defaultScene = SceneManager.GetSceneByName(defaultSceneName);
            if (!defaultScene.IsValid() || !defaultScene.isLoaded)
            {
                return;
            }

            await WaitForOperationAsync(SceneManager.UnloadSceneAsync(defaultSceneName));
            didUnloadDefaultSceneForLobby = true;
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

            if (!unloadLobbySceneOnLeft || string.IsNullOrWhiteSpace(sceneNameToUnload))
            {
                return;
            }

            var lobbyScene = SceneManager.GetSceneByName(sceneNameToUnload);
            if (!lobbyScene.IsValid() || !lobbyScene.isLoaded)
            {
                return;
            }

            await WaitForOperationAsync(SceneManager.UnloadSceneAsync(sceneNameToUnload));
        }

        public void LoadDefaultSceneOnLeft()
        {
            if (!loadDefaultSceneOnLeft)
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
            if (string.IsNullOrWhiteSpace(defaultSceneName))
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(defaultSceneName))
            {
                Debug.LogError($"[SteamLobbySceneLoader] Scene '{defaultSceneName}' is not in Build Settings.");
                return;
            }

            var defaultScene = SceneManager.GetSceneByName(defaultSceneName);
            if (!(defaultScene.IsValid() && defaultScene.isLoaded))
            {
                var operation = SceneManager.LoadSceneAsync(defaultSceneName, LoadSceneMode.Additive);
                if (operation == null)
                {
                    Debug.LogError($"[SteamLobbySceneLoader] Failed to start loading scene '{defaultSceneName}'.");
                    return;
                }

                await WaitForOperationAsync(operation);
                defaultScene = SceneManager.GetSceneByName(defaultSceneName);
            }

            ApplyLoadedSceneCameraSettings(defaultScene);
            ActivatePresentationScene(defaultScene);
            didUnloadDefaultSceneForLobby = false;
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
            if (string.IsNullOrWhiteSpace(defaultSceneName))
            {
                return false;
            }

            if (loadDefaultSceneOnLeft || didUnloadDefaultSceneForLobby)
            {
                return true;
            }

            if (!unloadDefaultSceneOnEntered)
            {
                return false;
            }

            var defaultScene = SceneManager.GetSceneByName(defaultSceneName);
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

        private static Task WaitForOperationAsync(AsyncOperation operation)
        {
            if (operation == null || operation.isDone)
            {
                return Task.CompletedTask;
            }

            var completionSource = new TaskCompletionSource<bool>();

            void OnCompleted(AsyncOperation completedOperation)
            {
                operation.completed -= OnCompleted;
                completionSource.TrySetResult(true);
            }

            operation.completed += OnCompleted;

            if (operation.isDone)
            {
                operation.completed -= OnCompleted;
                return Task.CompletedTask;
            }

            return completionSource.Task;
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

            lobbySceneName = sceneName;
        }

        public IReadOnlyList<string> CreatableStageSceneNames =>
            (projectStageSceneList != null && projectStageSceneList.sceneNames != null)
                ? projectStageSceneList.sceneNames
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
