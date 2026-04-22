namespace Koiusa.SteamMultiRuntime
{
    public interface ISteamLobbyTransitionScope
    {
        bool IsDirectLobbyTransitionInProgress { get; }
        void BeginDirectLobbyTransitionScope();
        void EndDirectLobbyTransitionScope();
    }
}
