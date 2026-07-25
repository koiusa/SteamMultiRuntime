using System.Collections.Generic;
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

        [Header("Distance Fade")]
        [Min(0f)] [SerializeField] private float fadeStartDistance = 8f;
        [Min(0f)] [SerializeField] private float fadeEndDistance = 20f;

        [Header("UI Assets")]
        [SerializeField] private VisualTreeAsset labelTemplateAsset;
        [SerializeField] private StyleSheet overlayStyleSheet;

        private UIDocument sourceDocument;
        private IPlayerDisplayNameSource displayNameSource;
        private bool displayNameSourceResolved;

        internal float RefreshInterval => Mathf.Max(0.1f, refreshInterval);
        internal float LabelWidth => labelWidth;
        internal float LabelHeight => labelHeight;
        internal float FadeStartDistance => fadeStartDistance;
        internal float FadeEndDistance => fadeEndDistance;
        internal VisualTreeAsset LabelTemplateAsset => labelTemplateAsset;
        internal StyleSheet OverlayStyleSheet => overlayStyleSheet;

        private void Awake()
        {
            sourceDocument = GetComponent<UIDocument>();
            labelTemplateAsset ??= Resources.Load<VisualTreeAsset>(DefaultLabelTemplateResourcePath);
            overlayStyleSheet ??= Resources.Load<StyleSheet>(DefaultStyleSheetResourcePath);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            sourceDocument ??= GetComponent<UIDocument>();
            var panelSettings = sourceDocument != null ? sourceDocument.panelSettings : null;

            // The prefab document is only a settings holder. Rendering one full-screen document
            // per character is expensive, so the shared manager owns the only active document.
            if (sourceDocument != null)
                sourceDocument.enabled = false;

            PlayerNameOverlayManager.Register(this, panelSettings);
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                PlayerNameOverlayManager.Unregister(this);
        }

        internal bool TryGetPlayerName(out string playerName)
        {
            var source = FindTargetSource();
            if (source == null)
            {
                playerName = null;
                return false;
            }

            if (!displayNameSourceResolved)
            {
                displayNameSource = FindDisplayNameSource(source);
                displayNameSourceResolved = true;
            }

            if (displayNameSource != null)
            {
                var displayName = displayNameSource.DisplayName;
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    playerName = displayName;
                    return true;
                }
            }

            var ownerClientId = TryGetOwnerClientId(source);
            playerName = ownerClientId.HasValue
                ? $"Player{ownerClientId.Value}"
                : $"Player{Mathf.Abs(source.GetInstanceID()) % 1000}";
            return true;
        }

        private MonoBehaviour FindTargetSource()
        {
            var parents = GetComponentsInParent<MonoBehaviour>(true);
            MonoBehaviour fallback = null;

            for (var i = 0; i < parents.Length; i++)
            {
                var candidate = parents[i];
                if (candidate == null || candidate == this || !(candidate is IPlayerController))
                    continue;

                if (candidate is Unity.Netcode.NetworkBehaviour)
                    return candidate;

                fallback ??= candidate;
            }

            return fallback;
        }

        private static IPlayerDisplayNameSource FindDisplayNameSource(MonoBehaviour source)
        {
            var candidates = source.GetComponentsInParent<MonoBehaviour>(true);
            for (var i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] is IPlayerDisplayNameSource nameSource)
                    return nameSource;
            }

            return null;
        }

        private static ulong? TryGetOwnerClientId(MonoBehaviour source)
        {
            var sourceType = source.GetType();
            var isSpawnedProperty = sourceType.GetProperty("IsSpawned");
            if (isSpawnedProperty?.GetValue(source) is bool isSpawned && !isSpawned)
                return null;

            return sourceType.GetProperty("OwnerClientId")?.GetValue(source) is ulong ownerClientId
                ? ownerClientId
                : null;
        }
    }

    internal sealed class PlayerNameOverlayManager : MonoBehaviour
    {
        private sealed class Entry
        {
            public PlayerNameOverlayUiDocument Owner;
            public Label Label;
            public float NextRefreshTime;
            public bool HasTarget;
            public bool Visible;
            public float LastOpacity = -1f;
        }

        private static PlayerNameOverlayManager instance;

        private readonly Dictionary<PlayerNameOverlayUiDocument, Entry> entries = new();
        private readonly List<PlayerNameOverlayUiDocument> removalBuffer = new();
        private UIDocument uiDocument;
        private VisualElement overlayRoot;
        private Camera targetCamera;

        internal static void Register(PlayerNameOverlayUiDocument owner, PanelSettings panelSettings)
        {
            if (owner == null || panelSettings == null)
                return;

            EnsureInstance(panelSettings);
            instance.Add(owner);
        }

        internal static void Unregister(PlayerNameOverlayUiDocument owner)
        {
            if (instance != null)
                instance.Remove(owner);
        }

        private static void EnsureInstance(PanelSettings panelSettings)
        {
            if (instance != null)
                return;

            var managerObject = new GameObject("[Player Name Overlay]");
            DontDestroyOnLoad(managerObject);
            instance = managerObject.AddComponent<PlayerNameOverlayManager>();
            instance.uiDocument = managerObject.AddComponent<UIDocument>();
            instance.uiDocument.panelSettings = panelSettings;
            instance.uiDocument.sortingOrder = short.MaxValue;
            instance.BuildRoot();
        }

        private void BuildRoot()
        {
            var documentRoot = uiDocument != null ? uiDocument.rootVisualElement : null;
            if (documentRoot == null)
                return;

            documentRoot.pickingMode = PickingMode.Ignore;
            overlayRoot = new VisualElement
            {
                name = "player-name-overlay-root",
                pickingMode = PickingMode.Ignore
            };
            overlayRoot.AddToClassList("player-name-overlay");
            documentRoot.Add(overlayRoot);
        }

        private void Add(PlayerNameOverlayUiDocument owner)
        {
            if (entries.ContainsKey(owner))
                return;

            if (overlayRoot == null)
                BuildRoot();
            if (overlayRoot == null)
                return;

            if (owner.OverlayStyleSheet != null && !overlayRoot.styleSheets.Contains(owner.OverlayStyleSheet))
                overlayRoot.styleSheets.Add(owner.OverlayStyleSheet);

            var label = CreateLabel(owner);
            overlayRoot.Add(label);
            entries.Add(owner, new Entry { Owner = owner, Label = label });
        }

        private static Label CreateLabel(PlayerNameOverlayUiDocument owner)
        {
            Label label = null;
            if (owner.LabelTemplateAsset != null)
            {
                var container = owner.LabelTemplateAsset.Instantiate();
                label = container.Q<Label>();
                label?.RemoveFromHierarchy();
            }

            label ??= new Label();
            label.pickingMode = PickingMode.Ignore;
            label.AddToClassList("player-name-overlay__label");
            label.style.position = Position.Absolute;
            label.style.width = owner.LabelWidth;
            label.style.height = owner.LabelHeight;
            label.style.display = DisplayStyle.None;
            return label;
        }

        private void Remove(PlayerNameOverlayUiDocument owner)
        {
            if (!entries.TryGetValue(owner, out var entry))
                return;

            entry.Label?.RemoveFromHierarchy();
            entries.Remove(owner);
        }

        private void LateUpdate()
        {
            if (overlayRoot == null || overlayRoot.panel == null)
                return;

            targetCamera = ResolveCamera(targetCamera);
            removalBuffer.Clear();

            foreach (var pair in entries)
            {
                var entry = pair.Value;
                if (entry.Owner == null)
                {
                    removalBuffer.Add(pair.Key);
                    continue;
                }

                if (Time.unscaledTime >= entry.NextRefreshTime)
                {
                    entry.HasTarget = entry.Owner.TryGetPlayerName(out var playerName);
                    if (entry.HasTarget && entry.Label.text != playerName)
                        entry.Label.text = playerName;
                    entry.NextRefreshTime = Time.unscaledTime + entry.Owner.RefreshInterval;
                }

                UpdateEntry(entry, targetCamera);
            }

            for (var i = 0; i < removalBuffer.Count; i++)
                Remove(removalBuffer[i]);
        }

        private void UpdateEntry(Entry entry, Camera camera)
        {
            if (camera == null || !entry.HasTarget)
            {
                SetVisible(entry, false);
                return;
            }

            var screenPosition = camera.WorldToScreenPoint(entry.Owner.transform.position);
            var viewport = camera.pixelRect;
            var visible = screenPosition.z > 0f
                && screenPosition.x >= viewport.xMin
                && screenPosition.x <= viewport.xMax
                && screenPosition.y >= viewport.yMin
                && screenPosition.y <= viewport.yMax;
            SetVisible(entry, visible);
            if (!visible)
                return;

            var panelPosition = RuntimePanelUtils.ScreenToPanel(
                overlayRoot.panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            entry.Label.transform.position = new Vector3(
                panelPosition.x - entry.Owner.LabelWidth * 0.5f,
                panelPosition.y - entry.Owner.LabelHeight * 0.5f,
                0f);

            var distance = Vector3.Distance(camera.transform.position, entry.Owner.transform.position);
            var fadeRange = Mathf.Max(0.01f, entry.Owner.FadeEndDistance - entry.Owner.FadeStartDistance);
            var opacity = 1f - Mathf.Clamp01((distance - entry.Owner.FadeStartDistance) / fadeRange);
            if (Mathf.Abs(opacity - entry.LastOpacity) >= 0.01f)
            {
                entry.Label.style.opacity = opacity;
                entry.LastOpacity = opacity;
            }
        }

        private static void SetVisible(Entry entry, bool visible)
        {
            if (entry.Visible == visible)
                return;

            entry.Visible = visible;
            entry.Label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static Camera ResolveCamera(Camera current)
        {
            if (current != null && current.isActiveAndEnabled)
                return current;
            if (Camera.main != null && Camera.main.isActiveAndEnabled)
                return Camera.main;
            return FindFirstObjectByType<Camera>();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
