using System;
using System.Threading.Tasks;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Backend-independent control surface for a lobby network session.</summary>
    public interface INetworkSessionController
    {
        event Action Stopping;
        event Action<ulong> ClientDisconnected;
        event Action RemoteSessionEnding;

        bool TryActivateBootstrapScene(string excludedPresentationSceneReference);
        bool TryStartHost();
        bool TryStartServer();
        bool TryStartClient(ulong hostSteamId);
        Task NotifyClientsSessionEndingAsync();
        Task<bool> SwitchStageSceneAsync(string targetSceneReference, string previousSceneReference);
        Task<bool> WaitForClientStageSceneAsync(string sceneReference);
        Task<bool> UnloadStageSceneAsync(string sceneReference);
        Task StopAsync();
    }
}
