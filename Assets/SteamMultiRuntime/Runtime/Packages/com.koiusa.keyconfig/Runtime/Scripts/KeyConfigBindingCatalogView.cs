using System;
using System.Collections.Generic;
using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    internal sealed class KeyConfigBindingCatalogView : IDisposable
    {
        private readonly VisualElement root;
        private readonly ScrollView mapTabBar;
        private readonly ScrollView bindingListView;
        private readonly KeyConfigBindingNavigation bindingNavigation = new KeyConfigBindingNavigation();
        private readonly KeyConfigInputMonitor inputMonitor = new KeyConfigInputMonitor();
        private readonly KeyConfigRowLocalization rowLocalization = new KeyConfigRowLocalization();
        private readonly List<string> mapNames = new List<string>();
        private readonly List<Button> mapTabButtons = new List<Button>();
        private readonly HashSet<Button> unavailableButtons = new HashSet<Button>();

        private InputBindingIconResolver iconResolver;
        private IReadOnlyList<InputBindingService.BindingEntry> entries;
        private Action<int> onRebind;
        private Action<int> onAddModifier;
        private Action<int> onRemoveModifier;
        private Action<int> onReset;
        private string selectedMapName;
        private bool isInteractive = true;

        public KeyConfigBindingCatalogView(
            VisualElement root,
            ScrollView mapTabBar,
            ScrollView bindingListView,
            Label inputMonitorDot,
            Label inputMonitorStatus)
        {
            this.root = root;
            this.mapTabBar = mapTabBar;
            this.bindingListView = bindingListView;
            inputMonitor.Configure(inputMonitorDot, inputMonitorStatus);
        }

        public int MapCount => mapNames.Count;
        public int SelectedMapIndex => Mathf.Max(0, mapNames.IndexOf(selectedMapName));
        public bool IsUnavailable(Button button) => button != null && unavailableButtons.Contains(button);

        public void SetIconResolver(InputBindingIconResolver resolver) => iconResolver = resolver;
        public void SetInteractive(bool enabled) => isInteractive = enabled;

        public void Render(
            IReadOnlyList<InputBindingService.BindingEntry> bindingEntries,
            Action<int> rebind,
            Action<int> addModifier,
            Action<int> removeModifier,
            Action<int> reset,
            bool resetScroll = false)
        {
            if (bindingListView == null) return;

            var previousMapName = selectedMapName;
            var previousScrollOffset = bindingListView.scrollOffset;
            entries = bindingEntries;
            onRebind = rebind;
            onAddModifier = addModifier;
            onRemoveModifier = removeModifier;
            onReset = reset;

            ClearRenderedContent();
            if (entries == null || entries.Count == 0)
            {
                selectedMapName = null;
                var emptyLabel = new Label();
                rowLocalization.Bind(emptyLabel, "keyconfig.no_bindings");
                emptyLabel.AddToClassList("keyconfig-binding");
                bindingListView.Add(emptyLabel);
                return;
            }

            CollectMapNames();
            if (string.IsNullOrWhiteSpace(selectedMapName) || !mapNames.Contains(selectedMapName))
                selectedMapName = mapNames[0];

            BuildMapTabs();
            BuildBindingRows();

            var targetScrollOffset = resetScroll ||
                !string.Equals(previousMapName, selectedMapName, StringComparison.Ordinal)
                ? Vector2.zero
                : previousScrollOffset;
            bindingListView.schedule.Execute(() => bindingListView.scrollOffset = targetScrollOffset);
        }

        public void SelectMap(int index, bool focusTab)
        {
            if (index < 0 || index >= mapNames.Count) return;
            selectedMapName = mapNames[index];
            Render(entries, onRebind, onAddModifier, onRemoveModifier, onReset, true);
            if (!focusTab) return;
            root?.schedule.Execute(() => FocusMapTab(index));
        }

        public bool ContainsMapOrListFocus(VisualElement focused) => focused != null &&
            ((mapTabBar != null && mapTabBar.Contains(focused)) ||
             (bindingListView != null && bindingListView.Contains(focused)));

        public bool ContainsBindingFocus() => bindingNavigation.ContainsFocus(root);
        public bool TryHandleBindingMove(UiNavigationDirection direction) =>
            bindingNavigation.TryHandleMove(root, bindingListView, direction);
        public void EnterBindingList() => bindingNavigation.FocusFirst(root, bindingListView);

        public void FocusEntry(int entryIndex, int preferredColumn)
        {
            if (!bindingNavigation.FocusEntry(root, bindingListView, entryIndex, preferredColumn))
                FocusSelectedMapTab();
        }

        public void FocusSelectedMapTab() => FocusMapTab(mapNames.IndexOf(selectedMapName));

        public void UpdateTabHighlights(VisualElement focused)
        {
            var showHighlight = ContainsMapOrListFocus(focused);
            for (var i = 0; i < mapTabButtons.Count; i++)
            {
                var selected = i < mapNames.Count &&
                    string.Equals(mapNames[i], selectedMapName, StringComparison.Ordinal);
                mapTabButtons[i].EnableInClassList("active", showHighlight && selected);
            }
        }

        public void Scroll(int direction, float step)
        {
            if (bindingListView == null || direction == 0) return;
            var currentOffset = bindingListView.scrollOffset;
            var contentHeight = bindingListView.contentContainer.layout.height;
            var viewportHeight = bindingListView.contentViewport.layout.height;
            var hasResolvedLayout = !float.IsNaN(contentHeight) && !float.IsNaN(viewportHeight);
            var maxOffset = hasResolvedLayout ? Mathf.Max(0f, contentHeight - viewportHeight) : 0f;
            bindingListView.scrollOffset = new Vector2(
                currentOffset.x,
                Mathf.Clamp(currentOffset.y + Math.Sign(direction) * step, 0f, maxOffset));
        }

        public void UpdateInputStates(InputActionAsset asset) => inputMonitor.Update(asset);
        public void RefreshLocalization() => rowLocalization.Refresh();

        private void ClearRenderedContent()
        {
            rowLocalization.Clear();
            bindingListView.Clear();
            mapTabBar?.Clear();
            inputMonitor.Clear();
            mapNames.Clear();
            mapTabButtons.Clear();
            bindingNavigation.Clear();
            unavailableButtons.Clear();
        }

        private void CollectMapNames()
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var mapName = entries[i].ActionMapName;
                if (!mapNames.Contains(mapName)) mapNames.Add(mapName);
            }
        }

        private void BuildMapTabs()
        {
            if (mapTabBar == null) return;
            for (var i = 0; i < mapNames.Count; i++)
            {
                var mapName = mapNames[i];
                var tabButton = new Button(() =>
                {
                    selectedMapName = mapName;
                    Render(entries, onRebind, onAddModifier, onRemoveModifier, onReset, true);
                    EnterBindingList();
                });
                rowLocalization.Bind(tabButton, mapName);
                tabButton.AddToClassList("keyconfig-map-tab-button");
                tabButton.SetEnabled(isInteractive);
                mapTabBar.Add(tabButton);
                mapTabButtons.Add(tabButton);
            }
        }

        private void BuildBindingRows()
        {
            string currentSchemeName = null;
            string currentProfileName = null;
            string currentActionName = null;
            var rowCounter = 0;

            for (var i = 0; i < entries.Count; i++)
            {
                var rowIndex = i;
                var entry = entries[i];
                if (!string.Equals(entry.ActionMapName, selectedMapName, StringComparison.Ordinal)) continue;

                if (!string.Equals(currentSchemeName, entry.SchemeName, StringComparison.Ordinal))
                {
                    currentSchemeName = entry.SchemeName;
                    currentProfileName = null;
                    currentActionName = null;
                    AddHeader(currentSchemeName, "keyconfig-scheme-group-header");
                }

                if (!string.Equals(currentProfileName, entry.ProfileName, StringComparison.Ordinal))
                {
                    currentProfileName = entry.ProfileName;
                    currentActionName = null;
                    AddHeader(currentProfileName, "keyconfig-profile-group-header");
                }

                var isNewAction = !string.Equals(currentActionName, entry.ActionName, StringComparison.Ordinal);
                if (isNewAction) currentActionName = entry.ActionName;

                var bindingRow = KeyConfigBindingRowFactory.Create(
                    entry, rowIndex, rowCounter++, isNewAction, isInteractive, iconResolver, unavailableButtons,
                    rowLocalization.Bind, rowLocalization.BindTooltip,
                    onRebind, onAddModifier, onRemoveModifier, onReset);
                bindingNavigation.Add(
                    rowIndex, bindingRow.Row, bindingRow.AddModifierButton, bindingRow.RemoveModifierButton,
                    bindingRow.ChangeButton, bindingRow.ResetButton);
                bindingListView.Add(bindingRow.Row);
                inputMonitor.Add(entry, bindingRow.Row, bindingRow.InputStateLabel, bindingRow.Control);
            }
        }

        private void AddHeader(string key, string className)
        {
            var header = new Label();
            rowLocalization.Bind(header, key);
            header.AddToClassList(className);
            bindingListView.Add(header);
        }

        private void FocusMapTab(int index)
        {
            if (index < 0 || index >= mapTabButtons.Count) return;
            var button = mapTabButtons[index];
            if (!button.enabledInHierarchy) return;
            button.Focus();
            mapTabBar?.ScrollTo(button);
        }

        public void Dispose()
        {
            inputMonitor.Dispose();
            rowLocalization.Dispose();
        }
    }
}
