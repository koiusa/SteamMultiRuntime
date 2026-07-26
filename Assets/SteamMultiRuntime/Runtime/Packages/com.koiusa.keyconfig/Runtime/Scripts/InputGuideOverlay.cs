using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    /// <summary>Gameplay HUD that visualizes the current bindings and their live input state.</summary>
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public sealed class InputGuideOverlay : MonoBehaviour
    {
        private const string DefaultLayoutPath = "UI/InputGuide/InputGuideOverlay";

        [Header("Input")]
        [SerializeField] private KeyConfigInputActionsConfig inputActionsConfig;
        [Tooltip("Empty displays the first action map.")]
        [SerializeField] private string actionMapName = string.Empty;
        [Tooltip("Empty displays bindings from every control scheme.")]
        [SerializeField] private string bindingGroup = string.Empty;

        [Header("UI Toolkit")]
        [SerializeField] private VisualTreeAsset layoutAsset;
        [SerializeField] private InputBindingIconResolver iconResolver;
        [SerializeField] private bool startVisible = true;
        [SerializeField] private bool autoSwitchDeviceLayout = true;

        private readonly List<GuideRow> rows = new List<GuideRow>();
        private UIDocument uiDocument;
        private VisualElement overlay;
        private Label deviceLabel;
        private VisualElement keyboardLayout;
        private VisualElement mouseLayout;
        private VisualElement gamepadLayout;
        private InputActionAsset inputActionAsset;
        private InputDevice lastActiveDevice;
        private bool isGamepadLayoutVisible;

        private sealed class GuideRow
        {
            public VisualElement Element;
            public InputControl Control;
            public string BindingPath;
            public float ReleaseDelay;
            public float ActiveUntil;
        }

        public bool IsVisible => overlay != null && overlay.style.display != DisplayStyle.None;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            inputActionAsset = inputActionsConfig != null ? inputActionsConfig.Resolve() : null;
        }

        private void OnEnable()
        {
            Build();
            SetVisible(startVisible);
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
        }

        public void SetVisible(bool visible)
        {
            if (overlay != null)
            {
                overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void ToggleVisible()
        {
            SetVisible(!IsVisible);
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
            inputActionAsset = inputActionsConfig != null ? inputActionsConfig.Resolve() : null;
            Build();
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
            overlay = root.Q<VisualElement>("input-guide-overlay");
            deviceLabel = root.Q<Label>("device-label");
            if (deviceLabel != null)
            {
                deviceLabel.tooltip = "Click to switch keyboard / gamepad layout";
                deviceLabel.AddManipulator(new Clickable(ToggleDeviceLayout));
            }
            keyboardLayout = root.Q<VisualElement>("keyboard-layout");
            mouseLayout = root.Q<VisualElement>(className: "input-mouse");
            gamepadLayout = root.Q<VisualElement>("gamepad-layout");
            var deviceLayout = root.Q<VisualElement>("device-layout");
            var mapLabel = root.Q<Label>("map-label");

            if (inputActionAsset == null || deviceLayout == null)
            {
                mapLabel.text = "INPUT ASSET NOT SET";
                return;
            }

            var map = string.IsNullOrWhiteSpace(actionMapName)
                ? (inputActionAsset.actionMaps.Count > 0 ? inputActionAsset.actionMaps[0] : null)
                : inputActionAsset.FindActionMap(actionMapName, false);
            if (map == null)
            {
                mapLabel.text = "ACTION MAP NOT FOUND";
                return;
            }

            mapLabel.text = Nicify(map.name);
            SetGamepadLayout(IsGamepadLike(lastActiveDevice));
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

            // Keep physical controller buttons visible in the input monitor even
            // when the current action map does not assign an action to them.
            BindDebugControl(root, "control-start", "<Gamepad>/start");
            BindDebugControl(root, "control-select", "<Gamepad>/select");
            BindDebugControl(root, "control-systembutton", "<DualShockGamepad>/systemButton");
            BindDebugControl(root, "control-touchpadbutton", "<DualShockGamepad>/touchpadButton");
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
            if (keyboardLayout != null) keyboardLayout.style.display = showGamepad ? DisplayStyle.None : DisplayStyle.Flex;
            if (mouseLayout != null) mouseLayout.style.display = showGamepad ? DisplayStyle.None : DisplayStyle.Flex;
            if (gamepadLayout != null) gamepadLayout.style.display = showGamepad ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsGamepadLike(InputDevice device)
        {
            return device is Gamepad || device is Joystick;
        }

        private void BindControl(VisualElement root, InputAction action, int bindingIndex, InputBinding binding)
        {
            var path = string.IsNullOrWhiteSpace(binding.effectivePath)
                ? binding.path
                : binding.effectivePath;
            var controlName = GetControlName(path);
            var controlElement = root.Q<VisualElement>($"control-{controlName}");
            if (controlElement == null)
            {
                return;
            }

            var actionName = binding.isPartOfComposite
                ? $"{Nicify(action.name)} · {Nicify(binding.name)}"
                : Nicify(action.name);
            var actionLabel = new Label(actionName);
            actionLabel.AddToClassList("input-device-action");
            controlElement.Add(actionLabel);
            controlElement.tooltip = $"{action.GetBindingDisplayString(bindingIndex)} — {actionName}";

            rows.Add(new GuideRow
            {
                Element = controlElement,
                Control = InputControlActivity.Resolve(path),
                BindingPath = path,
                // Pointer delta commonly returns to zero between input events. A short
                // release delay makes motion read as continuous without delaying onset.
                ReleaseDelay = string.Equals(controlName, "delta", StringComparison.Ordinal)
                    ? 0.18f
                    : 0f
            });
        }

        private static string GetControlName(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
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
