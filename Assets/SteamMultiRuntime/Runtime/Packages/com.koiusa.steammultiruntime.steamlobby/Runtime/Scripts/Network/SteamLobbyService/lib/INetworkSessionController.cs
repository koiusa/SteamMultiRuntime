using System;
using System.Threading.Tasks;

namespace Koiusa.SteamMultiRuntime
{
    internal interface INetworkSessionController
    {
        event Action Stopping;

        bool TryActivateBootstrapScene(string excludedPresentationSceneReference);
        bool TryStartHost();
        bool TryStartServer();
        bool TryStartClient(ulong hostSteamId);
        Task<bool> SwitchStageSceneAsync(string targetSceneReference, string previousSceneReference);
        Task<bool> WaitForClientStageSceneAsync(string sceneReference);
        Task<bool> UnloadStageSceneAsync(string sceneReference);
        Task StopAsync();
    }
}
