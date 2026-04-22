using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Koiusa.SteamMultiRuntime
{
    public abstract class SteamLobbySceneLoaderBase : MonoBehaviour, ISteamLobbySceneLoader
    {
        public event Action LoadingStarted;
        public event Action LoadingFinished;

        public abstract string LobbySceneName { get; }
        public abstract IReadOnlyList<string> CreatableStageSceneNames { get; }

        public abstract Task<bool> LoadLobbySceneOnEnteredAsync();
        public abstract void UnloadLobbySceneOnLeft();
        public abstract Task HandleLobbyLeftAsync(string sceneNameToUnload);
        public abstract void SetLobbySceneName(string sceneName);

        protected void RaiseLoadingStarted() => LoadingStarted?.Invoke();
        protected void RaiseLoadingFinished() => LoadingFinished?.Invoke();

        protected static Task WaitForOperationAsync(AsyncOperation operation)
            => SteamLobbySceneUtility.WaitForOperationAsync(operation);

        protected static bool CanLoadScene(string sceneReference)
            => SteamLobbySceneUtility.CanLoadScene(sceneReference);

        protected static Scene GetLoadedScene(string sceneReference)
            => SteamLobbySceneUtility.GetLoadedScene(sceneReference);
    }
}
