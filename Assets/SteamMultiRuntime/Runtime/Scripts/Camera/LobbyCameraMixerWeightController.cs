using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class LobbyCameraMixerWeightController : CameraMixerWeightControllerBase
    {
        [SerializeField] private NetworkFocusMarkerContext networkContext;
        [SerializeField] private SteamLobbyService lobbyService;

        protected override IFocusMarkerContext ResolveContext()
        {
            if (networkContext != null)
            {
                return networkContext;
            }

            networkContext = GetComponent<NetworkFocusMarkerContext>();
            if (networkContext != null)
            {
                return networkContext;
            }

            networkContext = GetComponentInChildren<NetworkFocusMarkerContext>();
            if (networkContext != null)
            {
                return networkContext;
            }

            if (lobbyService == null)
            {
                lobbyService = FindFirstObjectByType<SteamLobbyService>();
            }

            if (lobbyService == null)
            {
                return null;
            }

            var autoContext = lobbyService.GetComponent<NetworkFocusMarkerContext>();
            if (autoContext != null)
            {
                networkContext = autoContext;
                return networkContext;
            }

            networkContext = lobbyService.gameObject.AddComponent<NetworkFocusMarkerContext>();
            return networkContext;
        }
    }
}
