using System;
using System.Collections.Generic;
using Koiusa.Input.Icons;
using Koiusa.KeyConfig;
using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UIElements;

namespace Koiusa.InputGuide
{
    /// <summary>Gameplay HUD that visualizes the current bindings and their live input state.</summary>
    [RequireComponent(typeof(UIDocument))]
    [RequireComponent(typeof(InputGuidePanelCollection))]
    [RequireComponent(typeof(InputGuideDeviceLayoutCollection))]
    [DisallowMultipleComponent]
    public sealed class InputGuideOverlay : MonoBehaviour, IInputGuideOverlay
    {
        private const string DefaultLayoutPath = "UI/InputGuide/InputGuideOverlay";

        [Header("Input")]
        [SerializeField] private KeyConfigSettings inputActionsConfig;

        [Header("UI Toolkit")]
        [SerializeField] private VisualTreeAsset layoutAsset;
        [SerializeField] private KeyConfigIconSet iconResolver;
        [SerializeField] private bool startVisible;
        [SerializeField] private InputGuideLayoutPreset layoutPreset = InputGuideLayoutPreset.Standard;
        [SerializeField] private InputGuideToggleHintVisibility toggleHintVisibility = InputGuideToggleHintVisibility.PresetDefault;
        [SerializeField] private InputGuidePanelCollection panelCollection;
        [SerializeField] private InputGuideDeviceLayoutCollection deviceLayoutCollection;
        [SerializeField] private bool autoSwitchDeviceLayout = true;
        [SerializeField, Range(0f, 16f)] private float stickVisualTravel = 8f;
        [SerializeField, Range(0f, 0.5f)] private float stickVisualDeadzone = 0.08f;
        [SerializeField, Min(0f)] private float deviceIdleDelay = 1.5f;
        [SerializeField, Min(0.01f)] private float deviceFadeDuration = 0.2f;
        [SerializeField, Min(0f)] private float primaryDeviceSwitchLock = 0.25f;

        private readonly List<GuideRow> rows = new List<GuideRow>();
        private UIDocument uiDocument;
        private VisualElement overlay;
        private VisualElement devicePanel;
        private VisualElement operationPanel;
        private Label deviceLabel;
        private Label inputModeLabel;
        private VisualElement keyboardLayout;
        private VisualElement mouseLayout;
        private VisualElement gamepadLayout;
        private Label gamepadFaceWestLabel;
        private Label gamepadFaceNorthLabel;
        private Label gamepadFaceEastLabel;
        private Label gamepadFaceSouthLabel;
        private VisualElement leftStickVisual;
        private VisualElement rightStickVisual;
        private InputActionAsset inputActionAsset;
        private InputActionBinding debugToggleBinding;
        private InputDevice lastActiveDevice;
        private bool isGamepadLayoutVisible;
        private float lastMouseActivityTime;
        private float lastKeyboardActivityTime;
        private float lastGamepadActivityTime;
        private float lastPrimaryDeviceSwitchTime = float.NegativeInfinity;
        private bool primaryDeviceIsGamepad;
        private InputGuideDisplayMode displayMode = InputGuideDisplayMode.Hidden;
        private LocalizedVisualTree localizedTree;
        private bool bindingRefreshScheduled;
        private IVisualElementScheduledItem bindingRefresh;
        private VisualElement panelLifecycleRoot;
        private bool actionChangeSubscribed;
        private bool configurationApplied;
        private InputGuideSelection selection = InputGuideSelection.All();
        private readonly List<InputActionMap> displayedMaps = new List<InputActionMap>();
        private InputGuideOperationPanel operationPanelController;
        private ScrollView operationScrollView;

        private sealed class GuideRow
        {
            public VisualElement Element;
            public VisualElement SecondaryElement;
            public InputControl Control;
            public string BindingPath;
            public float ReleaseDelay;
            public float ActiveUntil;
        }

        public bool IsVisible => displayMode != InputGuideDisplayMode.Hidden;
        public InputGuideDisplayMode DisplayMode => displayMode;
        public InputGuideSelection Selection => selection;
        public InputGuideLayoutPreset LayoutPreset => layoutPreset;
        public InputGuideToggleHintVisibility ToggleHintVisibility => toggleHintVisibility;
        public event Action ConfiguredInputActionsChanged;

        /// <summary>Returns the Action Maps supplied by this overlay's Input Actions Config.</summary>
        public string[] GetAvailableActionMapNames()
        {
            var asset = ResolveConfiguredInputActions();
            if (asset == null) return Array.Empty<string>();

            var result = new string[asset.actionMaps.Count];
            for (var i = 0; i < result.Length; i++) result[i] = asset.actionMaps[i].name;
            return result;
        }

        /// <summary>Returns the binding groups supplied by this overlay's Input Actions Config.</summary>
        public string[] GetAvailableBindingGroups()
        {
            var asset = ResolveConfiguredInputActions();
            if (asset == null) return Array.Empty<string>();

            var result = new string[asset.controlSchemes.Count];
            for (var i = 0; i < result.Length; i++) result[i] = asset.controlSchemes[i].bindingGroup;
            return result;
        }

        /// <summary>Returns every Action as a stable Map/Action path.</summary>
        public string[] GetAvailableActionPaths()
        {
            var asset = ResolveConfiguredInputActions();
            if (asset == null) return Array.Empty<string>();

            var result = new List<string>();
            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions) result.Add($"{map.name}/{action.name}");
            }

            return result.ToArray();
        }

        public string[] GetAvailableVector2ActionPaths()
        {
            var asset = ResolveConfiguredInputActions();
            if (asset == null) return Array.Empty<string>();

            var result = new List<string>();
            foreach (var map in asset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    if (string.Equals(action.expectedControlType, "Vector2", StringComparison.Ordinal))
                    {
                        result.Add($"{map.name}/{action.name}");
                    }
                }
            }

            return result.ToArray();
        }

        internal InputAction FindConfiguredAction(string actionPath)
        {
            return string.IsNullOrWhiteSpace(actionPath)
                ? null
                : ResolveConfiguredInputActions()?.FindAction(actionPath, false);
        }

        private InputActionAsset ResolveConfiguredInputActions()
        {
            return inputActionAsset != null
                ? inputActionAsset
                : inputActionsConfig != null ? inputActionsConfig.Resolve() : null;
        }

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            if (panelCollection == null) panelCollection = GetComponent<InputGuidePanelCollection>();
            if (deviceLayoutCollection == null) deviceLayoutCollection = GetComponent<InputGuideDeviceLayoutCollection>();
            inputActionAsset = ResolveConfiguredInputActions();
        }

        private void OnEnable()
        {
            KeyConfigLocalization.LocaleChanged += RefreshLocalizedUi;
            AcquireDebugToggleInput();
            Build();
            BeginPanelLifecycle();
            SetDisplayMode(configurationApplied
                ? displayMode
                : startVisible ? InputGuideDisplayMode.Both : InputGuideDisplayMode.Hidden);
        }

        private void OnDisable()
        {
            KeyConfigLocalization.LocaleChanged -= RefreshLocalizedUi;
            EndPanelLifecycle();
            ReleaseDebugToggleInput();
            localizedTree?.Dispose();
            localizedTree = null;
        }

        private void BeginPanelLifecycle()
        {
            EndPanelLifecycle();
            panelLifecycleRoot = uiDocument?.rootVisualElement;
            if (panelLifecycleRoot == null) return;

            panelLifecycleRoot.RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            panelLifecycleRoot.RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
            if (panelLifecycleRoot.panel != null) SubscribeToActionChanges();
        }

        private void EndPanelLifecycle()
        {
            bindingRefresh?.Pause();
            bindingRefresh = null;
            bindingRefreshScheduled = false;
            UnsubscribeFromActionChanges();

            if (panelLifecycleRoot == null) return;
            panelLifecycleRoot.UnregisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            panelLifecycleRoot.UnregisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
            panelLifecycleRoot = null;
        }

        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            if (evt.target == panelLifecycleRoot) SubscribeToActionChanges();
        }

        private void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            if (evt.target != panelLifecycleRoot) return;
            bindingRefresh?.Pause();
            bindingRefresh = null;
            bindingRefreshScheduled = false;
            UnsubscribeFromActionChanges();
        }

        private void SubscribeToActionChanges()
        {
            if (actionChangeSubscribed) return;
            InputSystem.onActionChange += OnInputActionChange;
            actionChangeSubscribed = true;
        }

        private void UnsubscribeFromActionChanges()
        {
            if (!actionChangeSubscribed) return;
            InputSystem.onActionChange -= OnInputActionChange;
            actionChangeSubscribed = false;
        }

        private void Update()
        {
            if (!IsVisible)
            {
                return;
            }

            for (var i = 0; i < rows.Count; i++)
            {
                rows[i].Element.RemoveFromClassList("active");
                rows[i].SecondaryElement?.RemoveFromClassList("active");
            }

            InputDevice frameActiveDevice = null;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!InputControlActivity.IsUsable(row.Control))
                {
                    row.Control = InputControlActivity.Resolve(row.BindingPath);
                }
                var activeControl = InputControlActivity.FindActive(row.BindingPath, row.Control);
                var inputDetected = activeControl != null;
                if (inputDetected)
                {
                    row.Control = activeControl;
                    row.ActiveUntil = Time.unscaledTime + row.ReleaseDelay;
                }

                var active = inputDetected || Time.unscaledTime < row.ActiveUntil;
                if (active)
                {
                    row.Element.AddToClassList("active");
                    row.SecondaryElement?.AddToClassList("active");
                }

                if (inputDetected && row.Control?.device != null)
                {
                    var inputDevice = row.Control.device;
                    if (frameActiveDevice == null ||
                        (!IsGamepadLike(frameActiveDevice) && IsGamepadLike(inputDevice)))
                    {
                        frameActiveDevice = inputDevice;
                    }
                }
            }

            if (frameActiveDevice != null)
            {
                if (frameActiveDevice != lastActiveDevice)
                {
                    UpdateGamepadFaceLabels(frameActiveDevice);
                }
                lastActiveDevice = frameActiveDevice;
                if (autoSwitchDeviceLayout)
                {
                    deviceLayoutCollection?.ShowForDevice(lastActiveDevice);
                    SetGamepadLayout(IsGamepadLike(lastActiveDevice), false);
                }
            }

            if (deviceLabel != null)
            {
                deviceLabel.text = GetCurrentDeviceName(lastActiveDevice);
            }

            UpdateInputModeLabel();

            UpdateStickVisuals(lastActiveDevice as Gamepad ?? Gamepad.current);
            UpdateDeviceVisibility();
        }

        private void ToggleVisible()
        {
            CycleDisplayMode();
        }

        private void CycleDisplayMode()
        {
            var next = displayMode switch
            {
                InputGuideDisplayMode.Both => InputGuideDisplayMode.DeviceOnly,
                InputGuideDisplayMode.DeviceOnly => InputGuideDisplayMode.OperationsOnly,
                InputGuideDisplayMode.OperationsOnly => InputGuideDisplayMode.Hidden,
                _ => InputGuideDisplayMode.Both
            };
            SetDisplayMode(next);
        }

        private void SetDisplayMode(InputGuideDisplayMode mode)
        {
            displayMode = mode;
            if (overlay == null)
            {
                return;
            }

            overlay.style.display = mode == InputGuideDisplayMode.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
            var showDevices = mode is InputGuideDisplayMode.Both or InputGuideDisplayMode.DeviceOnly;
            var showOperations = mode is InputGuideDisplayMode.Both or InputGuideDisplayMode.OperationsOnly;
            panelCollection?.SetVisible(InputGuidePanelSlot.Device, showDevices);
            panelCollection?.SetVisible(InputGuidePanelSlot.Operations, showOperations);
        }

        private void ToggleDeviceLayout()
        {
            // A manual selection must not immediately be undone by the mouse event
            // generated by this click.
            autoSwitchDeviceLayout = false;
            SetGamepadLayout(!isGamepadLayoutVisible);
        }

        /// <summary>Captures the caller-controlled view state for later restoration.</summary>
        public InputGuideConfiguration CaptureConfiguration()
        {
            return new InputGuideConfiguration(
                displayMode,
                layoutPreset,
                toggleHintVisibility,
                GetPanelAnchor(InputGuidePanelSlot.Device),
                GetPanelAnchor(InputGuidePanelSlot.Operations));
        }

        public InputGuidePanelAnchor GetPanelAnchor(InputGuidePanelSlot panelSlot)
        {
            var layout = FindPanelLayout(panelSlot);
            if (layout != null) return layout.Anchor;
            return panelSlot switch
            {
                InputGuidePanelSlot.Device => InputGuidePanelAnchor.BottomLeft,
                _ => InputGuidePanelAnchor.TopRight
            };
        }

        public void SetPanelAnchor(InputGuidePanelSlot panelSlot, InputGuidePanelAnchor anchor)
        {
            var layout = FindPanelLayout(panelSlot);
            if (layout == null)
                throw new InvalidOperationException($"No panel layout is configured for {panelSlot}.");

            panelCollection.SetAnchor(panelSlot, anchor);
        }

        public VisualTreeAsset GetPanelLayoutOverride(InputGuidePanelSlot panelSlot)
        {
            return FindPanelLayout(panelSlot)?.LayoutOverride;
        }

        public void SetPanelLayoutOverride(InputGuidePanelSlot panelSlot, VisualTreeAsset layoutAsset)
        {
            var layout = FindPanelLayout(panelSlot);
            if (layout == null)
                throw new InvalidOperationException($"No panel layout is configured for {panelSlot}.");
            if (ReferenceEquals(layout.LayoutOverride, layoutAsset)) return;
            layout.SetLayoutOverride(layoutAsset);
            RebuildIfActive();
        }

        public VisualTreeAsset GetDeviceLayoutOverride(string layoutId) =>
            deviceLayoutCollection?.Get(layoutId)?.LayoutOverride;

        public void SetDeviceLayoutOverride(string layoutId, VisualTreeAsset layoutAsset)
        {
            var layout = deviceLayoutCollection?.Get(layoutId)
                ?? throw new ArgumentException($"Device layout '{layoutId}' is not registered.", nameof(layoutId));
            if (ReferenceEquals(layout.LayoutOverride, layoutAsset)) return;
            layout.SetOverride(layoutAsset);
            RebuildIfActive();
        }

        public void SetDeviceLayoutVisible(string layoutId, bool visible)
        {
            var layout = deviceLayoutCollection?.Get(layoutId)
                ?? throw new ArgumentException($"Device layout '{layoutId}' is not registered.", nameof(layoutId));
            layout.SetVisible(visible);
        }

        public void ShowDeviceLayout(string layoutId)
        {
            if (deviceLayoutCollection?.Get(layoutId) == null)
                throw new ArgumentException($"Device layout '{layoutId}' is not registered.", nameof(layoutId));
            deviceLayoutCollection.Show(layoutId);
        }

        /// <summary>Applies map selection and presentation as one rebuild.</summary>
        public void ApplyConfiguration(InputGuideConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            layoutPreset = configuration.LayoutPreset;
            toggleHintVisibility = configuration.ToggleHintVisibility;
            SetPanelAnchor(InputGuidePanelSlot.Device, configuration.DevicePanelAnchor);
            SetPanelAnchor(InputGuidePanelSlot.Operations, configuration.OperationsPanelAnchor);
            displayMode = configuration.DisplayMode;
            configurationApplied = true;

            if (isActiveAndEnabled && uiDocument != null)
            {
                Build();
                SetDisplayMode(displayMode);
            }
        }

        public void ApplySelection(InputGuideSelection value)
        {
            if (value == null)
            {
                return;
            }

            selection = value;
            RebuildIfActive();
        }

        /// <summary>Reapplies serialized Inspector settings while the overlay is running.</summary>
        internal void RefreshFromInspector()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || uiDocument == null)
            {
                return;
            }

            var resolvedAsset = inputActionsConfig != null ? inputActionsConfig.Resolve() : null;
            if (!ReferenceEquals(inputActionAsset, resolvedAsset))
            {
                ReleaseDebugToggleInput();
                inputActionAsset = resolvedAsset;
                AcquireDebugToggleInput();
                ConfiguredInputActionsChanged?.Invoke();
            }

            RebuildIfActive();
        }

        private void RebuildIfActive()
        {
            if (!isActiveAndEnabled || uiDocument == null)
            {
                return;
            }

            var previousMode = displayMode;
            Build();
            SetDisplayMode(previousMode);
        }

        private void OnInputActionChange(object changedObject, InputActionChange change)
        {
            if ((change != InputActionChange.BoundControlsChanged &&
                change != InputActionChange.ActionMapEnabled &&
                change != InputActionChange.ActionMapDisabled &&
                change != InputActionChange.ActionEnabled &&
                change != InputActionChange.ActionDisabled) ||
                bindingRefreshScheduled ||
                !BelongsToInputAsset(changedObject))
            {
                return;
            }

            var root = panelLifecycleRoot;
            if (root == null || root.panel == null)
            {
                UnsubscribeFromActionChanges();
                return;
            }

            bindingRefreshScheduled = true;
            bindingRefresh = root.schedule.Execute(() =>
            {
                bindingRefresh = null;
                bindingRefreshScheduled = false;
                if (isActiveAndEnabled && root.panel != null)
                {
                    RebuildIfActive();
                }
            });
        }

        private bool BelongsToInputAsset(object changedObject)
        {
            return changedObject switch
            {
                InputAction action => ReferenceEquals(action.actionMap?.asset, inputActionAsset),
                InputActionMap map => ReferenceEquals(map.asset, inputActionAsset),
                InputActionAsset asset => ReferenceEquals(asset, inputActionAsset),
                _ => false
            };
        }

        private void AcquireDebugToggleInput()
        {
            debugToggleBinding = InputActionBinding.Bind(
                inputActionAsset?.FindAction("System/DebugInputGuideToggle", false),
                OnDebugTogglePerformed);
        }

        private void ReleaseDebugToggleInput()
        {
            debugToggleBinding?.Dispose();
            debugToggleBinding = null;
        }

        private void OnDebugTogglePerformed(InputAction.CallbackContext context)
        {
            ToggleVisible();
        }

        public void SelectPreviousMapTab()
        {
            if (!AreOperationTabsVisible()) return;
            operationPanelController?.SelectPreviousMap();
            ResetOperationScroll();
        }

        public void SelectNextMapTab()
        {
            if (!AreOperationTabsVisible()) return;
            operationPanelController?.SelectNextMap();
            ResetOperationScroll();
        }

        public void ScrollOperationList(float direction, float distance = 90f)
        {
            if (!AreOperationTabsVisible() || operationScrollView == null || Mathf.Abs(direction) < 0.1f) return;
            var y = operationScrollView.scrollOffset.y - Mathf.Sign(direction) * Mathf.Max(1f, distance);
            var max = Mathf.Max(0f, operationScrollView.verticalScroller.highValue);
            operationScrollView.scrollOffset = new Vector2(0f, Mathf.Clamp(y, 0f, max));
        }

        private bool AreOperationTabsVisible()
        {
            return layoutPreset == InputGuideLayoutPreset.CompactOperations &&
                   displayMode is InputGuideDisplayMode.Both or InputGuideDisplayMode.OperationsOnly;
        }

        private void Build()
        {
            var root = uiDocument.rootVisualElement;
            root.Clear();
            rows.Clear();
            displayedMaps.Clear();
            operationPanelController = null;
            operationScrollView = null;

            var layout = layoutAsset != null
                ? layoutAsset
                : Resources.Load<VisualTreeAsset>(DefaultLayoutPath);
            if (layout == null)
            {
                Debug.LogError($"Input guide layout was not found at Resources/{DefaultLayoutPath}.", this);
                return;
            }

            layout.CloneTree(root);
            panelCollection?.Build(root);
            deviceLayoutCollection?.Build(root);
            overlay = root.Q<VisualElement>("input-guide-overlay");
            devicePanel = root.Q<VisualElement>(className: "input-guide-panel");
            operationPanel = root.Q<VisualElement>(className: "input-operation-panel");
            ApplyPresentationPreset();
            ApplyPanelLayouts();
            deviceLabel = root.Q<Label>("device-label");
            inputModeLabel = root.Q<Label>("input-mode-label");
            if (deviceLabel != null)
            {
                deviceLabel.tooltip = KeyConfigLocalization.Get("keyconfig.switch_device_tooltip");
                deviceLabel.AddManipulator(new Clickable(ToggleDeviceLayout));
            }
            keyboardLayout = root.Q<VisualElement>("keyboard-layout");
            mouseLayout = root.Q<VisualElement>(className: "input-mouse");
            if (mouseLayout != null)
            {
                mouseLayout.style.display = DisplayStyle.Flex;
                mouseLayout.style.opacity = 1f;
                lastMouseActivityTime = Time.unscaledTime;
            }
            gamepadLayout = root.Q<VisualElement>("gamepad-layout");
            var now = Time.unscaledTime;
            lastKeyboardActivityTime = now;
            lastGamepadActivityTime = now;
            var presentationDevice = ResolvePresentationDevice(lastActiveDevice);
            primaryDeviceIsGamepad = IsGamepadLike(presentationDevice);
            lastPrimaryDeviceSwitchTime = float.NegativeInfinity;
            gamepadFaceWestLabel = root.Q<Label>("gamepad-face-west-label");
            gamepadFaceNorthLabel = root.Q<Label>("gamepad-face-north-label");
            gamepadFaceEastLabel = root.Q<Label>("gamepad-face-east-label");
            gamepadFaceSouthLabel = root.Q<Label>("gamepad-face-south-label");
            leftStickVisual = root.Q<VisualElement>("control-leftstick");
            rightStickVisual = root.Q<VisualElement>("control-rightstick");
            var deviceLayout = root.Q<VisualElement>("device-layout");
            var mapLabel = root.Q<Label>("map-label");
            localizedTree?.Dispose();
            localizedTree = LocalizedVisualTree.Bind(root, deviceLabel, mapLabel, gamepadFaceWestLabel,
                gamepadFaceNorthLabel, gamepadFaceEastLabel, gamepadFaceSouthLabel);

            if (inputActionAsset == null || deviceLayout == null)
            {
                KeyConfigLocalization.Set(mapLabel, "keyconfig.input_asset_missing");
                return;
            }

            InputGuideMapSelection.Select(
                inputActionAsset, selection.MapFilter, selection.ActionMapNames, displayedMaps);
            if (displayedMaps.Count == 0)
            {
                KeyConfigLocalization.Set(mapLabel, "keyconfig.action_map_missing");
                UpdateInputModeLabel();
                return;
            }

            var localizedMapNames = new List<string>(displayedMaps.Count);
            foreach (var displayedMap in displayedMaps)
            {
                localizedMapNames.Add(KeyConfigLocalization.Get(displayedMap.name));
            }
            mapLabel.text = string.Join(" / ", localizedMapNames);
            UpdateInputModeLabel();
            deviceLayoutCollection?.ShowForDevice(presentationDevice);
            SetGamepadLayout(primaryDeviceIsGamepad, false);
            UpdateGamepadFaceLabels(presentationDevice);
            foreach (var map in displayedMaps)
            {
                foreach (var action in map.actions)
                {
                    for (var bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
                    {
                        var binding = action.bindings[bindingIndex];
                        if (binding.isComposite || !IsInBindingGroup(binding.groups))
                        {
                            continue;
                        }

                        BindControl(root, action, bindingIndex, binding);
                    }
                }
            }
            operationScrollView = root.Q<ScrollView>("input-operation-scroll-view");
            operationPanelController = new InputGuideOperationPanel(
                root.Q<VisualElement>("keyboard-operation-list"),
                root.Q<VisualElement>("gamepad-operation-list"),
                root.Q<VisualElement>("input-operation-map-tabs"),
                IsInBindingGroup);
            operationPanelController.Build(displayedMaps);
            operationPanelController.SetGamepadVisible(isGamepadLayoutVisible);

            // Keep physical controller buttons visible in the input monitor even
            // when the current action map does not assign an action to them.
            BindDebugControl(root, "control-start", "<Gamepad>/start");
            BindDebugControl(root, "control-select", "<Gamepad>/select");
            BindDebugControl(root, "control-systembutton", "<DualShockGamepad>/systemButton");
            BindDebugControl(root, "control-touchpadbutton", "<DualShockGamepad>/touchpadButton");
            BindDebugControl(root, "control-v", "<Keyboard>/v");
            BindDebugControl(root, "control-f", "<Keyboard>/f");
            BindDebugControl(root, "control-p", "<Keyboard>/p");
            BindDebugControl(root, "control-t", "<Keyboard>/t");
            BindDebugControl(root, "control-z", "<Keyboard>/z");
            BindDebugControl(root, "control-x", "<Keyboard>/x");
            BindDebugControl(root, "control-3", "<Keyboard>/3");
            BindDebugControl(root, "control-4", "<Keyboard>/4");
            BindDebugControl(root, "control-5", "<Keyboard>/5");
            BindDebugControl(root, "control-6", "<Keyboard>/6");
            BindDebugControl(root, "control-7", "<Keyboard>/7");
            BindDebugControl(root, "control-8", "<Keyboard>/8");
            BindDebugControl(root, "control-9", "<Keyboard>/9");
            BindDebugControl(root, "control-0", "<Keyboard>/0");
            BindDebugControl(root, "control-r", "<Keyboard>/r");
            BindDebugControl(root, "control-y", "<Keyboard>/y");
            BindDebugControl(root, "control-u", "<Keyboard>/u");
            BindDebugControl(root, "control-i", "<Keyboard>/i");
            BindDebugControl(root, "control-o", "<Keyboard>/o");
            BindDebugControl(root, "control-j", "<Keyboard>/j");
            BindDebugControl(root, "control-k", "<Keyboard>/k");
            BindDebugControl(root, "control-l", "<Keyboard>/l");
            BindDebugControl(root, "control-b", "<Keyboard>/b");
            BindDebugControl(root, "control-n", "<Keyboard>/n");
            BindDebugControl(root, "control-m", "<Keyboard>/m");
            BindDebugControl(root, "control-tab", "<Keyboard>/tab");
            BindDebugControl(root, "control-capslock", "<Keyboard>/capsLock");
        }

        private void ApplyPresentationPreset()
        {
            ApplyLayoutPreset(overlay, operationPanel, CaptureConfiguration());
        }

        internal static void ApplyLayoutPreset(
            VisualElement targetOverlay,
            VisualElement targetOperationPanel,
            InputGuideConfiguration configuration)
        {
            if (targetOverlay == null || configuration == null) return;
            var compact = configuration.LayoutPreset == InputGuideLayoutPreset.CompactOperations;
            targetOverlay.EnableInClassList("input-guide-screen--compact-operations", compact);
            var hint = targetOperationPanel?.Q<VisualElement>(className: "input-operation-toggle-hint");
            if (hint == null) return;
            hint.style.display = configuration.ToggleHintVisibility == InputGuideToggleHintVisibility.Visible ||
                                 (configuration.ToggleHintVisibility == InputGuideToggleHintVisibility.PresetDefault && !compact)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void ResetOperationScroll()
        {
            if (operationScrollView != null) operationScrollView.scrollOffset = Vector2.zero;
        }

        private void ApplyPanelLayouts()
        {
            panelCollection?.Refresh(InputGuidePanelSlot.Device);
            panelCollection?.Refresh(InputGuidePanelSlot.Operations);
        }

        private InputGuidePanelLayout FindPanelLayout(InputGuidePanelSlot panelSlot)
        {
            return panelCollection?.Get(panelSlot);
        }

        internal void ConfigurePanelCollection(InputGuidePanelCollection collection)
        {
            panelCollection = collection;
        }

        internal void ConfigureDeviceLayoutCollection(InputGuideDeviceLayoutCollection collection)
        {
            deviceLayoutCollection = collection;
        }


        private void BindDebugControl(VisualElement root, string elementName, string bindingPath)
        {
            var element = root.Q<VisualElement>(elementName);
            if (element == null || rows.Exists(row => row.Element == element))
            {
                return;
            }

            rows.Add(new GuideRow
            {
                Element = element,
                Control = InputControlActivity.Resolve(bindingPath),
                BindingPath = bindingPath
            });
        }

        private void SetGamepadLayout(bool showGamepad, bool updateDeviceLayout = true)
        {
            isGamepadLayoutVisible = showGamepad;
            if (updateDeviceLayout) deviceLayoutCollection?.Show(showGamepad ? "gamepad" : "keyboard");
            if (keyboardLayout != null) keyboardLayout.style.display = DisplayStyle.Flex;
            if (gamepadLayout != null) gamepadLayout.style.display = DisplayStyle.Flex;
            operationPanelController?.SetGamepadVisible(showGamepad);
        }

        private void RefreshLocalizedUi()
        {
            var previousMode = displayMode;
            Build();
            SetDisplayMode(previousMode);
        }

        private void UpdateGamepadFaceLabels(InputDevice device)
        {
            var isPlayStation = device is DualShockGamepad;
            if (gamepadFaceWestLabel != null) gamepadFaceWestLabel.text = isPlayStation ? "□" : "X";
            if (gamepadFaceNorthLabel != null) gamepadFaceNorthLabel.text = isPlayStation ? "△" : "Y";
            if (gamepadFaceEastLabel != null) gamepadFaceEastLabel.text = isPlayStation ? "○" : "B";
            if (gamepadFaceSouthLabel != null) gamepadFaceSouthLabel.text = isPlayStation ? "×" : "A";
        }

        private void UpdateStickVisuals(Gamepad gamepad)
        {
            var left = gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;
            var right = gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;
            var displayedLeft = ApplyStickVisualDeadzone(left);
            var displayedRight = ApplyStickVisualDeadzone(right);
            SetStickVisualPosition(leftStickVisual, displayedLeft);
            SetStickVisualPosition(rightStickVisual, displayedRight);
        }

        private void UpdateDeviceVisibility()
        {
            var now = Time.unscaledTime;
            var keyboard = Keyboard.current;
            var hasKeyboardActivity = keyboard != null && keyboard.anyKey.isPressed;
            if (hasKeyboardActivity)
            {
                lastKeyboardActivityTime = now;
            }

            var gamepad = Gamepad.current;
            var hasGamepadActivity = HasDeviceActivity(gamepad);
            if (hasGamepadActivity)
            {
                lastGamepadActivityTime = now;
            }

            // Simultaneous input keeps the current device to avoid rapid oscillation.
            if (hasKeyboardActivity != hasGamepadActivity)
            {
                TrySelectPrimaryDevice(hasGamepadActivity, now);
            }
            else if (primaryDeviceIsGamepad && gamepad == null && keyboard != null)
            {
                SelectPrimaryDevice(false, now);
            }
            else if (!primaryDeviceIsGamepad && keyboard == null && gamepad != null)
            {
                SelectPrimaryDevice(true, now);
            }

            var primaryAvailable = primaryDeviceIsGamepad ? gamepad != null : keyboard != null;
            var primaryActivityTime = primaryDeviceIsGamepad
                ? lastGamepadActivityTime
                : lastKeyboardActivityTime;
            if (keyboardLayout != null)
            {
                keyboardLayout.style.opacity = primaryDeviceIsGamepad ? 0f : 1f;
            }
            if (gamepadLayout != null)
            {
                gamepadLayout.style.opacity = primaryDeviceIsGamepad ? 1f : 0f;
            }
            UpdateDeviceOpacity(
                devicePanel,
                primaryAvailable,
                primaryActivityTime,
                now);

            if (mouseLayout == null)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                mouseLayout.style.opacity = 0f;
                return;
            }

            var hasActivity = mouse.delta.ReadValue().sqrMagnitude > 0.01f
                || mouse.scroll.ReadValue().sqrMagnitude > 0.01f
                || mouse.leftButton.isPressed
                || mouse.middleButton.isPressed
                || mouse.rightButton.isPressed;
            if (hasActivity)
            {
                lastMouseActivityTime = now;
                mouseLayout.style.opacity = 1f;
                return;
            }

            UpdateDeviceOpacity(mouseLayout, true, lastMouseActivityTime, now);
        }

        private void TrySelectPrimaryDevice(bool gamepad, float now)
        {
            if (primaryDeviceIsGamepad == gamepad
                || now - lastPrimaryDeviceSwitchTime < primaryDeviceSwitchLock)
            {
                return;
            }

            SelectPrimaryDevice(gamepad, now);
        }

        private void SelectPrimaryDevice(bool gamepad, float now)
        {
            primaryDeviceIsGamepad = gamepad;
            lastPrimaryDeviceSwitchTime = now;
        }

        private void UpdateDeviceOpacity(VisualElement element, bool deviceAvailable, float lastActivityTime, float now)
        {
            if (element == null)
            {
                return;
            }

            if (!deviceAvailable)
            {
                element.style.opacity = 0f;
                return;
            }

            var fadeElapsed = now - lastActivityTime - deviceIdleDelay;
            element.style.opacity = fadeElapsed <= 0f
                ? 1f
                : 1f - Mathf.Clamp01(fadeElapsed / Mathf.Max(0.01f, deviceFadeDuration));
        }

        private static bool HasDeviceActivity(InputDevice device)
        {
            if (device == null)
            {
                return false;
            }

            foreach (var control in device.allControls)
            {
                if (control.IsActuated(0.05f))
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2 ApplyStickVisualDeadzone(Vector2 value)
        {
            var magnitude = Mathf.Clamp01(value.magnitude);
            if (magnitude <= stickVisualDeadzone)
            {
                return Vector2.zero;
            }

            var range = Mathf.Max(0.0001f, 1f - stickVisualDeadzone);
            var remappedMagnitude = (magnitude - stickVisualDeadzone) / range;
            return value.normalized * remappedMagnitude;
        }

        private void SetStickVisualPosition(VisualElement stick, Vector2 value)
        {
            if (stick == null)
            {
                return;
            }

            stick.style.translate = new Translate(
                new Length(value.x * stickVisualTravel, LengthUnit.Pixel),
                new Length(-value.y * stickVisualTravel, LengthUnit.Pixel));
        }

        private static bool IsGamepadLike(InputDevice device)
        {
            return device is Gamepad || device is Joystick;
        }

        private void BindControl(VisualElement root, InputAction action, int bindingIndex, InputBinding binding)
        {
            var path = binding.overridePath != null ? binding.overridePath : binding.path;
            var controlName = GetControlName(path);
            var controlElement = root.Q<VisualElement>($"control-{controlName}");
            if (controlElement == null)
            {
                return;
            }

            var externalCaptionName = GetExternalCaptionName(path);
            var actionLabelHost = string.IsNullOrEmpty(externalCaptionName)
                ? controlElement
                : root.Q<VisualElement>(externalCaptionName) ?? controlElement;
            var localizedActionName = KeyConfigLocalization.Get(action.name);
            var actionName = binding.isPartOfComposite
                ? $"{localizedActionName} · {KeyConfigLocalization.Get(Nicify(binding.name))}"
                : localizedActionName;
            var actionLabel = actionLabelHost.Q<Label>("control-action-label");
            if (actionLabel == null)
            {
                actionLabel = new Label(actionName) { name = "control-action-label" };
                actionLabel.AddToClassList("input-device-action");
                actionLabelHost.Add(actionLabel);
            }
            else if (!actionLabel.text.Contains(actionName, StringComparison.Ordinal))
            {
                actionLabel.text += $" / {actionName}";
            }

            var tooltipEntry = $"{action.GetBindingDisplayString(bindingIndex)} — {actionName}";
            controlElement.tooltip = string.IsNullOrWhiteSpace(controlElement.tooltip)
                ? tooltipEntry
                : $"{controlElement.tooltip}\n{tooltipEntry}";

            var isTransientPointerInput = IsTransientPointerInput(path, controlName);
            rows.Add(new GuideRow
            {
                Element = controlElement,
                SecondaryElement = actionLabelHost != controlElement ? actionLabelHost : null,
                Control = InputControlActivity.Resolve(path),
                BindingPath = path,
                // Pointer delta and scroll return to zero between input events. A short
                // release delay keeps transient activity readable without delaying onset.
                ReleaseDelay = isTransientPointerInput ? 0.18f : 0f
            });
        }

        private static string GetExternalCaptionName(string path)
        {
            if (path.StartsWith("<Mouse>/scroll", StringComparison.OrdinalIgnoreCase))
            {
                return "control-scrollcaption";
            }

            return path.Equals("<Mouse>/middleButton", StringComparison.OrdinalIgnoreCase)
                ? "control-middlecaption"
                : string.Empty;
        }

        private static bool IsTransientPointerInput(string path, string controlName)
        {
            return string.Equals(controlName, "delta", StringComparison.Ordinal)
                || path.StartsWith("<Mouse>/scroll", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetControlName(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            if (path.StartsWith("<Mouse>/scroll/", StringComparison.OrdinalIgnoreCase)) return "middlebutton";
            if (path.StartsWith("<Pointer>/position", StringComparison.OrdinalIgnoreCase)) return "delta";
            var slash = path.LastIndexOf('/');
            var controlName = slash >= 0 ? path.Substring(slash + 1) : path;

            // Axis children such as rightStick/y share the visual for their parent stick.
            if (path.StartsWith("<Gamepad>/", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = path.Substring("<Gamepad>/".Length);
                var childSlash = relativePath.IndexOf('/');
                if (childSlash >= 0 && !relativePath.StartsWith("dpad/", StringComparison.OrdinalIgnoreCase))
                {
                    controlName = relativePath.Substring(0, childSlash);
                }
            }

            return controlName.ToLowerInvariant();
        }

        private bool IsInBindingGroup(string groups)
        {
            if (string.IsNullOrWhiteSpace(selection.BindingGroup))
            {
                return true;
            }

            var values = groups?.Split(';') ?? Array.Empty<string>();
            for (var i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], selection.BindingGroup, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetCurrentDeviceName(InputDevice lastDevice)
        {
            var device = ResolvePresentationDevice(lastDevice);
            return device != null ? device.displayName.ToUpperInvariant() : "NO DEVICE";
        }

        internal static InputDevice ResolvePresentationDevice(InputDevice lastDevice)
        {
            return ResolvePresentationDevice(lastDevice, Keyboard.current, Gamepad.current);
        }

        internal static InputDevice ResolvePresentationDevice(
            InputDevice lastDevice,
            Keyboard keyboard,
            Gamepad gamepad) => lastDevice ?? keyboard as InputDevice ?? gamepad;

        private void UpdateInputModeLabel()
        {
            if (inputModeLabel == null)
            {
                return;
            }

            if (inputActionAsset == null)
            {
                inputModeLabel.text = "MODE: NO INPUT ASSET";
                return;
            }

            var activeModes = new List<string>();
            foreach (var map in inputActionAsset.actionMaps)
            {
                if (map.enabled)
                {
                    activeModes.Add(KeyConfigLocalization.Get(map.name).ToUpperInvariant());
                }
            }
            inputModeLabel.text = activeModes.Count > 0
                ? $"MODE: {string.Join(" + ", activeModes)}"
                : "MODE: NONE";
        }

        private static string Nicify(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var result = value[0].ToString().ToUpperInvariant();
            for (var i = 1; i < value.Length; i++)
            {
                result += char.IsUpper(value[i]) && !char.IsWhiteSpace(value[i - 1]) ? " " : string.Empty;
                result += value[i];
            }
            return result;
        }
    }

    internal static class InputGuideMapSelection
    {
        public static void Select(InputActionAsset asset, InputGuideMapFilter filter,
            IReadOnlyList<string> specifiedNames, List<InputActionMap> result)
        {
            result.Clear();
            if (asset == null)
            {
                return;
            }

            if (filter == InputGuideMapFilter.All)
            {
                foreach (var map in asset.actionMaps) result.Add(map);
                return;
            }

            if (filter == InputGuideMapFilter.EnabledOnly)
            {
                foreach (var map in asset.actionMaps)
                {
                    if (map.enabled) result.Add(map);
                }
                return;
            }

            if (specifiedNames != null)
            {
                for (var i = 0; i < specifiedNames.Count; i++) AddNamedMap(asset, specifiedNames[i], result);
            }
        }

        private static void AddNamedMap(InputActionAsset asset, string name, List<InputActionMap> result)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            var map = asset.FindActionMap(name, false);
            if (map != null && !result.Contains(map)) result.Add(map);
        }
    }
}
