namespace Koiusa.SteamMultiRuntime
{
    public interface ISteamLobbyTransitionScope
    {
        bool IsLobbyTransitionInProgress { get; }
        void BeginLobbyTransitionScope();
        void EndLobbyTransitionScope();
    }
}
