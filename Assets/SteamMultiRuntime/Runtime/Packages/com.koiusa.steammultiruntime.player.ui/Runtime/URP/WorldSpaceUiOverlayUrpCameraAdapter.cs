using Koiusa.SteamMultiRuntime.Player.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Koiusa.SteamMultiRuntime.Player.UI.URP
{
    internal sealed class WorldSpaceUiOverlayUrpCameraAdapter : IWorldSpaceUiOverlayCameraAdapter
    {
        private static readonly WorldSpaceUiOverlayUrpCameraAdapter Instance = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            WorldSpaceUiOverlayCameraAdapterRegistry.Register(Instance);
        }

        public bool IsSourceCamera(Camera camera) =>
            camera.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Base;

        public bool Configure(Camera source, Camera overlay)
        {
            var sourceData = source.GetUniversalAdditionalCameraData();
            var overlayData = overlay.GetUniversalAdditionalCameraData();
            var cameraStack = sourceData.cameraStack;
            if (cameraStack == null)
                return false;

            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderPostProcessing = false;
            if (!cameraStack.Contains(overlay))
                cameraStack.Add(overlay);

            return cameraStack.Contains(overlay);
        }

        public void Release(Camera source, Camera overlay)
        {
            if (source == null || overlay == null)
                return;

            source.GetUniversalAdditionalCameraData().cameraStack?.Remove(overlay);
        }
    }
}
