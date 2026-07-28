using System;

namespace Koiusa.SteamMultiRuntime.Network
{
    public interface ILoadingSplashEventSource
    {
        event Action LoadingStarted;
        event Action LoadingFinished;
    }

    public interface ILobbyExitEventSource
    {
        event Action LobbyExitStarted;
        event Action LobbyExitFinished;

        bool IsLobbyExitInProgress { get; }
    }
}
