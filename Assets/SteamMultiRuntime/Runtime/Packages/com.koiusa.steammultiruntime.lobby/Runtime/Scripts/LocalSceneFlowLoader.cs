using Koiusa.SteamMultiRuntime.Network;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Local版シーン管理コンポーネント
    /// スタートアップシーン読み込みとステージシーン選択に特化
    /// SteamロビーなどのNetwork機能は含まない
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalSceneFlowLoader : MonoBehaviour, ISceneLoadContext, ILoadingSplashEventSource, ISteamLobbySceneLoader, Network.IStartupStageSceneLoaderContext
    {
        [Header("Startup Scene")]
        [SerializeField] private bool loadOnStart = true;
        [SerializeField, Min(0)] private int startupStageSceneIndex;

        [Header("Stage Scenes")]
        [SerializeField] private Network.StageSceneList stageSceneList;

        [Header("Policy")]
        [SerializeField] private LoadSceneMode sceneLoadMode = LoadSceneMode.Single;
        [SerializeField] private bool setLoadedSceneAsActive = true;
        [SerializeField] private bool disableCamerasInLoadedScenes = true;

        public event Action LoadingStarted;
        public event Action LoadingFinished;

        // ISceneLoadContext implementation
        public string DefaultSceneName => string.Empty;
        public string LobbySceneName => string.Empty;
        public bool DisableCamerasInLoadedScenes => disableCamerasInLoadedScenes;
        public bool UnloadDefaultSceneOnLobbyEntered => false;
        public bool LoadDefaultSceneOnLobbyLeft => false;
        public bool ShouldUnloadLobbySceneOnLeft => false;

        // ISteamLobbySceneLoader implementation
        public IReadOnlyList<string> CreatableStageSceneNames
        {
            get
            {
                if (stageSceneList == null)
                {
                    return Array.Empty<string>();
                }

                return stageSceneList.sceneNames ?? Array.Empty<string>();
            }
        }

        // IStartupStageSceneLoaderContext implementation
        public Network.StageSceneList StageSceneList => stageSceneList;
        public int StartupStageSceneIndex => startupStageSceneIndex;
        public LoadSceneMode SceneLoadMode => sceneLoadMode;
        public bool SetLoadedSceneAsActive => setLoadedSceneAsActive;

        private async void Start()
        {
            if (!loadOnStart)
            {
                return;
            }

            await LoadStartupSceneAsync();
        }

        public async Task<bool> LoadStartupSceneAsync()
        {
            LoadingStarted?.Invoke();
            try
            {
                var loaded = await Network.StageStartupSceneLoader.LoadStartupSceneAsync(this, this, nameof(LocalSceneFlowLoader));
                if (!loaded || !disableCamerasInLoadedScenes)
                {
                    return loaded;
                }

                var startupScene = Network.StageStartupSceneLoader.ResolveStartupSceneReference(this, this, nameof(LocalSceneFlowLoader));
                var scene = SceneUtilityEx.GetLoadedScene(startupScene);
                SceneLoadUtility.ApplyLoadedSceneCameraSettings(scene, disableCamerasInLoadedScenes: true);
                return loaded;
            }
            finally
            {
                LoadingFinished?.Invoke();
            }
        }

        // ISteamLobbySceneLoader implementation (not used in Local version)
        public Task<bool> LoadLobbySceneOnEnteredAsync()
        {
            Debug.LogWarning("LocalSceneFlowLoader: LoadLobbySceneOnEnteredAsync is not implemented for Local version.");
            return Task.FromResult(false);
        }

        public void UnloadLobbySceneOnLeft()
        {
            Debug.LogWarning("LocalSceneFlowLoader: UnloadLobbySceneOnLeft is not implemented for Local version.");
        }

        public async Task HandleLobbyLeftAsync(string sceneNameToUnload)
        {
            Debug.LogWarning("LocalSceneFlowLoader: HandleLobbyLeftAsync is not implemented for Local version.");
            await Task.CompletedTask;
        }

        public void SetLobbySceneName(string sceneName)
        {
            Debug.LogWarning("LocalSceneFlowLoader: SetLobbySceneName is not implemented for Local version.");
        }
    }
}
