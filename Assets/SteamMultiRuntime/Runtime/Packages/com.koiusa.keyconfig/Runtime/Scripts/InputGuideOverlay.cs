using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    /// <summary>Gameplay HUD that visualizes the current bindings and their live input state.</summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class InputGuideOverlay : MonoBehaviour
    {
        public enum OverlayDisplayMode
        {
            Both,
            DeviceOnly,
            OperationsOnly,
            Hidden
        }

        private const string DefaultLayoutPath = "UI/InputGuide/InputGuideOverlay";
        private const string KeyboardLayoutPath = "UI/InputGuide/InputGuideKeyboard";
        private const string MouseLayoutPath = "UI/InputGuide/InputGuideMouse";
        private const string GamepadLayoutPath = "UI/InputGuide/InputGuideGamepad";

        [Header("Input")]
        [SerializeField] private KeyConfigInputActionsConfig inputActionsConfig;
        [Tooltip("Empty displays the first action map.")]
        [SerializeField] private string actionMapName = string.Empty;
        [Tooltip("Empty displays bindings from every control scheme.")]
        [SerializeField] private string bindingGroup = string.Empty;

        [Header("UI Toolkit")]
        [SerializeField] private VisualTreeAsset layoutAsset;
        [SerializeField] private InputBindingIconResolver iconResolver;
        [SerializeField] private bool startVisible;
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
        private VisualElement mouseLayoutHost;
        private Label deviceLabel;
        private Label inputModeLabel;
        private VisualElement keyboardLayout;
        private VisualElement mouseLayout;
        private VisualElement gamepadLayout;
        private VisualElement keyboardOperationList;
        private VisualElement gamepadOperationList;
        private Label gamepadFaceWestLabel;
        private Label gamepadFaceNorthLabel;
        private Label gamepadFaceEastLabel;
        private Label gamepadFaceSouthLabel;
        private VisualElement leftStickVisual;
        private VisualElement rightStickVisual;
        private InputActionAsset inputActionAsset;
        private InputAction debugToggleAction;
        private InputDevice lastActiveDevice;
        private bool isGamepadLayoutVisible;
        private float lastMouseActivityTime;
        private float lastKeyboardActivityTime;
        private float lastGamepadActivityTime;
        private float lastPrimaryDeviceSwitchTime = float.NegativeInfinity;
        private bool primaryDeviceIsGamepad;
        private OverlayDisplayMode displayMode = OverlayDisplayMode.Hidden;
        private LocalizedVisualTree localizedTree;
        private bool bindingRefreshScheduled;

        private sealed class GuideRow
        {
            public VisualElement Element;
            public VisualElement SecondaryElement;
            public InputControl Control;
            public string BindingPath;
            public float ReleaseDelay;
            public float ActiveUntil;
        }

        public bool IsVisible => displayMode != OverlayDisplayMode.Hidden;
        public OverlayDisplayMode DisplayMode => displayMode;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            inputActionAsset = inputActionsConfig != null ? inputActionsConfig.Resolve() : null;
        }

        private void OnEnable()
        {
            KeyConfigLocalization.LocaleChanged += RefreshLocalizedUi;
            InputSystem.onActionChange += OnInputActionChange;
            AcquireDebugToggleInput();
            Build();
            SetDisplayMode(startVisible ? OverlayDisplayMode.Both : OverlayDisplayMode.Hidden);
        }

        private void OnDisable()
        {
            KeyConfigLocalization.LocaleChanged -= RefreshLocalizedUi;
            InputSystem.onActionChange -= OnInputActionChange;
            bindingRefreshScheduled = false;
            ReleaseDebugToggleInput();
            localizedTree?.Dispose();
            localizedTree = null;
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
                    SetGamepadLayout(IsGamepadLike(lastActiveDevice));
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

        public void SetVisible(bool visible)
        {
            SetDisplayMode(visible ? OverlayDisplayMode.Both : OverlayDisplayMode.Hidden);
        }

        public void ToggleVisible()
        {
            CycleDisplayMode();
        }

        public void CycleDisplayMode()
        {
            var next = displayMode switch
            {
                OverlayDisplayMode.Both => OverlayDisplayMode.DeviceOnly,
                OverlayDisplayMode.DeviceOnly => OverlayDisplayMode.OperationsOnly,
                OverlayDisplayMode.OperationsOnly => OverlayDisplayMode.Hidden,
                _ => OverlayDisplayMode.Both
            };
            SetDisplayMode(next);
        }

        public void SetDisplayMode(OverlayDisplayMode mode)
        {
            displayMode = mode;
            if (overlay == null)
            {
                return;
            }

            var showDevices = mode is OverlayDisplayMode.Both or OverlayDisplayMode.DeviceOnly;
            var showOperations = mode is OverlayDisplayMode.Both or OverlayDisplayMode.OperationsOnly;
            overlay.style.display = mode == OverlayDisplayMode.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
            if (devicePanel != null)
            {
                devicePanel.style.display = showDevices ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (mouseLayoutHost != null)
            {
                mouseLayoutHost.style.display = showDevices ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (operationPanel != null)
            {
                operationPanel.style.display = showOperations ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void ShowKeyboardLayout()
        {
            autoSwitchDeviceLayout = false;
            SetGamepadLayout(false);
        }

        public void ShowGamepadLayout()
        {
            autoSwitchDeviceLayout = false;
            SetGamepadLayout(true);
        }

        public void ToggleDeviceLayout()
        {
            // A manual selection must not immediately be undone by the mouse event
            // generated by this click.
            autoSwitchDeviceLayout = false;
            SetGamepadLayout(!isGamepadLayoutVisible);
        }

        public void Refresh()
        {
            var previousMode = displayMode;
            ReleaseDebugToggleInput();
            inputActionAsset = inputActionsConfig != null ? inputActionsConfig.Resolve() : null;
            AcquireDebugToggleInput();
            Build();
            SetDisplayMode(previousMode);
        }

        private void OnInputActionChange(object changedObject, InputActionChange change)
        {
            if (change != InputActionChange.BoundControlsChanged ||
                bindingRefreshScheduled ||
                !BelongsToInputAsset(changedObject))
            {
                return;
            }

            bindingRefreshScheduled = true;
            uiDocument?.rootVisualElement.schedule.Execute(() =>
            {
                bindingRefreshScheduled = false;
                if (isActiveAndEnabled)
                {
                    Refresh();
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
            debugToggleAction = inputActionAsset?.FindAction("System/DebugInputGuideToggle", false);
            if (debugToggleAction == null)
            {
                return;
            }

            debugToggleAction.performed += OnDebugTogglePerformed;
            debugToggleAction.Enable();
        }

        private void ReleaseDebugToggleInput()
        {
            if (debugToggleAction == null)
            {
                return;
            }

            debugToggleAction.performed -= OnDebugTogglePerformed;
            debugToggleAction.Disable();
            debugToggleAction = null;
        }

        private void OnDebugTogglePerformed(InputAction.CallbackContext context)
        {
            ToggleVisible();
        }

        private void Build()
        {
            var root = uiDocument.rootVisualElement;
            root.Clear();
            rows.Clear();

            var layout = layoutAsset != null
                ? layoutAsset
                : Resources.Load<VisualTreeAsset>(DefaultLayoutPath);
            if (layout == null)
            {
                Debug.LogError($"Input guide layout was not found at Resources/{DefaultLayoutPath}.", this);
                return;
            }

            layout.CloneTree(root);
            CloneDeviceLayout(root, "keyboard-layout-host", KeyboardLayoutPath);
            CloneDeviceLayout(root, "mouse-layout-host", MouseLayoutPath);
            CloneDeviceLayout(root, "gamepad-layout-host", GamepadLayoutPath);
            overlay = root.Q<VisualElement>("input-guide-overlay");
            devicePanel = root.Q<VisualElement>(className: "input-guide-panel");
            operationPanel = root.Q<VisualElement>(className: "input-operation-panel");
            mouseLayoutHost = root.Q<VisualElement>("mouse-layout-host");
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
            primaryDeviceIsGamepad = IsGamepadLike(lastActiveDevice)
                || (Keyboard.current == null && Gamepad.current != null);
            lastPrimaryDeviceSwitchTime = float.NegativeInfinity;
            keyboardOperationList = root.Q<VisualElement>("keyboard-operation-list");
            gamepadOperationList = root.Q<VisualElement>("gamepad-operation-list");
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

            var map = string.IsNullOrWhiteSpace(actionMapName)
                ? (inputActionAsset.actionMaps.Count > 0 ? inputActionAsset.actionMaps[0] : null)
                : inputActionAsset.FindActionMap(actionMapName, false);
            if (map == null)
            {
                KeyConfigLocalization.Set(mapLabel, "keyconfig.action_map_missing");
                return;
            }

            mapLabel.text = Nicify(map.name);
            UpdateInputModeLabel();
            SetGamepadLayout(IsGamepadLike(lastActiveDevice));
            UpdateGamepadFaceLabels(lastActiveDevice);
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
            BuildOperationLists(map);

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

        private void CloneDeviceLayout(VisualElement root, string hostName, string resourcePath)
        {
            var host = root.Q<VisualElement>(hostName);
            if (host == null || host.childCount > 0)
            {
                return;
            }

            var deviceLayoutAsset = Resources.Load<VisualTreeAsset>(resourcePath);
            if (deviceLayoutAsset == null)
            {
                Debug.LogError($"Input guide device layout was not found at Resources/{resourcePath}.", this);
                return;
            }

            deviceLayoutAsset.CloneTree(host);
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

        private void SetGamepadLayout(bool showGamepad)
        {
            isGamepadLayoutVisible = showGamepad;
            if (keyboardLayout != null) keyboardLayout.style.display = DisplayStyle.Flex;
            if (gamepadLayout != null) gamepadLayout.style.display = DisplayStyle.Flex;
            if (keyboardOperationList != null) keyboardOperationList.style.display = showGamepad ? DisplayStyle.None : DisplayStyle.Flex;
            if (gamepadOperationList != null) gamepadOperationList.style.display = showGamepad ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void BuildOperationLists(InputActionMap map)
        {
            keyboardOperationList?.Clear();
            gamepadOperationList?.Clear();
            if (keyboardOperationList == null || gamepadOperationList == null)
            {
                return;
            }

            BuildOperationSections(map, keyboardOperationList, false);
            BuildOperationSections(map, gamepadOperationList, true);
        }

        private void RefreshLocalizedUi()
        {
            var previousMode = displayMode;
            Build();
            SetDisplayMode(previousMode);
        }

        private void BuildOperationSections(InputActionMap map, VisualElement target, bool gamepad)
        {
            for (var sectionIndex = 0; sectionIndex < 4; sectionIndex++)
            {
                var section = new VisualElement();
                section.AddToClassList("input-operation-section");
                var sectionTitle = new Label(GetOperationSectionTitle(sectionIndex));
                sectionTitle.AddToClassList("input-operation-section-title");
                section.Add(sectionTitle);
                var rowCount = 0;

                foreach (var action in map.actions)
                {
                    if (GetOperationSection(action.name) != sectionIndex)
                    {
                        continue;
                    }

                    var bindings = GetOperationBindings(action, gamepad);
                    if (bindings.Count == 0)
                    {
                        continue;
                    }

                    AddOperationRow(section, action.name, bindings);
                    rowCount++;
                }

                if (rowCount > 0)
                {
                    target.Add(section);
                }
            }
        }

        private List<string> GetOperationBindings(InputAction action, bool gamepad)
        {
            var result = new List<string>();
            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || !IsInBindingGroup(binding.groups))
                {
                    continue;
                }

                var path = binding.overridePath != null ? binding.overridePath : binding.path;
                if (gamepad ? !IsGamepadBinding(path) : !IsKeyboardMouseBinding(path))
                {
                    continue;
                }

                var displayName = action.GetBindingDisplayString(i);
                if (!string.IsNullOrWhiteSpace(displayName) && !result.Contains(displayName))
                {
                    result.Add(displayName);
                }
            }
            return result;
        }

        private static int GetOperationSection(string actionName)
        {
            return actionName switch
            {
                "Move" or "Jump" or "Sprint" or "Crouch" or "Dash" or "StrafeToggle" => 0,
                "Attack" or "Guard" or "Heal" or "LockOn" or "Previous" or "Next" => 1,
                "Grapple" or "GrappleFire" or "Reel" => 2,
                _ => 3
            };
        }

        private static string GetOperationSectionTitle(int sectionIndex)
        {
            return sectionIndex switch
            {
                0 => KeyConfigLocalization.Get("keyconfig.section_movement"),
                1 => KeyConfigLocalization.Get("keyconfig.section_combat"),
                2 => KeyConfigLocalization.Get("keyconfig.section_grapple"),
                _ => KeyConfigLocalization.Get("keyconfig.section_camera")
            };
        }

        private static void AddOperationRow(VisualElement target, string actionName, List<string> bindings)
        {
            if (bindings.Count == 0)
            {
                return;
            }

            var row = new VisualElement();
            row.AddToClassList("input-operation-row");
            var actionLabel = new Label(KeyConfigLocalization.Get(actionName));
            actionLabel.AddToClassList("input-operation-action");
            row.Add(actionLabel);
            var bindingLabel = new Label(string.Join(" / ", bindings));
            bindingLabel.AddToClassList("input-operation-binding");
            row.Add(bindingLabel);
            target.Add(row);
        }

        private static bool IsGamepadBinding(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && (path.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.IndexOf("Joystick", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsKeyboardMouseBinding(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && (path.IndexOf("Keyboard", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.IndexOf("Pointer", StringComparison.OrdinalIgnoreCase) >= 0);
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
            if (string.IsNullOrWhiteSpace(bindingGroup))
            {
                return true;
            }

            var values = groups?.Split(';') ?? Array.Empty<string>();
            for (var i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], bindingGroup, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetCurrentDeviceName(InputDevice lastDevice)
        {
            var device = lastDevice ?? Keyboard.current as InputDevice ?? Gamepad.current;
            return device != null ? device.displayName.ToUpperInvariant() : "NO DEVICE";
        }

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
            AddEnabledMode(activeModes, "Adventure");
            AddEnabledMode(activeModes, "Combat");
            AddEnabledMode(activeModes, "UI");
            inputModeLabel.text = activeModes.Count > 0
                ? $"MODE: {string.Join(" + ", activeModes)}"
                : "MODE: NONE";
        }

        private void AddEnabledMode(List<string> activeModes, string mapName)
        {
            var map = inputActionAsset.FindActionMap(mapName, false);
            if (map == null)
            {
                return;
            }

            foreach (var action in map.actions)
            {
                if (action.enabled)
                {
                    activeModes.Add(mapName.ToUpperInvariant());
                    return;
                }
            }
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
}
