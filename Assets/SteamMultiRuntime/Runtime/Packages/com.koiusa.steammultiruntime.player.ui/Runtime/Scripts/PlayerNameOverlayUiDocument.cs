using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime.Player.UI
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class PlayerNameOverlayUiDocument : MonoBehaviour
    {
        [Header("Distance Fade")]
        [Min(0f)] [SerializeField] private float fadeStartDistance = 8f;
        [Min(0f)] [SerializeField] private float fadeEndDistance = 20f;

        [Header("Screen Size")]
        [Min(0.01f)] [SerializeField] private float referenceDistance = 5f;
        [Range(1f, 179f)] [SerializeField] private float referenceFieldOfView = 60f;
        [Min(0.01f)] [SerializeField] private float referenceOrthographicSize = 5f;
        [Min(0.01f)] [SerializeField] private float screenSizeMultiplier = 1f;

        private UIDocument uiDocument;
        private Label playerNameLabel;
        private IPlayerIdentitySource identitySource;
        private IPlayerDisplayNameNotifier displayNameNotifier;
        private int originalLayer;
        private Vector3 baseLocalScale;
        private float referenceProjectionScale;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            originalLayer = gameObject.layer;
            baseLocalScale = transform.localScale;
            CacheProjectionReference();
        }

        private void OnValidate() => CacheProjectionReference();

        private void CacheProjectionReference()
        {
            referenceProjectionScale = 1f
                / Mathf.Tan(Mathf.Clamp(referenceFieldOfView, 1f, 179f) * 0.5f * Mathf.Deg2Rad);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreLiveInstancesAfterSubsystemReset()
        {
            // With both Domain Reload and Scene Reload disabled, enabled scene components
            // do not receive OnEnable again although subsystem static state was reset.
            var instances = FindObjectsByType<PlayerNameOverlayUiDocument>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance != null && instance.isActiveAndEnabled)
                    instance.InitializeRuntime();
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            InitializeRuntime();
        }

        private void InitializeRuntime()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            if (displayNameNotifier != null)
                displayNameNotifier.DisplayNameChanged -= RefreshDisplayName;

            uiDocument ??= GetComponent<UIDocument>();
            BindVisualTreeAndRefresh();
            uiDocument?.rootVisualElement?.schedule.Execute(BindVisualTreeAndRefresh);
            identitySource = FindIdentitySource();
            displayNameNotifier = identitySource as IPlayerDisplayNameNotifier;
            if (displayNameNotifier != null)
                displayNameNotifier.DisplayNameChanged += RefreshDisplayName;

            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            WorldSpaceUiOverlayCamera.Register(gameObject, originalLayer);
            RefreshDisplayName();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            WorldSpaceUiOverlayCamera.Unregister(gameObject);
            if (displayNameNotifier != null)
                displayNameNotifier.DisplayNameChanged -= RefreshDisplayName;

            displayNameNotifier = null;
            identitySource = null;
            playerNameLabel = null;
            transform.localScale = baseLocalScale;
        }

        private void BindVisualTreeAndRefresh()
        {
            playerNameLabel = uiDocument != null
                ? uiDocument.rootVisualElement?.Q<Label>("player-name-label")
                : null;
            RefreshDisplayName();
        }

        private void RefreshDisplayName()
        {
            if (playerNameLabel == null)
                return;

            if (identitySource == null || !identitySource.IsAvailable)
            {
                playerNameLabel.style.display = DisplayStyle.None;
                return;
            }

            var displayName = identitySource.DisplayName;
            playerNameLabel.text = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : identitySource.PlayerId is { } playerId ? $"Player{playerId}" : "Player";
            playerNameLabel.style.display = DisplayStyle.Flex;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || playerNameLabel == null || camera.cameraType != CameraType.Game
                || camera.GetComponent<WorldSpaceUiOverlayCameraMarker>() != null)
                return;

            // Position is inherited from Presentation. Only camera-facing orientation and
            // camera-dependent fading are updated at the render boundary.
            transform.rotation = camera.transform.rotation;

            var distance = Vector3.Distance(camera.transform.position, transform.position);
            transform.localScale = baseLocalScale * (CalculateScreenSizeScale(camera) * screenSizeMultiplier);

            var fadeRange = Mathf.Max(0.01f, fadeEndDistance - fadeStartDistance);
            playerNameLabel.style.opacity = 1f - Mathf.Clamp01((distance - fadeStartDistance) / fadeRange);
        }

        private float CalculateScreenSizeScale(Camera camera)
        {
            if (camera.orthographic)
                return Mathf.Max(0.01f, camera.orthographicSize / referenceOrthographicSize);

            // Projected height is proportional to worldSize * projection.m11 / viewDepth.
            // Compensating both terms keeps the UI's viewport height stable even when
            // the player is off-center or the camera FOV changes.
            var toOverlay = transform.position - camera.transform.position;
            var viewDepth = Mathf.Max(camera.nearClipPlane, Vector3.Dot(toOverlay, camera.transform.forward));
            var currentProjectionScale = Mathf.Max(0.0001f, Mathf.Abs(camera.projectionMatrix.m11));
            return Mathf.Max(
                0.01f,
                viewDepth * referenceProjectionScale
                / (Mathf.Max(0.01f, referenceDistance) * currentProjectionScale));
        }

        private IPlayerIdentitySource FindIdentitySource()
        {
            var parents = GetComponentsInParent<MonoBehaviour>(true);
            for (var i = 0; i < parents.Length; i++)
            {
                var candidate = parents[i];
                if (candidate != null && candidate != this && candidate is IPlayerIdentitySource source)
                    return source;
            }

            return null;
        }
    }
}
