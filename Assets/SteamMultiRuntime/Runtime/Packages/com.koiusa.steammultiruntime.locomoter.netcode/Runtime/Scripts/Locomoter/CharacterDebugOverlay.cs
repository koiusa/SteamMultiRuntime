using System;
using System.Collections.Generic;
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
        private const string CommonScrollStyleSheetPath = "UI/Common/SteamMultiRuntimeScrollView";
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

        private readonly List<LabelBinding> labelBindings = new();
        private IPlayerController playerController;
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
            if (!ActiveInstances.Contains(this)) ActiveInstances.Add(this);
            RefreshRendererOwnership();
        }

        private void OnDisable()
        {
            ActiveInstances.Remove(this);
            if (selectedInstanceIndex >= ActiveInstances.Count)
                selectedInstanceIndex = Mathf.Max(0, ActiveInstances.Count - 1);
            DestroyUi();
            RefreshRendererOwnership();
        }

        private void Update()
        {
            if (!ShouldOwnUi())
            {
                DestroyUi();
                return;
            }

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
            UpdateBoundLabels();
            UpdateTargetSelector(target);
        }

        public void Toggle() { isVisible = !isVisible; UpdateVisibility(); }
        public void Show() { isVisible = true; UpdateVisibility(); }
        public void Hide() { isVisible = false; UpdateVisibility(); }

        public void ResolveReferences()
        {
            var root = targetRoot != null ? targetRoot : FindPlayerRoot();
            displayScope = GetComponentInParent<CharacterDebugDisplayScope>();
            playerController = root.GetComponent<IPlayerController>();
            targetNetworkBehaviour = root.GetComponent<NetworkBehaviour>();
            if (targetRigidbody == null) targetRigidbody = root.GetComponent<Rigidbody>();
            if (targetAnimator == null) targetAnimator = FindBodyAnimator(root);
            if (targetFaceAnimator == null) targetFaceAnimator = FindFaceAnimator(root);
        }

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
            var commonScrollStyle = Resources.Load<StyleSheet>(CommonScrollStyleSheetPath);
            if (commonScrollStyle != null && !root.styleSheets.Contains(commonScrollStyle))
                root.styleSheets.Add(commonScrollStyle);
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
            launcherButton.style.left = windowPosition.x;
            launcherButton.style.top = windowPosition.y;
            launcherButton.style.width = launcherButtonSize.x;
            launcherButton.style.height = launcherButtonSize.y;
            overlayRoot.Add(launcherButton);

            window = new VisualElement { name = "character-debug-window" };
            window.style.position = Position.Absolute;
            window.style.left = windowPosition.x;
            window.style.top = windowPosition.y;
            window.style.width = windowWidth;
            window.style.height = windowHeight;
            window.style.paddingLeft = 8;
            window.style.paddingRight = 8;
            window.style.paddingBottom = 8;
            window.style.backgroundColor = new Color(0.08f, 0.08f, 0.09f, 0.96f);
            window.style.borderTopLeftRadius = 4;
            window.style.borderTopRightRadius = 4;
            window.style.borderBottomLeftRadius = 4;
            window.style.borderBottomRightRadius = 4;
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

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.AddToClassList("character-debug-scroll");
            scrollView.style.flexGrow = 1;
            window.Add(scrollView);
            content = scrollView.contentContainer;
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
            AddSection("Target", section =>
            {
                BindLabel(section, "Name", target.GetTargetDisplayName);
                BindLabel(section, "NetworkMode", GetNetworkModeDisplayName);
            });
            AddSection("Controller State", section => BindControllerLabels(section, target));
            AddSection("Rigidbody", section => BindRigidbodyLabels(section, target));
            AddSection("Body Animator", section => BindAnimatorLabels(section, target.targetAnimator, 0, true));
            AddSection("Face Animation", section => BindFaceAnimatorLabels(section, target));
            UpdateBoundLabels();
            UpdateTargetSelector(target);
        }

        private void AddSection(string title, Action<VisualElement> build)
        {
            var foldout = new Foldout { text = title, value = true };
            foldout.AddToClassList("character-debug-section");
            foldout.style.marginBottom = 4;
            content.Add(foldout);
            build(foldout);
        }

        private void BindLabel(VisualElement parent, string name, Func<string> value)
        {
            var label = new Label();
            label.AddToClassList("character-debug-value");
            parent.Add(label);
            labelBindings.Add(new LabelBinding { Label = label, Value = () => $"{name}: {value()}" });
        }

        private void BindControllerLabels(VisualElement parent, CharacterDebugOverlay target)
        {
            if (target.playerController == null) { parent.Add(new Label("IPlayerController: not found")); return; }
            BindLabel(parent, "Grounded", () => target.playerController.IsGrounded.ToString());
            BindLabel(parent, "Jumping", () => target.playerController.IsJumping.ToString());
            BindLabel(parent, "Freefall", () => target.playerController.IsFreefall.ToString());
            BindLabel(parent, "FallingAfterJump", () => target.playerController.IsFallingAfterJump.ToString());
            if (target.playerController is IPlayerLadderState ladder)
            {
                BindLabel(parent, "OnLadder", () => ladder.IsOnLadder.ToString());
                BindLabel(parent, "LadderSpeed", () => ladder.LadderSpeed.ToString("F3"));
            }
            BindLabel(parent, "HorizontalVelocity", () => target.playerController.HorizontalVelocity.ToString("F3"));
            BindLabel(parent, "VerticalVelocity", () => target.playerController.VerticalVelocity.ToString("F3"));
            BindLabel(parent, "MaxMoveSpeed", () => target.playerController.MaxMoveSpeed.ToString("F3"));
            BindLabel(parent, "InheritedGroundVelocity", () => target.playerController.InheritedGroundVelocity.ToString());
        }

        private void BindRigidbodyLabels(VisualElement parent, CharacterDebugOverlay target)
        {
            if (target.targetRigidbody == null) { parent.Add(new Label("Rigidbody: not found")); return; }
            BindLabel(parent, "Position", () => target.targetRigidbody.position.ToString());
            BindLabel(parent, "Velocity", () => target.targetRigidbody.linearVelocity.ToString());
            BindLabel(parent, "Speed", () => target.targetRigidbody.linearVelocity.magnitude.ToString("F3"));
            BindLabel(parent, "AngularVelocity", () => target.targetRigidbody.angularVelocity.ToString());
        }

        private void BindAnimatorLabels(VisualElement parent, Animator animator, int layer, bool parameters)
        {
            if (animator == null || layer < 0 || layer >= animator.layerCount)
            {
                parent.Add(new Label("Animator/layer not found"));
                return;
            }
            BindAnimatorState(parent, animator, layer);
            if (!parameters) return;
            parent.Add(new Label("Parameters"));
            foreach (var parameter in animator.parameters)
            {
                var captured = parameter;
                BindLabel(parent, captured.name, () => GetAnimatorParameterValue(animator, captured));
            }
        }

        private void BindFaceAnimatorLabels(VisualElement parent, CharacterDebugOverlay target)
        {
            if (!target.TryGetFaceAnimatorAndLayer(out var animator, out var layer))
            {
                parent.Add(new Label("Face animation layer/animator not found"));
                return;
            }
            BindLabel(parent, "Animator", () => animator.name);
            BindAnimatorState(parent, animator, layer);
        }

        private void BindAnimatorState(VisualElement parent, Animator animator, int layer)
        {
            BindLabel(parent, "Layer", () => $"{animator.GetLayerName(layer)} ({layer})");
            BindLabel(parent, "State", () => GetAnimatorStateDisplayName(animator, layer));
            BindLabel(parent, "NormalizedTime", () => animator.GetCurrentAnimatorStateInfo(layer).normalizedTime.ToString("F3"));
            BindLabel(parent, "LayerWeight", () => animator.GetLayerWeight(layer).ToString("F3"));
            BindLabel(parent, "Clip", () => GetAnimatorClipName(animator, layer));
        }

        private static string GetAnimatorParameterValue(Animator animator, AnimatorControllerParameter parameter)
        {
            return parameter.type switch
            {
                AnimatorControllerParameterType.Float => animator.GetFloat(parameter.nameHash).ToString("F3"),
                AnimatorControllerParameterType.Int => animator.GetInteger(parameter.nameHash).ToString(),
                AnimatorControllerParameterType.Bool => animator.GetBool(parameter.nameHash).ToString(),
                _ => "trigger"
            };
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
            targetSelectorLabel.text = target == null ? "No target" : $"{index + 1}/{targets.Count} {target.GetTargetDisplayName()}";
        }

        private void BeginDrag(PointerDownEvent evt)
        {
            if (evt.button != 0) return;
            dragging = true;
            dragStartPointer = evt.position;
            dragStartWindow = new Vector2(window.resolvedStyle.left, window.resolvedStyle.top);
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

        private static void RefreshRendererOwnership()
        {
            foreach (var overlay in ActiveInstances)
                if (overlay != null) overlay.nextRefreshTime = 0;
        }

        private Transform FindPlayerRoot()
        {
            for (var current = transform; current != null; current = current.parent)
                if (current.GetComponent<IPlayerController>() != null) return current;
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

        private string GetTargetDisplayName()
        {
            var root = targetRoot != null ? targetRoot : transform;
            return targetNetworkBehaviour == null ? root.name : $"{root.name} (Owner:{targetNetworkBehaviour.OwnerClientId})";
        }

        private static string GetNetworkModeDisplayName()
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsListening) return "Offline";
            if (manager.IsHost) return "Host";
            if (manager.IsServer) return "Server";
            return manager.IsClient ? "Client" : "Unknown";
        }

        private static string GetAnimatorStateDisplayName(Animator animator, int layer)
        {
            var clips = animator.GetCurrentAnimatorClipInfo(layer);
            return clips.Length > 0 && clips[0].clip != null
                ? clips[0].clip.name : animator.GetCurrentAnimatorStateInfo(layer).shortNameHash.ToString();
        }

        private static string GetAnimatorClipName(Animator animator, int layer)
        {
            var clips = animator.GetCurrentAnimatorClipInfo(layer);
            return clips.Length > 0 && clips[0].clip != null ? clips[0].clip.name : "none";
        }
    }
}
