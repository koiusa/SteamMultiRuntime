using System;
using System.Collections.Generic;
using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    internal sealed class KeyConfigView
    {
        private const string DropdownPopupStyleSheetPath = "UI/KeyConfig/KeyConfigDropdownPopup";
        private const float NavigationScrollStep = 72f;

        private readonly UIDocument uiDocument;
        private readonly VisualTreeAsset layoutAsset;
        private readonly StyleSheet styleSheet;
        private VisualElement root;
        private InputBindingIconResolver iconResolver;
        private StyleSheet dropdownPopupStyleSheet;
        private VisualElement dropdownPopupStyleHost;
        private VisualElement pendingPanelRoot;

        private Label statusLabel;
        private DropdownField bindingGroupDropdown;
        private ScrollView mapTabBar;
        private ScrollView bindingListView;
        private Button loadButton;
        private Button saveButton;
        private Button resetAllButton;
        private Button closeButton;
        private readonly KeyConfigConflictOverlay conflictOverlay = new KeyConfigConflictOverlay();
        private readonly KeyConfigBindingNavigation bindingNavigation = new KeyConfigBindingNavigation();

        private Action onLoad;
        private Action onSave;
        private Action onResetAll;
        private Action onClose;
        private Action<string> onBindingGroupChanged;

        private IReadOnlyList<InputBindingService.BindingEntry> cachedEntries;
        private Action<int> cachedOnRebind;
        private Action<int> cachedOnAddModifier;
        private Action<int> cachedOnRemoveModifier;
        private Action<int> cachedOnReset;
        private string selectedMapName;
        private readonly KeyConfigInputMonitor inputMonitor = new KeyConfigInputMonitor();
        private readonly KeyConfigRowLocalization rowLocalization = new KeyConfigRowLocalization();
        private LocalizedVisualTree localizedTree;
        private LocalizedTextBinding statusBinding;
        private IReadOnlyList<string> cachedBindingGroups;
        private string cachedSelectedBindingGroup;
        private bool isInteractive = true;
        private readonly List<string> mapNames = new List<string>();
        private readonly List<Button> mapTabButtons = new List<Button>();
        private readonly HashSet<Button> unavailableButtons = new HashSet<Button>();

        public KeyConfigView(UIDocument uiDocument, VisualTreeAsset layoutAsset, StyleSheet styleSheet)
        {
            this.uiDocument = uiDocument;
            this.layoutAsset = layoutAsset;
            this.styleSheet = styleSheet;
            KeyConfigLocalization.LocaleChanged += RefreshLocalizedUi;
        }

        public bool IsRenderable => statusLabel != null && bindingListView != null;

        public void Build()
        {
            root = uiDocument.rootVisualElement;
            root.Clear();
            ApplyDropdownPopupStyle(root);

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }

            if (layoutAsset != null)
            {
                layoutAsset.CloneTree(root);
                statusLabel = root.Q<Label>("status-label");
                statusBinding?.Dispose();
                statusBinding = statusLabel == null ? null : new LocalizedTextBinding(statusLabel);
                var inputMonitorDot = root.Q<Label>("input-monitor-dot");
                var inputMonitorStatus = root.Q<Label>("input-monitor-status");
                inputMonitor.Configure(inputMonitorDot, inputMonitorStatus);
                bindingListView = root.Q<ScrollView>("binding-list-view");

                var tableHeader = root.Q<VisualElement>("table-header");
                var insertParent = tableHeader?.parent ?? bindingListView?.parent;

                bindingGroupDropdown = new DropdownField("BindingGroup");
                bindingGroupDropdown.AddToClassList("keyconfig-binding-group-dropdown");
                mapTabBar = KeyConfigFallbackUiBuilder.CreateMapTabBar();
                mapTabBar.AddToClassList("keyconfig-map-tabs");

                if (insertParent != null && tableHeader != null)
                {
                    var headerIndex = insertParent.IndexOf(tableHeader);
                    insertParent.Insert(headerIndex, bindingGroupDropdown);
                    insertParent.Insert(headerIndex + 1, mapTabBar);
                }
                else if (insertParent != null && bindingListView != null)
                {
                    var listIndex = insertParent.IndexOf(bindingListView);
                    insertParent.Insert(listIndex, bindingGroupDropdown);
                    insertParent.Insert(listIndex + 1, mapTabBar);
                }

                loadButton = root.Q<Button>("load-button");
                saveButton = root.Q<Button>("save-button");
                resetAllButton = root.Q<Button>("reset-all-button");
                closeButton = root.Q<Button>("close-button");
                localizedTree?.Dispose();
                localizedTree = LocalizedVisualTree.Bind(root, statusLabel, inputMonitorStatus);
                return;
            }

            var fallback = KeyConfigFallbackUiBuilder.Build(root);
            statusLabel = fallback.StatusLabel;
            bindingGroupDropdown = fallback.BindingGroupDropdown;
            mapTabBar = fallback.MapTabBar;
            bindingListView = fallback.BindingListView;
            loadButton = fallback.LoadButton;
            saveButton = fallback.SaveButton;
            resetAllButton = fallback.ResetAllButton;
            closeButton = fallback.CloseButton;
            statusBinding?.Dispose();
            statusBinding = statusLabel == null ? null : new LocalizedTextBinding(statusLabel);
            inputMonitor.Configure(fallback.InputMonitorDot, fallback.InputMonitorStatus);
            localizedTree?.Dispose();
            localizedTree = LocalizedVisualTree.Bind(root, statusLabel, fallback.InputMonitorStatus);
        }

        public void BindActions(Action onLoadCallback, Action onSaveCallback, Action onResetAllCallback, Action onCloseCallback, Action<string> onBindingGroupChangedCallback)
        {
            onLoad = onLoadCallback;
            onSave = onSaveCallback;
            onResetAll = onResetAllCallback;
            onClose = onCloseCallback;
            onBindingGroupChanged = onBindingGroupChangedCallback;

            if (loadButton != null) loadButton.clicked += onLoad;
            if (saveButton != null) saveButton.clicked += onSave;
            if (resetAllButton != null) resetAllButton.clicked += onResetAll;
            if (closeButton != null) closeButton.clicked += onClose;
            if (bindingGroupDropdown != null)
            {
                bindingGroupDropdown.RegisterValueChangedCallback(OnBindingGroupDropdownValueChanged);
            }
            root?.RegisterCallback<NavigationCancelEvent>(OnNavigationCancel);
            root?.RegisterCallback<NavigationSubmitEvent>(OnNavigationSubmit, TrickleDown.TrickleDown);
            root?.RegisterCallback<FocusInEvent>(OnRootFocusIn);
        }

        public void UnbindActions()
        {
            if (loadButton != null && onLoad != null) loadButton.clicked -= onLoad;
            if (saveButton != null && onSave != null) saveButton.clicked -= onSave;
            if (resetAllButton != null && onResetAll != null) resetAllButton.clicked -= onResetAll;
            if (closeButton != null && onClose != null) closeButton.clicked -= onClose;
            if (bindingGroupDropdown != null)
            {
                bindingGroupDropdown.UnregisterValueChangedCallback(OnBindingGroupDropdownValueChanged);
            }
            root?.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel);
            root?.UnregisterCallback<NavigationSubmitEvent>(OnNavigationSubmit, TrickleDown.TrickleDown);
            root?.UnregisterCallback<FocusInEvent>(OnRootFocusIn);

            onLoad = null;
            onSave = null;
            onResetAll = null;
            onClose = null;
            onBindingGroupChanged = null;
        }

        public void Dispose()
        {
            DetachDropdownPopupStyle();
            KeyConfigLocalization.LocaleChanged -= RefreshLocalizedUi;
            localizedTree?.Dispose();
            localizedTree = null;
            statusBinding?.Dispose();
            statusBinding = null;
            inputMonitor.Dispose();
            rowLocalization.Dispose();
        }

        private void ApplyDropdownPopupStyle(VisualElement root)
        {
            dropdownPopupStyleSheet ??= Resources.Load<StyleSheet>(DropdownPopupStyleSheetPath);
            if (dropdownPopupStyleSheet == null)
            {
                Debug.LogWarning($"KeyConfigView: Dropdown popup stylesheet not found at '{DropdownPopupStyleSheetPath}'.");
                return;
            }

            if (root.panel != null)
            {
                AttachDropdownPopupStyle(root.panel.visualTree);
                return;
            }

            pendingPanelRoot = root;
            pendingPanelRoot.RegisterCallback<AttachToPanelEvent>(OnRootAttachedToPanel);
        }

        private void OnRootAttachedToPanel(AttachToPanelEvent evt)
        {
            pendingPanelRoot?.UnregisterCallback<AttachToPanelEvent>(OnRootAttachedToPanel);
            pendingPanelRoot = null;
            if (evt.destinationPanel != null)
                AttachDropdownPopupStyle(evt.destinationPanel.visualTree);
        }

        private void AttachDropdownPopupStyle(VisualElement panelRoot)
        {
            if (panelRoot == null || dropdownPopupStyleSheet == null) return;
            if (dropdownPopupStyleHost != null && dropdownPopupStyleHost != panelRoot)
                dropdownPopupStyleHost.styleSheets.Remove(dropdownPopupStyleSheet);

            dropdownPopupStyleHost = panelRoot;
            if (!panelRoot.styleSheets.Contains(dropdownPopupStyleSheet))
                panelRoot.styleSheets.Add(dropdownPopupStyleSheet);
        }

        private void DetachDropdownPopupStyle()
        {
            pendingPanelRoot?.UnregisterCallback<AttachToPanelEvent>(OnRootAttachedToPanel);
            pendingPanelRoot = null;
            if (dropdownPopupStyleHost != null && dropdownPopupStyleSheet != null)
                dropdownPopupStyleHost.styleSheets.Remove(dropdownPopupStyleSheet);
            dropdownPopupStyleHost = null;
        }

        public void SetLocalizedStatus(string key, params object[] arguments) => statusBinding?.Set(key, arguments);

        public void ShowConflict(string targetAction, string existingAction, Action replaceExisting, Action keepBoth, Action cancel)
        {
            conflictOverlay.Show(root, targetAction, existingAction, replaceExisting, keepBoth, cancel);
        }

        public void HideConflict() => conflictOverlay.Hide();

        public void SetStatus(string status)
        {
            if (statusLabel != null)
            {
                statusBinding?.Clear();
                statusLabel.text = string.IsNullOrWhiteSpace(status) ? string.Empty : status;
            }
        }

        public void SetInteractive(bool enabled, bool allowCloseWhenDisabled = false)
        {
            isInteractive = enabled;
            bindingGroupDropdown?.SetEnabled(enabled);
            root?.Query<Button>().ForEach(button => button.SetEnabled(
                !unavailableButtons.Contains(button) &&
                (enabled || (allowCloseWhenDisabled && button == closeButton))));
        }

        public void FocusDefault()
        {
            root?.schedule.Execute(() =>
            {
                if (!isInteractive || root.panel == null)
                {
                    return;
                }

                if (loadButton != null && loadButton.enabledInHierarchy)
                {
                    loadButton.Focus();
                    return;
                }

                var firstButton = root.Q<Button>();
                if (firstButton != null && firstButton.enabledInHierarchy)
                {
                    firstButton.Focus();
                }
            });
        }

        public void SetBindingGroupChoices(IReadOnlyList<string> groups, string selectedGroup)
        {
            cachedBindingGroups = groups;
            cachedSelectedBindingGroup = selectedGroup;
            RefreshBindingGroupChoices();
        }

        private void RefreshBindingGroupChoices()
        {
            if (bindingGroupDropdown == null)
            {
                return;
            }

            bindingGroupDropdown.label = KeyConfigLocalization.Get("keyconfig.binding_group");
            var allLabel = KeyConfigLocalization.Get("keyconfig.all");
            var choices = new List<string> { allLabel };
            if (cachedBindingGroups != null)
            {
                choices.AddRange(cachedBindingGroups);
            }

            bindingGroupDropdown.choices = choices;
            var value = string.IsNullOrWhiteSpace(cachedSelectedBindingGroup) ? allLabel : cachedSelectedBindingGroup;
            if (!choices.Contains(value))
            {
                value = allLabel;
            }

            bindingGroupDropdown.SetValueWithoutNotify(value);
        }

        public void SetIconResolver(InputBindingIconResolver resolver)
        {
            iconResolver = resolver;
        }

        public void SelectAdjacentSection(int direction)
        {
            if (!isInteractive || mapNames.Count == 0 || direction == 0)
            {
                return;
            }

            var focusedElement = root?.focusController?.focusedElement as VisualElement;
            var bindingGroupFocused = bindingGroupDropdown != null && focusedElement != null &&
                (focusedElement == bindingGroupDropdown || bindingGroupDropdown.Contains(focusedElement));
            var selectedMapIndex = Mathf.Max(0, mapNames.IndexOf(selectedMapName));
            var currentSectionIndex = bindingGroupFocused ? 0 : selectedMapIndex + 1;
            var sectionCount = mapNames.Count + 1;
            var nextSectionIndex = (currentSectionIndex + Math.Sign(direction) + sectionCount) % sectionCount;

            if (nextSectionIndex == 0)
            {
                bindingGroupDropdown?.Focus();
                return;
            }

            var nextMapIndex = nextSectionIndex - 1;
            selectedMapName = mapNames[nextMapIndex];
            RenderBindingEntries(cachedEntries, cachedOnRebind, cachedOnAddModifier, cachedOnRemoveModifier, cachedOnReset, true);
            root?.schedule.Execute(() =>
            {
                if (nextMapIndex < mapTabButtons.Count && mapTabButtons[nextMapIndex].enabledInHierarchy)
                {
                    mapTabButtons[nextMapIndex].Focus();
                    mapTabBar?.ScrollTo(mapTabButtons[nextMapIndex]);
                }
            });
        }

        public void RenderBindingEntries(
            IReadOnlyList<InputBindingService.BindingEntry> entries,
            Action<int> onRebind,
            Action<int> onAddModifier,
            Action<int> onRemoveModifier,
            Action<int> onReset,
            bool resetScroll = false)
        {
            if (bindingListView == null)
            {
                return;
            }

            var previousMapName = selectedMapName;
            var previousScrollOffset = bindingListView.scrollOffset;
            cachedEntries = entries;
            cachedOnRebind = onRebind;
            cachedOnAddModifier = onAddModifier;
            cachedOnRemoveModifier = onRemoveModifier;
            cachedOnReset = onReset;

            rowLocalization.Clear();
            bindingListView.Clear();
            mapTabBar?.Clear();
            inputMonitor.Clear();
            mapNames.Clear();
            mapTabButtons.Clear();
            bindingNavigation.Clear();
            unavailableButtons.Clear();

            if (entries == null || entries.Count == 0)
            {
                selectedMapName = null;
                var emptyLabel = new Label();
                rowLocalization.Bind(emptyLabel, "keyconfig.no_bindings");
                emptyLabel.AddToClassList("keyconfig-binding");
                bindingListView.Add(emptyLabel);
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                var mapName = entries[i].ActionMapName;
                if (mapNames.Contains(mapName))
                {
                    continue;
                }

                mapNames.Add(mapName);
            }

            if (string.IsNullOrWhiteSpace(selectedMapName) || !mapNames.Contains(selectedMapName))
            {
                selectedMapName = mapNames[0];
            }

            if (mapTabBar != null)
            {
                for (var i = 0; i < mapNames.Count; i++)
                {
                    var mapName = mapNames[i];
                    var tabButton = new Button(() =>
                    {
                        selectedMapName = mapName;
                        RenderBindingEntries(cachedEntries, cachedOnRebind, cachedOnAddModifier, cachedOnRemoveModifier, cachedOnReset, true);
                        EnterBindingList();
                    });
                    rowLocalization.Bind(tabButton, mapName);

                    tabButton.AddToClassList("keyconfig-map-tab-button");
                    tabButton.SetEnabled(isInteractive);
                    mapTabBar.Add(tabButton);
                    mapTabButtons.Add(tabButton);
                }
            }

            string currentSchemeName = null;
            string currentProfileName = null;
            string currentActionName = null;
            var rowCounter = 0;

            for (var i = 0; i < entries.Count; i++)
            {
                var rowIndex = i;
                var entry = entries[i];

                if (!string.Equals(entry.ActionMapName, selectedMapName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(currentSchemeName, entry.SchemeName, StringComparison.Ordinal))
                {
                    currentSchemeName = entry.SchemeName;
                    currentProfileName = null;
                    currentActionName = null;

                    var schemeHeader = new Label();
                    rowLocalization.Bind(schemeHeader, currentSchemeName);
                    schemeHeader.AddToClassList("keyconfig-scheme-group-header");
                    bindingListView.Add(schemeHeader);
                }

                if (!string.Equals(currentProfileName, entry.ProfileName, StringComparison.Ordinal))
                {
                    currentProfileName = entry.ProfileName;
                    currentActionName = null;

                    var profileHeader = new Label();
                    rowLocalization.Bind(profileHeader, currentProfileName);
                    profileHeader.AddToClassList("keyconfig-profile-group-header");
                    bindingListView.Add(profileHeader);
                }

                var isNewAction = !string.Equals(currentActionName, entry.ActionName, StringComparison.Ordinal);
                if (isNewAction)
                {
                    currentActionName = entry.ActionName;
                }

                var bindingRow = KeyConfigBindingRowFactory.Create(
                    entry, rowIndex, rowCounter++, isNewAction, isInteractive, iconResolver, unavailableButtons,
                    rowLocalization.Bind, rowLocalization.BindTooltip,
                    onRebind, onAddModifier, onRemoveModifier, onReset);
                bindingNavigation.Add(
                    rowIndex,
                    bindingRow.Row,
                    bindingRow.AddModifierButton,
                    bindingRow.RemoveModifierButton,
                    bindingRow.ChangeButton,
                    bindingRow.ResetButton);
                bindingListView.Add(bindingRow.Row);

                inputMonitor.Add(
                    entry,
                    bindingRow.Row,
                    bindingRow.InputStateLabel,
                    bindingRow.Control);
            }

            var targetScrollOffset = resetScroll ||
                !string.Equals(previousMapName, selectedMapName, StringComparison.Ordinal)
                ? Vector2.zero
                : previousScrollOffset;
            bindingListView.schedule.Execute(() => bindingListView.scrollOffset = targetScrollOffset);
        }

        public void UpdateInputStates(InputActionAsset inputActionAsset) => inputMonitor.Update(inputActionAsset);

        private void RefreshLocalizedUi()
        {
            RefreshBindingGroupChoices();
            rowLocalization.Refresh();
        }

        private void OnBindingGroupDropdownValueChanged(ChangeEvent<string> evt)
        {
            if (onBindingGroupChanged == null)
            {
                return;
            }

            var group = string.Equals(evt.newValue, KeyConfigLocalization.Get("keyconfig.all"), StringComparison.Ordinal)
                ? string.Empty
                : evt.newValue;
            cachedSelectedBindingGroup = group;
            onBindingGroupChanged.Invoke(group);
        }

        private void OnNavigationCancel(NavigationCancelEvent evt)
        {
            if (conflictOverlay.HandleCancel(root, evt)) return;

            if (!isInteractive || onClose == null)
            {
                return;
            }

            if (bindingNavigation.ContainsFocus(root))
            {
                FocusSelectedMapTab();
                root?.focusController?.IgnoreEvent(evt);
                evt.StopImmediatePropagation();
                return;
            }

            evt.StopImmediatePropagation();
            onClose.Invoke();
        }

        private void OnNavigationSubmit(NavigationSubmitEvent evt)
        {
            conflictOverlay.HandleSubmit(root, evt);
        }

        public void HandleNavigationMove(UiNavigationDirection direction)
        {
            if (conflictOverlay.HandleMove(root, direction)) return;

            if (!isInteractive)
            {
                return;
            }

            if (bindingNavigation.TryHandleMove(root, bindingListView, direction)) return;

            if (direction == UiNavigationDirection.Left ||
                direction == UiNavigationDirection.Right)
            {
                FocusAdjacentFunctionButton(direction == UiNavigationDirection.Left ? -1 : 1);
                return;
            }

            var focusedElement = root?.focusController?.focusedElement as VisualElement;
            if (bindingGroupDropdown != null && focusedElement != null &&
                (focusedElement == bindingGroupDropdown || bindingGroupDropdown.Contains(focusedElement)))
            {
                if ((direction == UiNavigationDirection.Up || direction == UiNavigationDirection.Down) &&
                    bindingGroupDropdown.choices.Count > 0)
                {
                    var offset = direction == UiNavigationDirection.Up ? -1 : 1;
                    var currentIndex = Mathf.Max(0, bindingGroupDropdown.index);
                    var nextIndex = (currentIndex + offset + bindingGroupDropdown.choices.Count) %
                        bindingGroupDropdown.choices.Count;
                    bindingGroupDropdown.index = nextIndex;
                }
                return;
            }

            if (bindingListView == null ||
                (direction != UiNavigationDirection.Up &&
                 direction != UiNavigationDirection.Down))
            {
                return;
            }

            var scrollDirection = direction == UiNavigationDirection.Up ? -1f : 1f;
            var currentOffset = bindingListView.scrollOffset;
            var contentHeight = bindingListView.contentContainer.layout.height;
            var viewportHeight = bindingListView.contentViewport.layout.height;
            var hasResolvedLayout = !float.IsNaN(contentHeight) && !float.IsNaN(viewportHeight);
            var maxOffset = hasResolvedLayout ? Mathf.Max(0f, contentHeight - viewportHeight) : 0f;
            bindingListView.scrollOffset = new Vector2(
                currentOffset.x,
                Mathf.Clamp(currentOffset.y + scrollDirection * NavigationScrollStep, 0f, maxOffset));
        }

        private void EnterBindingList()
        {
            bindingNavigation.FocusFirst(root, bindingListView);
        }

        private void OnRootFocusIn(FocusInEvent evt)
        {
            UpdateMapTabHighlights(evt.target as VisualElement);
        }

        private void UpdateMapTabHighlights(VisualElement focusedElement)
        {
            var showHighlight = focusedElement != null &&
                ((mapTabBar != null && mapTabBar.Contains(focusedElement)) ||
                 (bindingListView != null && bindingListView.Contains(focusedElement)));

            for (var i = 0; i < mapTabButtons.Count; i++)
            {
                var isSelected = i < mapNames.Count &&
                    string.Equals(mapNames[i], selectedMapName, StringComparison.Ordinal);
                mapTabButtons[i].EnableInClassList("active", showHighlight && isSelected);
            }
        }

        public void FocusBindingEntry(int entryIndex)
        {
            if (!bindingNavigation.FocusEntry(root, bindingListView, entryIndex)) FocusSelectedMapTab();
        }

        private void FocusSelectedMapTab()
        {
            var index = mapNames.IndexOf(selectedMapName);
            if (index >= 0 && index < mapTabButtons.Count)
            {
                mapTabButtons[index].Focus();
                mapTabBar?.ScrollTo(mapTabButtons[index]);
            }
        }

        private void FocusAdjacentFunctionButton(int direction)
        {
            VisualElement[] controls = { loadButton, saveButton, resetAllButton, closeButton };
            var focusedElement = root?.focusController?.focusedElement as VisualElement;
            var currentIndex = Array.IndexOf(controls, focusedElement);

            for (var offset = 1; offset <= controls.Length; offset++)
            {
                var index = currentIndex < 0
                    ? (direction > 0 ? offset - 1 : controls.Length - offset)
                    : (currentIndex + direction * offset + controls.Length) % controls.Length;
                var control = controls[index];
                if (control != null && control.enabledInHierarchy)
                {
                    control.Focus();
                    return;
                }
            }
        }
    }
}
