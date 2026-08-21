using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig
{
    internal sealed class KeyConfigDropdownPopupStyleScope : IDisposable
    {
        private const string StyleSheetPath = "UI/KeyConfig/KeyConfigDropdownPopup";

        private StyleSheet styleSheet;
        private VisualElement styleHost;
        private VisualElement pendingRoot;

        public void AttachWhenPanelReady(VisualElement root)
        {
            Dispose();
            styleSheet ??= Resources.Load<StyleSheet>(StyleSheetPath);
            if (styleSheet == null)
            {
                Debug.LogWarning($"KeyConfigView: Dropdown popup stylesheet not found at '{StyleSheetPath}'.");
                return;
            }

            if (root?.panel != null)
            {
                Attach(root.panel.visualTree);
                return;
            }

            pendingRoot = root;
            pendingRoot?.RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
        }

        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            pendingRoot?.UnregisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            pendingRoot = null;
            if (evt.destinationPanel != null) Attach(evt.destinationPanel.visualTree);
        }

        private void Attach(VisualElement panelRoot)
        {
            if (panelRoot == null || styleSheet == null) return;
            if (styleHost != null && styleHost != panelRoot) styleHost.styleSheets.Remove(styleSheet);
            styleHost = panelRoot;
            if (!styleHost.styleSheets.Contains(styleSheet)) styleHost.styleSheets.Add(styleSheet);
        }

        public void Dispose()
        {
            pendingRoot?.UnregisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            pendingRoot = null;
            if (styleHost != null && styleSheet != null) styleHost.styleSheets.Remove(styleSheet);
            styleHost = null;
        }
    }
}
