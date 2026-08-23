using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.InputGuide
{
    /// <summary>Lists and validates every panel that composes an Input Guide.</summary>
    [DisallowMultipleComponent]
    public sealed class InputGuidePanelCollection : MonoBehaviour
    {
        private static readonly InputGuidePanelSlot[] RequiredPanelSlots =
        {
            InputGuidePanelSlot.Device,
            InputGuidePanelSlot.Operations
        };

        [SerializeField] private InputGuidePanelLayout[] panels = Array.Empty<InputGuidePanelLayout>();

        internal InputGuidePanelLayout Get(InputGuidePanelSlot panelSlot)
        {
            for (var i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];
                if (panel == null || panel.PanelSlot != panelSlot) continue;
                return panel;
            }
            return null;
        }

        internal void Build(VisualElement root)
        {
            for (var i = 0; i < RequiredPanelSlots.Length; i++)
            {
                if (Get(RequiredPanelSlots[i]) == null)
                    Debug.LogError($"Input Guide panel {RequiredPanelSlots[i]} is not registered.", this);
            }
            for (var i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];
                if (panel == null) continue;
                panel.Build(root);
            }
        }

        internal void Configure(params InputGuidePanelLayout[] values)
        {
            panels = values ?? Array.Empty<InputGuidePanelLayout>();
        }

        internal void SetAnchor(InputGuidePanelSlot panelSlot, InputGuidePanelAnchor anchor)
        {
            Get(panelSlot)?.SetAnchor(anchor);
        }

        internal void Refresh(InputGuidePanelSlot panelSlot)
        {
            for (var i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];
                if (panel != null && panel.PanelSlot == panelSlot) panel.Refresh();
            }
        }

        internal void SetVisible(InputGuidePanelSlot panelSlot, bool visible)
        {
            for (var i = 0; i < panels.Length; i++)
            {
                var panel = panels[i];
                if (panel != null && panel.PanelSlot == panelSlot) panel.SetVisible(visible);
            }
        }

    }
}
