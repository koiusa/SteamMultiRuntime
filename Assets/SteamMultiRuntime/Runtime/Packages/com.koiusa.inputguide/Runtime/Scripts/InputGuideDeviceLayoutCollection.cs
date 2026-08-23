using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.InputGuide
{
    [Serializable]
    public sealed class InputGuideDeviceLayout
    {
        [SerializeField] private string id;
        [SerializeField] private InputGuidePanelSlot panelSlot;
        [SerializeField] private VisualTreeAsset defaultLayout;
        [SerializeField] private VisualTreeAsset layoutOverride;
        [SerializeField] private string[] controlLayouts = Array.Empty<string>();
        [SerializeField] private string[] requiredUsages = Array.Empty<string>();
        [SerializeField] private string exclusiveGroup;
        [SerializeField] private string hostElementName;
        [SerializeField] private bool defaultVisible = true;

        private VisualElement instance;
        private bool visible;

        public string Id => id;
        public InputGuidePanelSlot PanelSlot => panelSlot;
        public VisualTreeAsset DefaultLayout => defaultLayout;
        public VisualTreeAsset LayoutOverride => layoutOverride;
        public bool IsVisible => visible;

        internal string ExclusiveGroup => exclusiveGroup;
        internal void SetOverride(VisualTreeAsset value) => layoutOverride = value;
        internal void SetVisible(bool value)
        {
            visible = value;
            if (instance != null) instance.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        internal bool Matches(InputDevice device)
        {
            if (device == null) return false;
            var matchesControlLayout = false;
            for (var i = 0; i < (controlLayouts?.Length ?? 0); i++)
            {
                var layout = controlLayouts[i];
                if (!string.IsNullOrWhiteSpace(layout) &&
                    (string.Equals(device.layout, layout, StringComparison.OrdinalIgnoreCase) ||
                     InputSystem.IsFirstLayoutBasedOnSecond(device.layout, layout)))
                {
                    matchesControlLayout = true;
                    break;
                }
            }
            if (!matchesControlLayout) return false;

            for (var requiredIndex = 0; requiredIndex < (requiredUsages?.Length ?? 0); requiredIndex++)
            {
                var requiredUsage = requiredUsages[requiredIndex];
                if (string.IsNullOrWhiteSpace(requiredUsage)) continue;
                var found = false;
                for (var usageIndex = 0; usageIndex < device.usages.Count; usageIndex++)
                {
                    if (!string.Equals(device.usages[usageIndex].ToString(), requiredUsage,
                            StringComparison.OrdinalIgnoreCase)) continue;
                    found = true;
                    break;
                }
                if (!found) return false;
            }
            return true;
        }

        internal void Build(VisualElement root)
        {
            instance = null;
            var auxiliary = panelSlot == InputGuidePanelSlot.Device && string.IsNullOrWhiteSpace(exclusiveGroup);
            var host = root?.Q<VisualElement>(ResolveHostName(auxiliary));
            var asset = layoutOverride != null ? layoutOverride : defaultLayout;
            if (host == null || asset == null) return;

            // Keep every simulated device in its own layer. Cloning multiple
            // templates directly below the host makes them participate in the
            // same flex layout and can shrink layouts such as the keyboard.
            instance = new VisualElement { name = $"input-guide-device-layout-{id}" };
            instance.AddToClassList("input-guide-device-layout-entry");
            if (panelSlot == InputGuidePanelSlot.Device)
            {
                instance.AddToClassList(auxiliary ? "input-device-auxiliary-host" : "input-device-host");
            }
            host.Add(instance);
            asset.CloneTree(instance);
            visible = defaultVisible;
            SetVisible(visible);
        }

        internal void Configure(string valueId, InputGuidePanelSlot targetPanel,
            VisualTreeAsset layout, string group, bool initiallyVisible, params string[] layouts)
        {
            id = valueId;
            panelSlot = targetPanel;
            defaultLayout = layout;
            exclusiveGroup = group;
            defaultVisible = initiallyVisible;
            controlLayouts = layouts ?? Array.Empty<string>();
        }

        internal void ConfigureRequiredUsages(params string[] usages) =>
            requiredUsages = usages ?? Array.Empty<string>();

        internal void ConfigureHostElementName(string value) => hostElementName = value;

        private string ResolveHostName(bool auxiliary)
        {
            if (!string.IsNullOrWhiteSpace(hostElementName)) return hostElementName;
            return panelSlot switch
            {
                InputGuidePanelSlot.Device => auxiliary ? "device-auxiliary-layouts-host" : "device-layouts-host",
                InputGuidePanelSlot.Operations => "operations-device-layouts-host",
                _ => string.Empty
            };
        }

    }

    [DisallowMultipleComponent]
    public sealed class InputGuideDeviceLayoutCollection : MonoBehaviour
    {
        [SerializeField] private InputGuideDeviceLayout[] layouts = Array.Empty<InputGuideDeviceLayout>();

        internal void Build(VisualElement root)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < layouts.Length; i++)
            {
                var layout = layouts[i];
                if (layout == null) continue;
                if (string.IsNullOrWhiteSpace(layout.Id) || !ids.Add(layout.Id))
                {
                    Debug.LogError($"Input Guide device layout ID '{layout.Id}' is empty or duplicated.", this);
                    continue;
                }
                layout.Build(root);
            }
        }

        internal InputGuideDeviceLayout Get(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (var i = 0; i < layouts.Length; i++)
            {
                if (layouts[i] != null && string.Equals(layouts[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return layouts[i];
            }
            return null;
        }

        internal bool ShowForDevice(InputDevice device)
        {
            var matched = false;
            for (var i = 0; i < layouts.Length; i++)
            {
                if (layouts[i] != null && layouts[i].Matches(device))
                {
                    Show(layouts[i].Id);
                    matched = true;
                }
            }
            return matched;
        }

        internal void Show(string id)
        {
            var target = Get(id);
            if (target == null) return;
            var group = target.ExclusiveGroup;
            if (!string.IsNullOrWhiteSpace(group))
            {
                for (var i = 0; i < layouts.Length; i++)
                {
                    var layout = layouts[i];
                    if (layout != null && string.Equals(layout.ExclusiveGroup, group, StringComparison.Ordinal))
                        layout.SetVisible(ReferenceEquals(layout, target));
                }
            }
            else target.SetVisible(true);
        }

        internal void Configure(params InputGuideDeviceLayout[] values) =>
            layouts = values ?? Array.Empty<InputGuideDeviceLayout>();
    }
}
