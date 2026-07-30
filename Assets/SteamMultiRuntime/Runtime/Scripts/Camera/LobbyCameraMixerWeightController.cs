using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class LobbyCameraMixerWeightController : CameraMixerWeightControllerBase
    {
        [SerializeField] private NetworkFocusMarkerContext networkContext;

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

            Debug.LogWarning(
                "Lobby camera requires a preconfigured NetworkFocusMarkerContext.",
                this);
            return null;
        }
    }
}
