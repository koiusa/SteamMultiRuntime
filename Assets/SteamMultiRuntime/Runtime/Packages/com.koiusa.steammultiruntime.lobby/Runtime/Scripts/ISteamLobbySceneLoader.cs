using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Koiusa.SteamMultiRuntime
{
    public interface IStageSceneCatalog
    {
        IReadOnlyList<string> CreatableStageSceneNames { get; }
    }

    public interface ISteamLobbySceneLoader : IStageSceneCatalog
    {
        event Action LoadingStarted;
        event Action LoadingFinished;

        string LobbySceneName { get; }
        Task<bool> LoadLobbySceneOnEnteredAsync();
        void UnloadLobbySceneOnLeft();
        Task HandleLobbyLeftAsync(string sceneNameToUnload);
        void SetLobbySceneName(string sceneName);
    }
}
