using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Koiusa.SteamMultiRuntime
{
    public interface ISteamLobbySceneLoader
    {
        event Action LoadingStarted;
        event Action LoadingFinished;

        string LobbySceneName { get; }
        IReadOnlyList<string> CreatableStageSceneNames { get; }

        Task<bool> LoadLobbySceneOnEnteredAsync();
        void UnloadLobbySceneOnLeft();
        Task HandleLobbyLeftAsync(string sceneNameToUnload);
        void SetLobbySceneName(string sceneName);
    }
}
