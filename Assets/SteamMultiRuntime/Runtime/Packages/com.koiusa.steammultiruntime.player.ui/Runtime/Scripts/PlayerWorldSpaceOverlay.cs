using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class PlayerWorldSpaceOverlay : MonoBehaviour
    {
        [Min(0f)] [SerializeField] private float fadeStartDistance = 8f;
        [Min(0f)] [SerializeField] private float fadeEndDistance = 20f;
        [Min(0.01f)] [SerializeField] private float referenceDistance = 5f;
        [Range(1f, 179f)] [SerializeField] private float referenceFieldOfView = 60f;
        [Min(0.01f)] [SerializeField] private float referenceOrthographicSize = 5f;
        [Min(0.01f)] [SerializeField] private float screenSizeMultiplier = 1f;

        private UIDocument document;
        private int originalLayer;
        private Vector3 baseLocalScale;
        private float referenceProjectionScale;

        private void Awake()
        {
            document = GetComponent<UIDocument>();
            originalLayer = gameObject.layer;
            baseLocalScale = transform.localScale;
            CacheProjectionReference();
        }

        private void OnValidate() => CacheProjectionReference();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreLiveInstancesAfterSubsystemReset()
        {
            var instances = FindObjectsByType<PlayerWorldSpaceOverlay>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance == null || !instance.isActiveAndEnabled) continue;
                instance.OnDisable();
                instance.OnEnable();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            WorldSpaceUiOverlayCamera.Register(gameObject, originalLayer);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            WorldSpaceUiOverlayCamera.Unregister(gameObject);
            transform.localScale = baseLocalScale;
        }

        private void CacheProjectionReference()
        {
            referenceProjectionScale = 1f / Mathf.Tan(
                Mathf.Clamp(referenceFieldOfView, 1f, 179f) * 0.5f * Mathf.Deg2Rad);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            var overlayRoot = document?.rootVisualElement?.Q<VisualElement>(className: "player-name-overlay")
                ?? document?.rootVisualElement?.Q<VisualElement>(className: "player-health-overlay");
            if (camera == null || overlayRoot == null || camera.cameraType != CameraType.Game
                || camera.GetComponent<WorldSpaceUiOverlayCameraMarker>() != null) return;

            transform.rotation = camera.transform.rotation;
            var distance = Vector3.Distance(camera.transform.position, transform.position);
            transform.localScale = baseLocalScale * (CalculateScreenSizeScale(camera) * screenSizeMultiplier);
            var fadeRange = Mathf.Max(0.01f, fadeEndDistance - fadeStartDistance);
            overlayRoot.style.opacity = 1f - Mathf.Clamp01((distance - fadeStartDistance) / fadeRange);
        }

        private float CalculateScreenSizeScale(Camera camera)
        {
            if (camera.orthographic) return Mathf.Max(0.01f, camera.orthographicSize / referenceOrthographicSize);
            var toOverlay = transform.position - camera.transform.position;
            var viewDepth = Mathf.Max(camera.nearClipPlane, Vector3.Dot(toOverlay, camera.transform.forward));
            var currentProjectionScale = Mathf.Max(0.0001f, Mathf.Abs(camera.projectionMatrix.m11));
            return Mathf.Max(0.01f, viewDepth * referenceProjectionScale
                / (Mathf.Max(0.01f, referenceDistance) * currentProjectionScale));
        }
    }
}
