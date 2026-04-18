using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class PlayerNameOverlayUiDocument : MonoBehaviour
    {
        private const string DefaultLabelTemplateResourcePath = "UI/PlayerNameOverlay/PlayerNameOverlayLabel";
        private const string DefaultStyleSheetResourcePath = "UI/PlayerNameOverlay/PlayerNameOverlay";

        [Header("Display")]
        [SerializeField] private float refreshInterval = 1f;
        [SerializeField] private float labelWidth = 88f;
        [SerializeField] private float labelHeight = 24f;

        [Header("Follow")]
        [SerializeField] private bool billboardToCamera = true;

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset labelTemplateAsset;
        [SerializeField] private StyleSheet overlayStyleSheet;

        private UIDocument uiDocument;
        private VisualElement overlayRoot;
        private Label playerNameLabel;
        private MonoBehaviour targetSource;
        private Camera targetCamera;
        private float nextRefreshTime;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();

            if (labelTemplateAsset == null)
            {
                labelTemplateAsset = Resources.Load<VisualTreeAsset>(DefaultLabelTemplateResourcePath);
            }

            if (overlayStyleSheet == null)
            {
                overlayStyleSheet = Resources.Load<StyleSheet>(DefaultStyleSheetResourcePath);
            }

            BuildUi();
            ResolveTarget();
            RefreshLabel();
        }

        private void OnEnable()
        {
            BuildUi();
            ResolveTarget();
            RefreshLabel();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Time.unscaledTime >= nextRefreshTime)
            {
                ResolveTarget();
                RefreshLabel();
                nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshInterval);
            }

            targetCamera = ResolveCamera(targetCamera);
            if (targetCamera == null)
            {
                if (playerNameLabel != null)
                {
                    playerNameLabel.style.display = DisplayStyle.None;
                }
                return;
            }

            if (billboardToCamera)
            {
                BillboardToCamera(targetCamera);
            }
        }

        private void BuildUi()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>();
            }

            var root = uiDocument != null ? uiDocument.rootVisualElement : null;
            if (root == null)
            {
                return;
            }

            if (overlayRoot != null && overlayRoot.parent == root && playerNameLabel != null)
            {
                return;
            }

            root.Clear();

            overlayRoot = new VisualElement
            {
                name = "player-name-overlay-root",
                pickingMode = PickingMode.Ignore
            };
            overlayRoot.AddToClassList("player-name-overlay");
            if (overlayStyleSheet != null)
            {
                overlayRoot.styleSheets.Add(overlayStyleSheet);
            }
            root.Add(overlayRoot);

            playerNameLabel = CreateLabel();
            overlayRoot.Add(playerNameLabel);
        }

        private Label CreateLabel()
        {
            Label label = null;

            if (labelTemplateAsset != null)
            {
                var container = labelTemplateAsset.Instantiate();
                label = container.Q<Label>();
                if (label != null)
                {
                    label.RemoveFromHierarchy();
                }
            }

            label ??= new Label();
            label.pickingMode = PickingMode.Ignore;
            label.AddToClassList("player-name-overlay__label");
            label.style.width = labelWidth;
            label.style.height = labelHeight;
            label.style.display = DisplayStyle.None;
            return label;
        }

        private void ResolveTarget()
        {
            targetSource = FindTargetSource();
        }

        private MonoBehaviour FindTargetSource()
        {
            var parents = GetComponentsInParent<MonoBehaviour>(true);
            MonoBehaviour fallback = null;

            for (var i = 0; i < parents.Length; i++)
            {
                var candidate = parents[i];
                if (candidate == null || candidate == this || !(candidate is IPlayerController))
                {
                    continue;
                }

                if (candidate is Unity.Netcode.NetworkBehaviour)
                {
                    return candidate;
                }

                fallback ??= candidate;
            }

            return fallback;
        }

        private void RefreshLabel()
        {
            if (playerNameLabel == null)
            {
                return;
            }

            if (targetSource == null)
            {
                playerNameLabel.style.display = DisplayStyle.None;
                return;
            }

            playerNameLabel.text = GetPlayerName(targetSource);
            playerNameLabel.style.display = DisplayStyle.Flex;
        }

        private void BillboardToCamera(Camera camera)
        {
            var toCamera = transform.position - camera.transform.position;
            if (toCamera.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(toCamera.normalized, camera.transform.up);
        }

        private string GetPlayerName(MonoBehaviour source)
        {
            var ownerClientId = TryGetOwnerClientId(source);
            if (ownerClientId.HasValue)
            {
                return $"Player{ownerClientId.Value}";
            }

            return $"Player{Mathf.Abs(source.GetInstanceID()) % 1000}";
        }

        private ulong? TryGetOwnerClientId(MonoBehaviour source)
        {
            if (source == null)
            {
                return null;
            }

            var sourceType = source.GetType();
            var isSpawnedProperty = sourceType.GetProperty("IsSpawned");
            if (isSpawnedProperty != null)
            {
                var isSpawnedValue = isSpawnedProperty.GetValue(source);
                if (isSpawnedValue is bool isSpawned && !isSpawned)
                {
                    return null;
                }
            }

            var ownerClientIdProperty = sourceType.GetProperty("OwnerClientId");
            if (ownerClientIdProperty == null)
            {
                return null;
            }

            var value = ownerClientIdProperty.GetValue(source);
            if (value is ulong ownerClientId)
            {
                return ownerClientId;
            }

            return null;
        }

        private static Camera ResolveCamera(Camera currentCamera)
        {
            if (currentCamera != null && currentCamera.isActiveAndEnabled)
            {
                return currentCamera;
            }

            if (Camera.main != null && Camera.main.isActiveAndEnabled)
            {
                return Camera.main;
            }

            return Object.FindFirstObjectByType<Camera>();
        }
    }
}
