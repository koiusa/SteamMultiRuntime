using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class LocalSceneFlowLoader : MonoBehaviour, ISceneLoadContext, ISteamLobbySceneLoader
    {
        [Header("Scenes")]
        [SerializeField] private string defaultSceneName = "";
        [SerializeField] private string lobbySceneName = "";

        [Header("Policy")]
        [SerializeField] private bool disableCamerasInLoadedScenes = true;
        [SerializeField] private bool unloadDefaultSceneOnLobbyEntered = true;
        [SerializeField] private bool loadDefaultSceneOnLobbyLeft = true;
        [SerializeField] private bool unloadLobbySceneOnLeft = true;

        public event Action LoadingStarted;
        public event Action LoadingFinished;

        public string DefaultSceneName => defaultSceneName;
        public string LobbySceneName => lobbySceneName;
        public bool DisableCamerasInLoadedScenes => disableCamerasInLoadedScenes;
        public bool UnloadDefaultSceneOnLobbyEntered => unloadDefaultSceneOnLobbyEntered;
        public bool LoadDefaultSceneOnLobbyLeft => loadDefaultSceneOnLobbyLeft;
        public bool ShouldUnloadLobbySceneOnLeft => unloadLobbySceneOnLeft;

        public IReadOnlyList<string> CreatableStageSceneNames => Array.Empty<string>();

        public Task<bool> LoadLobbySceneAsync()
        {
            return SceneLoadUtility.LoadPresentationSceneAsync(lobbySceneName, this, this, nameof(LocalSceneFlowLoader));
        }

        public Task<bool> LoadDefaultSceneAsync()
        {
            return SceneLoadUtility.LoadPresentationSceneAsync(defaultSceneName, this, this, nameof(LocalSceneFlowLoader));
        }

        public Task<bool> UnloadLobbySceneAsync()
        {
            return SceneLoadUtility.UnloadSceneAsync(lobbySceneName);
        }

        public Task<bool> UnloadDefaultSceneAsync()
        {
            return SceneLoadUtility.UnloadSceneAsync(defaultSceneName);
        }

        public async Task<bool> LoadLobbySceneOnEnteredAsync()
        {
            LoadingStarted?.Invoke();
            try
            {
                return await LoadLobbySceneAsync();
            }
            finally
            {
                LoadingFinished?.Invoke();
            }
        }

        public void UnloadLobbySceneOnLeft()
        {
            _ = UnloadLobbySceneAsync();
        }

        public async Task HandleLobbyLeftAsync(string sceneNameToUnload)
        {
            LoadingStarted?.Invoke();
            try
            {
                if (unloadLobbySceneOnLeft)
                {
                    var targetScene = string.IsNullOrWhiteSpace(sceneNameToUnload) ? lobbySceneName : sceneNameToUnload;
                    if (!string.IsNullOrWhiteSpace(targetScene))
                    {
                        await SceneLoadUtility.UnloadSceneAsync(targetScene);
                    }
                }

                if (loadDefaultSceneOnLobbyLeft && !string.IsNullOrWhiteSpace(defaultSceneName))
                {
                    await LoadDefaultSceneAsync();
                }
            }
            finally
            {
                LoadingFinished?.Invoke();
            }
        }

        public void SetLobbySceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            lobbySceneName = sceneName;
        }
    }
}
