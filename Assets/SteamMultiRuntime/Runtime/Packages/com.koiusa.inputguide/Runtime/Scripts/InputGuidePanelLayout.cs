using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.InputGuide
{
    /// <summary>Owns the visual asset and runtime anchor of one Input Guide panel.</summary>
    public sealed class InputGuidePanelLayout : MonoBehaviour
    {
        internal const string LeftClass = "input-guide-anchor--left";
        internal const string HorizontalCenterClass = "input-guide-anchor--horizontal-center";
        internal const string RightClass = "input-guide-anchor--right";
        internal const string TopClass = "input-guide-anchor--top";
        internal const string MiddleClass = "input-guide-anchor--middle";
        internal const string BottomClass = "input-guide-anchor--bottom";

        [SerializeField] private InputGuidePanelSlot panelSlot;
        [SerializeField] private string hostElementName;
        [SerializeField] private VisualTreeAsset defaultLayout;
        [SerializeField] private VisualTreeAsset layoutOverride;
        [SerializeField] private InputGuidePanelAnchor anchor = InputGuidePanelAnchor.TopRight;
        private VisualElement target;

        public InputGuidePanelSlot PanelSlot => panelSlot;
        public string HostElementName => hostElementName;
        public VisualTreeAsset DefaultLayout => defaultLayout;
        public VisualTreeAsset LayoutOverride => layoutOverride;
        public InputGuidePanelAnchor Anchor => anchor;

        internal void Build(VisualElement root)
        {
            target = root?.Q<VisualElement>(ResolveHostName());
            if (target == null) return;
            target.Clear();
            var layout = layoutOverride != null ? layoutOverride : defaultLayout;
            if (layout == null)
            {
                Debug.LogError($"No layout is configured for the {panelSlot} Input Guide panel.", this);
                Refresh();
                return;
            }
            layout.CloneTree(target);
            Refresh();
        }

        internal void Refresh()
        {
            if (target == null) return;
            target.EnableInClassList(LeftClass, IsLeft(anchor));
            target.EnableInClassList(HorizontalCenterClass, IsHorizontalCenter(anchor));
            target.EnableInClassList(RightClass, IsRight(anchor));
            target.EnableInClassList(TopClass, IsTop(anchor));
            target.EnableInClassList(MiddleClass, IsMiddle(anchor));
            target.EnableInClassList(BottomClass, IsBottom(anchor));
        }

        internal void Configure(
            InputGuidePanelSlot slot,
            InputGuidePanelAnchor value,
            VisualTreeAsset defaultAsset = null,
            string hostName = null)
        {
            panelSlot = slot;
            anchor = value;
            defaultLayout = defaultAsset;
            hostElementName = hostName;
        }

        internal void SetAnchor(InputGuidePanelAnchor value)
        {
            anchor = value;
            Refresh();
        }

        internal void SetLayoutOverride(VisualTreeAsset value)
        {
            layoutOverride = value;
        }

        private string ResolveHostName()
        {
            if (!string.IsNullOrWhiteSpace(hostElementName)) return hostElementName;
            return panelSlot switch
            {
                InputGuidePanelSlot.Device => "device-panel-host",
                InputGuidePanelSlot.Operations => "operations-panel-host",
                _ => string.Empty
            };
        }

        internal void SetVisible(bool value)
        {
            if (target != null) target.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static bool IsLeft(InputGuidePanelAnchor value) => value is
            InputGuidePanelAnchor.TopLeft or InputGuidePanelAnchor.MiddleLeft or InputGuidePanelAnchor.BottomLeft;
        private static bool IsHorizontalCenter(InputGuidePanelAnchor value) => value is
            InputGuidePanelAnchor.TopCenter or InputGuidePanelAnchor.Center or InputGuidePanelAnchor.BottomCenter;
        private static bool IsRight(InputGuidePanelAnchor value) => value is
            InputGuidePanelAnchor.TopRight or InputGuidePanelAnchor.MiddleRight or InputGuidePanelAnchor.BottomRight;
        private static bool IsTop(InputGuidePanelAnchor value) => value is
            InputGuidePanelAnchor.TopLeft or InputGuidePanelAnchor.TopCenter or InputGuidePanelAnchor.TopRight;
        private static bool IsMiddle(InputGuidePanelAnchor value) => value is
            InputGuidePanelAnchor.MiddleLeft or InputGuidePanelAnchor.Center or InputGuidePanelAnchor.MiddleRight;
        private static bool IsBottom(InputGuidePanelAnchor value) => value is
            InputGuidePanelAnchor.BottomLeft or InputGuidePanelAnchor.BottomCenter or InputGuidePanelAnchor.BottomRight;
    }
}
