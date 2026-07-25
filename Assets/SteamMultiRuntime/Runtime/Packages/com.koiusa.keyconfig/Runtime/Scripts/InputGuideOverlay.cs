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
        [SerializeField] private InputActionAssetResolver inputActionAssetResolver;
        [Tooltip("Empty displays the first action map.")]
        [SerializeField] private string actionMapName = string.Empty;
        [Tooltip("Empty displays bindings from every control scheme.")]
        [SerializeField] private string bindingGroup = string.Empty;

        [Header("UI Toolkit")]
        [SerializeField] private VisualTreeAsset layoutAsset;
        [SerializeField] private InputBindingIconResolver iconResolver;
        [SerializeField] private bool startVisible = true;

        private readonly List<GuideRow> rows = new List<GuideRow>();
        private UIDocument uiDocument;
        private VisualElement overlay;
        private Label deviceLabel;
        private InputActionAsset inputActionAsset;
        private InputDevice lastActiveDevice;

        private sealed class GuideRow
        {
            public VisualElement Element;
            public Label State;
            public InputControl Control;
        }

        public bool IsVisible => overlay != null && overlay.style.display != DisplayStyle.None;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            inputActionAsset = inputActionAssetResolver != null ? inputActionAssetResolver.Resolve() : null;
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
                var row = rows[i];
                var active = InputControlActivity.IsActive(row.Control);
                row.Element.EnableInClassList("active", active);
                row.State.text = active ? "INPUT" : string.Empty;
                if (active && row.Control?.device != null)
                {
                    lastActiveDevice = row.Control.device;
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

        public void Refresh()
        {
            inputActionAsset = inputActionAssetResolver != null ? inputActionAssetResolver.Resolve() : null;
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
            var list = root.Q<VisualElement>("guide-list");
            var mapLabel = root.Q<Label>("map-label");

            if (inputActionAsset == null || list == null)
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
            foreach (var action in map.actions)
            {
                for (var bindingIndex = 0; bindingIndex < action.bindings.Count; bindingIndex++)
                {
                    var binding = action.bindings[bindingIndex];
                    if (binding.isComposite || !IsInBindingGroup(binding.groups))
                    {
                        continue;
                    }

                    AddRow(list, action, bindingIndex, binding);
                }
            }
        }

        private void AddRow(VisualElement list, InputAction action, int bindingIndex, InputBinding binding)
        {
            var row = new VisualElement();
            row.AddToClassList("input-guide-row");

            var icon = new Image();
            icon.AddToClassList("input-guide-icon");
            var path = string.IsNullOrWhiteSpace(binding.effectivePath) ? binding.path : binding.effectivePath;
            icon.image = iconResolver != null ? iconResolver.Resolve(path) : null;
            icon.style.display = icon.image != null ? DisplayStyle.Flex : DisplayStyle.None;
            row.Add(icon);

            var key = new Label(action.GetBindingDisplayString(bindingIndex));
            key.AddToClassList("input-guide-key");
            row.Add(key);

            var actionName = binding.isPartOfComposite
                ? $"{Nicify(action.name)} · {Nicify(binding.name)}"
                : Nicify(action.name);
            var actionLabel = new Label(actionName);
            actionLabel.AddToClassList("input-guide-action");
            row.Add(actionLabel);

            var state = new Label();
            state.AddToClassList("input-guide-state");
            row.Add(state);
            list.Add(row);

            rows.Add(new GuideRow
            {
                Element = row,
                State = state,
                Control = InputControlActivity.Resolve(path)
            });
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
