namespace Koiusa.SteamMultiRuntime
{
    public interface ISceneLoadContext
    {
        string DefaultSceneName { get; }
        string LobbySceneName { get; }
        bool DisableCamerasInLoadedScenes { get; }
        bool UnloadDefaultSceneOnLobbyEntered { get; }
        bool LoadDefaultSceneOnLobbyLeft { get; }
        bool ShouldUnloadLobbySceneOnLeft { get; }
    }
}
