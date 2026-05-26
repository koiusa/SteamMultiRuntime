using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class LocalCameraMixerWeightController : CameraMixerWeightControllerBase
    {
        [SerializeField] private LocalFocusMarkerContext localContext;

        protected override IFocusMarkerContext ResolveContext()
        {
            if (localContext != null)
            {
                return localContext;
            }

            localContext = GetComponent<LocalFocusMarkerContext>();
            if (localContext != null)
            {
                return localContext;
            }

            localContext = GetComponentInChildren<LocalFocusMarkerContext>();
            if (localContext != null)
            {
                return localContext;
            }

            localContext = FindFirstObjectByType<LocalFocusMarkerContext>();
            return localContext;
        }
    }
}
