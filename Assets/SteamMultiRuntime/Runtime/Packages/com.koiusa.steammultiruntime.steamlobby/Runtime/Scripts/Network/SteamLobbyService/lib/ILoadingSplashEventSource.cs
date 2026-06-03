using System;

namespace Koiusa.SteamMultiRuntime.Network
{
    public interface ILoadingSplashEventSource
    {
        event Action LoadingStarted;
        event Action LoadingFinished;
    }
}
