using System;
using System.Collections.Generic;
using Koiusa.UI.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public class CharacterDebugOverlay : MonoBehaviour
    {
        private const string PreferredFaceLayerName = "face";
        private const string DefaultPanelSettingsPath = "UI/CharacterSelect/CharacterSelect Panel Settings";
        private const string DefaultStyleSheetPath = "UI/CharacterDebug/CharacterDebugOverlay";
        private const float RefreshInterval = 0.1f;

        [Header("Display")]
        [SerializeField] private bool showOnStart;
        [SerializeField] private bool autoShowInPlayMode = true;
        [SerializeField] private bool onlyLocalOwnedCharacter = true;
        [SerializeField] private bool aggregateFromAllInstances = true;
        [SerializeField] private bool includeNonOwnedCharactersInAggregate = true;
        [SerializeField] private bool showLauncherButtonWhenHidden = true;
        [SerializeField] private float windowWidth = 360f;
        [SerializeField] private float windowHeight = 520f;
        [SerializeField] private Vector2 windowPosition = new Vector2(12f, 12f);
        [SerializeField] private Vector2 launcherButtonSize = new Vector2(140f, 28f);
        [SerializeField] private PanelSettings panelSettings;
        [SerializeField] private StyleSheet styleSheet;

        [Header("Target")]
        [SerializeField] private Transform targetRoot;
        [SerializeField] private Rigidbody targetRigidbody;
        [SerializeField] private Animator targetAnimator;
        [SerializeField] private Animator targetFaceAnimator;

        private static readonly List<CharacterDebugOverlay> ActiveInstances = new();
        private static int selectedInstanceIndex;
        private static CharacterDebugOverlay currentUiOwner;
        private static CharacterDebugOverlayUpdateLoop updateLoop;

        private readonly List<LabelBinding> labelBindings = new();
        private readonly CharacterDebugSnapshot debugSnapshot = new();
        private IActorController playerController;
        private ICharacterDebugSnapshotSource snapshotSource;
        private NetworkBehaviour targetNetworkBehaviour;
        private CharacterDebugDisplayScope displayScope;
        private UIDocument uiDocument;
        private VisualElement documentRoot;
        private VisualElement overlayRoot;
        private VisualElement window;
        private VisualElement content;
        private Button launcherButton;
        private Label targetSelectorLabel;
        private bool isVisible;
        private float nextRefreshTime;
        private CharacterDebugOverlay displayedTarget;
        private Vector2 dragStartPointer;
        private Vector2 dragStartWindow;
        private bool dragging;
        private int selectedPageIndex;

        private sealed class LabelBinding
        {
            public Label Label;
            public Func<string> Value;
        }

        private void Awake()
        {
            isVisible = showOnStart || (autoShowInPlayMode && Application.isPlaying);
            ResolveReferences();
        }

        private void OnEnable()
        {
            ActiveInstances.Add(this);
            EnsureUpdateLoop();
        }

        private void OnDisable()
        {
            ActiveInstances.Remove(this);
            if (ReferenceEquals(currentUiOwner, this))
                currentUiOwner = null;
            if (selectedInstanceIndex >= ActiveInstances.Count)
                selectedInstanceIndex = Mathf.Max(0, ActiveInstances.Count - 1);
            DestroyUi();
        }

        private void Tick()
        {
            EnsureUi();
            UpdateVisibility();
            if (!isVisible || Time.unscaledTime < nextRefreshTime) return;

            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            var target = GetDisplayedTarget();
            if (target != displayedTarget)
            {
                BuildContent(target);
            }

            target?.ResolveReferences();
            target?.CaptureDebugSnapshot();
            UpdateBoundLabels();
            UpdateTargetSelector(target);
        }

        private static void EnsureUpdateLoop()
        {
            if (updateLoop != null)
                return;
            var host = new GameObject(nameof(CharacterDebugOverlay) + "UpdateLoop")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            DontDestroyOnLoad(host);
            updateLoop = host.AddComponent<CharacterDebugOverlayUpdateLoop>();
        }

        internal static void TickActiveOverlay()
        {
            CharacterDebugOverlay owner = null;
            for (var i = 0; i < ActiveInstances.Count; i++)
            {
                var candidate = ActiveInstances[i];
                if (candidate == null || !candidate.ShouldOwnUi())
                    continue;
                owner = candidate;
                break;
            }

            if (!ReferenceEquals(currentUiOwner, owner))
            {
                currentUiOwner?.DestroyUi();
                currentUiOwner = owner;
            }
            currentUiOwner?.Tick();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (updateLoop != null)
                Destroy(updateLoop.gameObject);
            ActiveInstances.Clear();
            selectedInstanceIndex = 0;
            currentUiOwner = null;
            updateLoop = null;
        }

        public void Toggle() { isVisible = !isVisible; UpdateVisibility(); }
        public void Show() { isVisible = true; UpdateVisibility(); }
        public void Hide() { isVisible = false; UpdateVisibility(); }

        public void ResolveReferences()
        {
            var root = targetRoot != null ? targetRoot : FindPlayerRoot();
            displayScope = GetComponentInParent<CharacterDebugDisplayScope>();
            playerController = root.GetComponent<IActorController>();
            targetNetworkBehaviour = root.GetComponent<NetworkBehaviour>();
            if (targetRigidbody == null) targetRigidbody = root.GetComponent<Rigidbody>();
            if (targetAnimator == null) targetAnimator = FindBodyAnimator(root);
            if (targetFaceAnimator == null) targetFaceAnimator = FindFaceAnimator(root);
            var hasFaceAnimator = TryGetFaceAnimatorAndLayer(out var resolvedFaceAnimator, out var faceLayer);
            var faceAnimator = hasFaceAnimator ? resolvedFaceAnimator : null;
            var resolvedFaceLayer = hasFaceAnimator ? faceLayer : -1;
            if (snapshotSource == null || !snapshotSource.Matches(
                    root, playerController, targetRigidbody, targetAnimator, faceAnimator,
                    targetNetworkBehaviour, resolvedFaceLayer))
            {
                snapshotSource = new CharacterDebugSnapshotSource(
                    root,
                    playerController,
                    targetRigidbody,
                    targetAnimator,
                    faceAnimator,
                    targetNetworkBehaviour,
                    resolvedFaceLayer);
            }
        }

        private void CaptureDebugSnapshot() => snapshotSource?.Capture(debugSnapshot);

        private void EnsureUi()
        {
            if (uiDocument == null)
            {
                panelSettings ??= Resources.Load<PanelSettings>(DefaultPanelSettingsPath);
                if (panelSettings == null)
                {
                    Debug.LogError($"Character debug UI requires PanelSettings. Assign it on {name}.", this);
                    enabled = false;
                    return;
                }

                uiDocument = gameObject.AddComponent<UIDocument>();
                // AddComponent enables UIDocument immediately. Re-enable it after the
                // settings assignment so its live panel is created with those settings.
                uiDocument.enabled = false;
                uiDocument.panelSettings = panelSettings;
                uiDocument.sortingOrder = 1000;
                uiDocument.enabled = true;
            }

            var currentRoot = uiDocument.rootVisualElement;
            if (currentRoot == null || ReferenceEquals(currentRoot, documentRoot)) return;

            documentRoot = currentRoot;
            BuildChrome(documentRoot);
        }

        private void BuildChrome(VisualElement root)
        {
            root.Clear();
            styleSheet ??= Resources.Load<StyleSheet>(DefaultStyleSheetPath);
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);
            KoiusaUiTheme.Apply(root);
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.right = 0;
            root.style.bottom = 0;
            root.pickingMode = PickingMode.Ignore;

            overlayRoot = new VisualElement { name = "character-debug-overlay", pickingMode = PickingMode.Ignore };
            overlayRoot.style.position = Position.Absolute;
            overlayRoot.style.left = 0;
            overlayRoot.style.top = 0;
            overlayRoot.style.right = 0;
            overlayRoot.style.bottom = 0;
            root.Add(overlayRoot);

            launcherButton = new Button(Show) { text = "Show Character Debug" };
            launcherButton.AddToClassList("character-debug-launcher");
            launcherButton.style.position = Position.Absolute;
            launcherButton.style.right = windowPosition.x;
            launcherButton.style.top = windowPosition.y;
            launcherButton.style.width = launcherButtonSize.x;
            launcherButton.style.height = launcherButtonSize.y;
            overlayRoot.Add(launcherButton);

            window = new VisualElement { name = "character-debug-window" };
            window.style.position = Position.Absolute;
            window.style.right = windowPosition.x;
            window.style.top = windowPosition.y;
            window.style.width = Mathf.Max(windowWidth, 680f);
            window.style.height = windowHeight;
            window.style.paddingLeft = 8;
            window.style.paddingRight = 8;
            window.style.paddingBottom = 8;
            overlayRoot.Add(window);

            var titleBar = new VisualElement();
            titleBar.AddToClassList("character-debug-titlebar");
            titleBar.style.height = 30;
            titleBar.style.flexDirection = FlexDirection.Row;
            titleBar.style.alignItems = Align.Center;
            titleBar.RegisterCallback<PointerDownEvent>(BeginDrag);
            titleBar.RegisterCallback<PointerMoveEvent>(Drag);
            titleBar.RegisterCallback<PointerUpEvent>(EndDrag);
            window.Add(titleBar);

            var title = new Label("Character Debug");
            title.AddToClassList("character-debug-title");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            titleBar.Add(title);
            var closeButton = new Button(Hide) { text = "×" };
            closeButton.AddToClassList("character-debug-close");
            titleBar.Add(closeButton);

            var selector = new VisualElement();
            selector.AddToClassList("character-debug-selector");
            selector.style.flexDirection = FlexDirection.Row;
            selector.Add(new Button(() => SelectRelative(-1)) { text = "<" });
            targetSelectorLabel = new Label();
            targetSelectorLabel.AddToClassList("character-debug-target-label");
            targetSelectorLabel.style.flexGrow = 1;
            targetSelectorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            selector.Add(targetSelectorLabel);
            selector.Add(new Button(() => SelectRelative(1)) { text = ">" });
            window.Add(selector);

            var refreshButton = new Button(() =>
            {
                displayedTarget?.ResolveReferences();
                BuildContent(displayedTarget);
            }) { text = "Refresh References" };
            refreshButton.AddToClassList("character-debug-refresh");
            window.Add(refreshButton);

            content = new VisualElement();
            content.AddToClassList("character-debug-content");
            content.style.flexGrow = 1;
            window.Add(content);
            BuildContent(GetDisplayedTarget());
            UpdateVisibility();
        }

        private void BuildContent(CharacterDebugOverlay target)
        {
            if (content == null) return;
            displayedTarget = target;
            content.Clear();
            labelBindings.Clear();
            if (target == null)
            {
                content.Add(new Label("Debug target not found."));
                return;
            }

            target.ResolveReferences();
            target.CaptureDebugSnapshot();

            var summary = new VisualElement();
            summary.AddToClassList("character-debug-summary");
            content.Add(summary);
            BindSummaryLabel(summary, "Character", () => target.debugSnapshot.CharacterName, true);
            BindSummaryLabel(summary, "Mode", () => target.debugSnapshot.NetworkMode, false);

            var tabBar = new VisualElement();
            tabBar.AddToClassList("character-debug-tabs");
            content.Add(tabBar);
            var statePage = new VisualElement();
            statePage.AddToClassList("character-debug-page");
            var animationPage = new VisualElement();
            animationPage.AddToClassList("character-debug-page");
            content.Add(statePage);
            content.Add(animationPage);

            var stateTab = new Button { text = "STATE" };
            var animationTab = new Button { text = "ANIMATION" };
            stateTab.AddToClassList("character-debug-tab");
            animationTab.AddToClassList("character-debug-tab");
            tabBar.Add(stateTab);
            tabBar.Add(animationTab);
            stateTab.clicked += () => SelectPage(0, statePage, animationPage, stateTab, animationTab);
            animationTab.clicked += () => SelectPage(1, statePage, animationPage, stateTab, animationTab);

            var stateColumns = CreateColumns(statePage);
            AddSection(stateColumns.left, "Controller State", section => BindControllerLabels(section, target));
            AddSection(stateColumns.right, "Rigidbody", section => BindRigidbodyLabels(section, target));

            var animationColumns = CreateColumns(animationPage);
            AddSection(animationColumns.left, "Body Animator", section =>
                BindAnimatorLabels(section, target.debugSnapshot.BodyAnimator, true));
            AddSection(animationColumns.right, "Face Animation", section =>
                BindAnimatorLabels(section, target.debugSnapshot.FaceAnimator, false));
            SelectPage(selectedPageIndex, statePage, animationPage, stateTab, animationTab);
            UpdateBoundLabels();
            UpdateTargetSelector(target);
        }

        private static (VisualElement left, VisualElement right) CreateColumns(VisualElement parent)
        {
            var columns = new VisualElement();
            columns.AddToClassList("character-debug-columns");
            var left = new VisualElement();
            var right = new VisualElement();
            left.AddToClassList("character-debug-column");
            right.AddToClassList("character-debug-column");
            columns.Add(left);
            columns.Add(right);
            parent.Add(columns);
            return (left, right);
        }

        private void SelectPage(
            int pageIndex,
            VisualElement statePage,
            VisualElement animationPage,
            Button stateTab,
            Button animationTab)
        {
            selectedPageIndex = pageIndex;
            statePage.style.display = pageIndex == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            animationPage.style.display = pageIndex == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            stateTab.EnableInClassList("active", pageIndex == 0);
            animationTab.EnableInClassList("active", pageIndex == 1);
        }

        private void AddSection(VisualElement parent, string title, Action<VisualElement> build)
        {
            var foldout = new Foldout { text = title, value = true };
            foldout.AddToClassList("character-debug-section");
            foldout.style.marginBottom = 4;
            parent.Add(foldout);
            build(foldout);
        }

        private void BindLabel(VisualElement parent, string name, Func<string> value)
        {
            var label = new Label();
            label.AddToClassList("character-debug-value");
            parent.Add(label);
            labelBindings.Add(new LabelBinding { Label = label, Value = () => $"{name}: {value()}" });
        }

        private void BindSummaryLabel(VisualElement parent, string name, Func<string> value, bool grow)
        {
            var label = new Label();
            label.AddToClassList("character-debug-summary-value");
            if (grow) label.AddToClassList("character-debug-summary-target");
            parent.Add(label);
            labelBindings.Add(new LabelBinding { Label = label, Value = () => $"{name}: {value()}" });
        }

        private void BindControllerLabels(VisualElement parent, CharacterDebugOverlay target)
        {
            var snapshot = target.debugSnapshot;
            if (!snapshot.HasController) { parent.Add(new Label("IActorController: not found")); return; }
            BindLabel(parent, "Grounded", () => snapshot.Grounded.ToString());
            BindLabel(parent, "Jumping", () => snapshot.Jumping.ToString());
            BindLabel(parent, "Freefall", () => snapshot.Freefall.ToString());
            BindLabel(parent, "FallingAfterJump", () => snapshot.FallingAfterJump.ToString());
            if (snapshot.HasLadderState)
            {
                BindLabel(parent, "OnLadder", () => snapshot.OnLadder.ToString());
                BindLabel(parent, "LadderSpeed", () => snapshot.LadderSpeed.ToString("F3"));
            }
            BindLabel(parent, "HorizontalVelocity", () => snapshot.HorizontalVelocity.ToString("F3"));
            BindLabel(parent, "VerticalVelocity", () => snapshot.VerticalVelocity.ToString("F3"));
            BindLabel(parent, "MaxMoveSpeed", () => snapshot.MaxMoveSpeed.ToString("F3"));
            BindLabel(parent, "InheritedGroundVelocity", () => snapshot.InheritedGroundVelocity.ToString());
        }

        private void BindRigidbodyLabels(VisualElement parent, CharacterDebugOverlay target)
        {
            var snapshot = target.debugSnapshot;
            if (!snapshot.HasRigidbody) { parent.Add(new Label("Rigidbody: not found")); return; }
            BindLabel(parent, "Position", () => snapshot.Position.ToString());
            BindLabel(parent, "Velocity", () => snapshot.Velocity.ToString());
            BindLabel(parent, "Speed", () => snapshot.Velocity.magnitude.ToString("F3"));
            BindLabel(parent, "AngularVelocity", () => snapshot.AngularVelocity.ToString());
        }

        private void BindAnimatorLabels(VisualElement parent, AnimatorDebugSnapshot snapshot, bool parameters)
        {
            if (!snapshot.IsAvailable)
            {
                parent.Add(new Label("Animator/layer not found"));
                return;
            }
            BindLabel(parent, "Animator", () => snapshot.AnimatorName);
            BindLabel(parent, "Layer", () => snapshot.Layer);
            BindLabel(parent, "State", () => snapshot.State);
            BindLabel(parent, "NormalizedTime", () => snapshot.NormalizedTime.ToString("F3"));
            BindLabel(parent, "LayerWeight", () => snapshot.LayerWeight.ToString("F3"));
            BindLabel(parent, "Clip", () => snapshot.Clip);
            if (!parameters) return;
            var parameterTitle = new Label("Parameters");
            parameterTitle.AddToClassList("character-debug-parameter-title");
            parent.Add(parameterTitle);
            var parameterGrid = new VisualElement();
            parameterGrid.AddToClassList("character-debug-parameter-grid");
            parent.Add(parameterGrid);
            for (var i = 0; i < snapshot.Parameters.Count; i++)
            {
                var index = i;
                var name = snapshot.Parameters[index].Name;
                BindLabel(parameterGrid, name, () => index < snapshot.Parameters.Count
                    ? snapshot.Parameters[index].Value
                    : "unavailable");
            }
        }

        private void UpdateBoundLabels()
        {
            foreach (var binding in labelBindings)
            {
                try { binding.Label.text = binding.Value(); }
                catch (MissingReferenceException) { binding.Label.text = "Reference missing"; }
            }
        }

        private void UpdateVisibility()
        {
            if (window == null || launcherButton == null) return;
            window.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            launcherButton.style.display = !isVisible && showLauncherButtonWhenHidden
                ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private CharacterDebugOverlay GetDisplayedTarget()
        {
            if (!aggregateFromAllInstances) return CanRenderForThisInstance() ? this : null;
            var targets = CollectAggregateTargets();
            if (targets.Count == 0) return null;
            selectedInstanceIndex = Mathf.Clamp(selectedInstanceIndex, 0, targets.Count - 1);
            return targets[selectedInstanceIndex];
        }

        private void SelectRelative(int offset)
        {
            var targets = CollectAggregateTargets();
            if (targets.Count == 0) return;
            selectedInstanceIndex = (selectedInstanceIndex + offset + targets.Count) % targets.Count;
            BuildContent(targets[selectedInstanceIndex]);
        }

        private void UpdateTargetSelector(CharacterDebugOverlay target)
        {
            if (targetSelectorLabel == null) return;
            var targets = aggregateFromAllInstances ? CollectAggregateTargets() : new List<CharacterDebugOverlay> { this };
            var index = target != null ? targets.IndexOf(target) : -1;
            targetSelectorLabel.text = target == null ? "No target" : $"{index + 1}/{targets.Count} {target.debugSnapshot.TargetName}";
        }

        private void BeginDrag(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            dragging = true;
            dragStartPointer = evt.position;
            dragStartWindow = new Vector2(window.resolvedStyle.left, window.resolvedStyle.top);
            window.style.right = StyleKeyword.Auto;
            window.style.left = dragStartWindow.x;
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
        }

        private void Drag(PointerMoveEvent evt)
        {
            if (!dragging) return;
            var position = dragStartWindow + (Vector2)evt.position - dragStartPointer;
            window.style.left = Mathf.Max(0, position.x);
            window.style.top = Mathf.Max(0, position.y);
        }

        private void EndDrag(PointerUpEvent evt)
        {
            dragging = false;
            ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
        }

        private bool ShouldOwnUi() => IsDisplayAllowed() &&
            (aggregateFromAllInstances ? IsPrimaryRenderer() : CanRenderForThisInstance());

        private bool IsPrimaryRenderer()
        {
            foreach (var candidate in ActiveInstances)
                if (candidate != null && candidate.IsDisplayAllowed()) return ReferenceEquals(candidate, this);
            return false;
        }

        private bool IsDisplayAllowed()
        {
            displayScope ??= GetComponentInParent<CharacterDebugDisplayScope>();
            return displayScope == null || displayScope.CanRender(this);
        }

        private bool CanRenderForThisInstance()
        {
            if (!onlyLocalOwnedCharacter || targetNetworkBehaviour == null) return true;
            var manager = NetworkManager.Singleton;
            return manager == null || !manager.IsListening || (targetNetworkBehaviour.IsSpawned && targetNetworkBehaviour.IsOwner);
        }

        private List<CharacterDebugOverlay> CollectAggregateTargets()
        {
            var targets = new List<CharacterDebugOverlay>();
            foreach (var candidate in ActiveInstances)
                if (candidate != null && candidate.IsDisplayAllowed() &&
                    (includeNonOwnedCharactersInAggregate || candidate.CanRenderForThisInstance())) targets.Add(candidate);
            return targets;
        }

        private void DestroyUi()
        {
            if (uiDocument == null) return;
            Destroy(uiDocument);
            uiDocument = null;
            documentRoot = null;
            overlayRoot = null;
            window = null;
            content = null;
            launcherButton = null;
            displayedTarget = null;
            labelBindings.Clear();
        }

        private Transform FindPlayerRoot()
        {
            for (var current = transform; current != null; current = current.parent)
                if (current.GetComponent<IActorController>() != null) return current;
            for (var current = transform; current != null; current = current.parent)
                if (current.GetComponent<Rigidbody>() != null || current.GetComponent<NetworkBehaviour>() != null) return current;
            return transform;
        }

        private Animator FindBodyAnimator(Transform root)
        {
            var rootAnimator = root.GetComponent<Animator>();
            if (rootAnimator != null) return rootAnimator;
            Animator fallback = null;
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator == null) continue;
                fallback ??= animator;
                if (animator.name.IndexOf(PreferredFaceLayerName, StringComparison.OrdinalIgnoreCase) < 0) return animator;
            }
            return fallback;
        }

        private Animator FindFaceAnimator(Transform root)
        {
            Animator fallback = null;
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            {
                if (animator == null || animator == targetAnimator) continue;
                if (animator.name.IndexOf(PreferredFaceLayerName, StringComparison.OrdinalIgnoreCase) >= 0) return animator;
                if (FindFaceLayerIndex(animator) >= 0) fallback = animator;
            }
            return fallback;
        }

        private bool TryGetFaceAnimatorAndLayer(out Animator animator, out int layer)
        {
            if (targetFaceAnimator != null)
            {
                animator = targetFaceAnimator;
                layer = Mathf.Max(0, FindFaceLayerIndex(animator));
                return animator.layerCount > 0;
            }
            animator = targetAnimator;
            layer = FindFaceLayerIndex(animator);
            return animator != null && layer >= 0;
        }

        private static int FindFaceLayerIndex(Animator animator)
        {
            if (animator == null) return -1;
            for (var i = 0; i < animator.layerCount; i++)
                if (animator.GetLayerName(i).IndexOf(PreferredFaceLayerName, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return -1;
        }

    }

    [DisallowMultipleComponent]
    internal sealed class CharacterDebugOverlayUpdateLoop : MonoBehaviour
    {
        private void Update() => CharacterDebugOverlay.TickActiveOverlay();
    }
}
