using System.Collections.Generic;
using Koiusa.SteamMultiRuntime.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    public interface IWorldSpaceUiOverlayCameraAdapter
    {
        bool IsSourceCamera(Camera camera);
        bool Configure(Camera source, Camera overlay);
        void Release(Camera source, Camera overlay);
    }

    public static class WorldSpaceUiOverlayCameraAdapterRegistry
    {
        public static IWorldSpaceUiOverlayCameraAdapter Current { get; private set; }

        public static void Register(IWorldSpaceUiOverlayCameraAdapter adapter)
        {
            Current = adapter;
            WorldSpaceUiOverlayCamera.OnAdapterChanged();
        }

        public static void Unregister(IWorldSpaceUiOverlayCameraAdapter adapter)
        {
            if (!object.ReferenceEquals(Current, adapter))
                return;

            Current = null;
            WorldSpaceUiOverlayCamera.OnAdapterChanged();
        }
    }

    /// <summary>
    /// Renders registered world-space UI on a depth-cleared camera after each game camera.
    /// Uses only UnityEngine camera APIs so the same path works with URP and HDRP.
    /// </summary>
    internal static class WorldSpaceUiOverlayCamera
    {
        private const int HighestUserLayer = 31;

        private sealed class CameraState
        {
            public Camera Source;
            public Camera Overlay;
            public bool SourceOriginallyRenderedOverlayLayer;
            public bool UsesDedicatedOverlay;
            public IWorldSpaceUiOverlayCameraAdapter AppliedAdapter;
        }

        private static readonly Dictionary<int, CameraState> CameraStates = new();
        private static readonly Dictionary<int, int> SurfaceLayers = new();
        private static readonly List<int> InvalidCameraIds = new();
        private static int registrationCount;
        private static int overlayLayer = -1;
        private static bool overlayLayerResolved;
        private static Transform host;
        private static int OverlayMask => 1 << overlayLayer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            CameraStates.Clear();
            SurfaceLayers.Clear();
            InvalidCameraIds.Clear();
            registrationCount = 0;
            overlayLayer = -1;
            overlayLayerResolved = false;
            host = null;
        }

        internal static void SetHost(Transform value)
        {
            host = value;
            foreach (var state in CameraStates.Values)
            {
                if (state.Overlay != null)
                    state.Overlay.transform.SetParent(host, true);
            }
        }

        internal static void ClearHost(Transform value)
        {
            if (host == value)
                host = null;
        }

        internal static void Register(GameObject surface, int originalLayer)
        {
            Register(surface, originalLayer, TryResolveOverlayLayer);
        }

        internal static void Register(GameObject surface, int originalLayer, System.Func<bool> tryResolveOverlayLayer)
        {
            if (surface == null || tryResolveOverlayLayer == null)
                return;

            var id = surface.GetInstanceID();
            if (SurfaceLayers.ContainsKey(id))
                return;

            SurfaceLayers.Add(id, originalLayer);
            if (!tryResolveOverlayLayer())
                return;

            WorldSpaceUiOverlaySceneRoot.EnsureAvailable();
            surface.layer = overlayLayer;
            registrationCount++;
            if (registrationCount != 1)
                return;

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RegisterExistingGameCameras();
        }

        internal static void Unregister(GameObject surface)
        {
            if (surface == null || !SurfaceLayers.Remove(surface.GetInstanceID(), out var originalLayer))
                return;

            if (surface.layer == overlayLayer)
                surface.layer = originalLayer;

            if (overlayLayer < 0)
                return;

            registrationCount = Mathf.Max(0, registrationCount - 1);
            if (registrationCount != 0)
                return;

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            ReleaseAllCameras();
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game
                || camera.GetComponent<WorldSpaceUiOverlayCameraMarker>() != null)
                return;

            var adapter = WorldSpaceUiOverlayCameraAdapterRegistry.Current;
            if (adapter != null && !adapter.IsSourceCamera(camera))
                return;

            ReleaseInvalidCameras();
            var state = GetOrCreateState(camera);
            Synchronize(state);
        }

        private static void RegisterExistingGameCameras()
        {
            var cameras = Camera.allCameras;
            for (var i = 0; i < cameras.Length; i++)
            {
                var camera = cameras[i];
                if (camera == null || camera.cameraType != CameraType.Game
                    || camera.GetComponent<WorldSpaceUiOverlayCameraMarker>() != null)
                    continue;

                var adapter = WorldSpaceUiOverlayCameraAdapterRegistry.Current;
                if (adapter != null && !adapter.IsSourceCamera(camera))
                    continue;

                Synchronize(GetOrCreateState(camera));
            }
        }

        private static CameraState GetOrCreateState(Camera source)
        {
            var id = source.GetInstanceID();
            if (CameraStates.TryGetValue(id, out var state) && state.Source == source && state.Overlay != null)
                return state;

            var overlayObject = new GameObject($"{source.name} World Space UI Overlay Camera");
            overlayObject.hideFlags = HideFlags.DontSave;
            if (host != null)
                overlayObject.transform.SetParent(host, false);
            else
                Object.DontDestroyOnLoad(overlayObject);

            var overlay = overlayObject.AddComponent<Camera>();
            overlayObject.AddComponent<WorldSpaceUiOverlayCameraMarker>();
            state = new CameraState
            {
                Source = source,
                Overlay = overlay,
                SourceOriginallyRenderedOverlayLayer = (source.cullingMask & OverlayMask) != 0
            };
            CameraStates[id] = state;
            return state;
        }

        private static bool TryResolveOverlayLayer()
        {
            if (overlayLayerResolved)
                return overlayLayer >= 0;

            overlayLayerResolved = true;

            overlayLayer = FindHighestUnnamedUserLayer(LayerMask.LayerToName);
            if (overlayLayer >= 0)
                return true;

            Debug.LogWarning(
                "WorldSpaceUiOverlayCamera: No unnamed user layer is available. "
                + "The source camera will render world-space UI without a dedicated depth-cleared overlay.");
            return false;
        }

        internal static int FindHighestUnnamedUserLayer(System.Func<int, string> layerNameResolver)
        {
            if (layerNameResolver == null)
                return -1;

            for (var layer = HighestUserLayer; layer >= 8; layer--)
            {
                if (string.IsNullOrEmpty(layerNameResolver(layer)))
                    return layer;
            }

            return -1;
        }

        internal static void OnAdapterChanged()
        {
            if (registrationCount == 0)
                return;

            ReleaseAllCameras();
            RegisterExistingGameCameras();
        }

        private static void Synchronize(CameraState state)
        {
            var source = state.Source;
            var overlay = state.Overlay;
            if (source == null || overlay == null)
                return;

            overlay.CopyFrom(source);
            overlay.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            // SRPs must not treat this fallback camera as a color-clearing Base camera.
            // Pipeline adapters can clear depth through their native overlay-camera path.
            overlay.clearFlags = CameraClearFlags.Nothing;
            overlay.cullingMask = OverlayMask;
            overlay.depth = source.depth + 1f;
            overlay.useOcclusionCulling = false;
            overlay.enabled = source.isActiveAndEnabled;
            var adapter = WorldSpaceUiOverlayCameraAdapterRegistry.Current;
            var appliedAdapter = state.AppliedAdapter;
            if (appliedAdapter != null && !object.ReferenceEquals(appliedAdapter, adapter))
            {
                appliedAdapter.Release(source, overlay);
                state.AppliedAdapter = null;
                state.UsesDedicatedOverlay = false;
            }

            var previouslyUsedDedicatedOverlay = state.UsesDedicatedOverlay;
            state.UsesDedicatedOverlay = adapter != null && adapter.Configure(source, overlay);
            state.AppliedAdapter = state.UsesDedicatedOverlay ? adapter : null;

            if (state.UsesDedicatedOverlay)
            {
                source.cullingMask &= ~OverlayMask;
            }
            else
            {
                if (previouslyUsedDedicatedOverlay)
                    appliedAdapter?.Release(source, overlay);

                // Never make the UI disappear when a renderer/pipeline cannot provide
                // a depth-cleared overlay path. The source camera remains the fallback.
                overlay.enabled = false;
                source.cullingMask |= OverlayMask;
            }

        }

        private static void ReleaseAllCameras()
        {
            foreach (var state in CameraStates.Values)
            {
                if (state.Source != null)
                {
                    if (state.SourceOriginallyRenderedOverlayLayer)
                        state.Source.cullingMask |= OverlayMask;
                    else
                        state.Source.cullingMask &= ~OverlayMask;
                }
                if (state.Overlay != null)
                {
                    state.AppliedAdapter?.Release(state.Source, state.Overlay);
                    Object.Destroy(state.Overlay.gameObject);
                }
            }

            CameraStates.Clear();
        }

        private static void ReleaseInvalidCameras()
        {
            InvalidCameraIds.Clear();
            foreach (var pair in CameraStates)
            {
                if (pair.Value.Source == null || pair.Value.Overlay == null)
                    InvalidCameraIds.Add(pair.Key);
            }

            for (var i = 0; i < InvalidCameraIds.Count; i++)
            {
                var id = InvalidCameraIds[i];
                if (!CameraStates.Remove(id, out var state))
                    continue;
                if (state.Overlay != null)
                {
                    state.AppliedAdapter?.Release(state.Source, state.Overlay);
                    Object.Destroy(state.Overlay.gameObject);
                }
            }

            InvalidCameraIds.Clear();
        }
    }

    [DisallowMultipleComponent]
    internal sealed class WorldSpaceUiOverlayCameraMarker : MonoBehaviour, IPreservedLoadedSceneCamera
    {
    }
}
