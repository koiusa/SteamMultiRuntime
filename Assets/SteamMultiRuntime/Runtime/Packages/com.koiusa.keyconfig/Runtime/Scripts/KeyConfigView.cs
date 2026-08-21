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
        private Label inputMonitorDot;
        private Label inputMonitorStatus;
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
        private readonly List<InputStateRow> inputStateRows = new List<InputStateRow>();
        private LocalizedVisualTree localizedTree;
        private LocalizedTextBinding statusBinding;
        private LocalizedTextBinding inputMonitorBinding;
        private readonly List<LocalizedTextBinding> rowBindings = new List<LocalizedTextBinding>();
        private readonly List<(VisualElement element, string key)> localizedTooltips = new List<(VisualElement, string)>();
        private int lastActiveInputCount = int.MinValue;
        private IReadOnlyList<string> cachedBindingGroups;
        private string cachedSelectedBindingGroup;
        private bool isInteractive = true;
        private readonly List<string> mapNames = new List<string>();
        private readonly List<Button> mapTabButtons = new List<Button>();
        private readonly HashSet<Button> unavailableButtons = new HashSet<Button>();

        private sealed class InputStateRow
        {
            public InputBindingService.BindingEntry Entry;
            public VisualElement Row;
            public Label StateLabel;
            public InputControl Control;
        }

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
                inputMonitorDot = root.Q<Label>("input-monitor-dot");
                inputMonitorStatus = root.Q<Label>("input-monitor-status");
                inputMonitorBinding?.Dispose();
                inputMonitorBinding = inputMonitorStatus == null ? null : new LocalizedTextBinding(inputMonitorStatus);
                bindingListView = root.Q<ScrollView>("binding-list-view");

                var tableHeader = root.Q<VisualElement>("table-header");
                var insertParent = tableHeader?.parent ?? bindingListView?.parent;

                bindingGroupDropdown = new DropdownField("BindingGroup");
                bindingGroupDropdown.AddToClassList("keyconfig-binding-group-dropdown");
                mapTabBar = CreateMapTabBar();
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

            BuildFallbackUi(root);
            statusBinding?.Dispose();
            statusBinding = statusLabel == null ? null : new LocalizedTextBinding(statusLabel);
            inputMonitorBinding?.Dispose();
            inputMonitorBinding = inputMonitorStatus == null ? null : new LocalizedTextBinding(inputMonitorStatus);
            localizedTree?.Dispose();
            localizedTree = LocalizedVisualTree.Bind(root, statusLabel, inputMonitorStatus);
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
            inputMonitorBinding?.Dispose();
            inputMonitorBinding = null;
            ClearRowBindings();
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
            RenderBindingEntries(cachedEntries, cachedOnRebind, cachedOnAddModifier, cachedOnRemoveModifier, cachedOnReset);
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
            Action<int> onReset)
        {
            if (bindingListView == null)
            {
                return;
            }

            cachedEntries = entries;
            cachedOnRebind = onRebind;
            cachedOnAddModifier = onAddModifier;
            cachedOnRemoveModifier = onRemoveModifier;
            cachedOnReset = onReset;

            ClearRowBindings();
            bindingListView.Clear();
            bindingListView.scrollOffset = Vector2.zero;
            mapTabBar?.Clear();
            inputStateRows.Clear();
            mapNames.Clear();
            mapTabButtons.Clear();
            bindingNavigation.Clear();
            unavailableButtons.Clear();

            if (entries == null || entries.Count == 0)
            {
                selectedMapName = null;
                var emptyLabel = new Label();
                BindRow(emptyLabel, "keyconfig.no_bindings");
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
                        RenderBindingEntries(cachedEntries, cachedOnRebind, cachedOnAddModifier, cachedOnRemoveModifier, cachedOnReset);
                        EnterBindingList();
                    });
                    BindRow(tabButton, mapName);

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
                    BindRow(schemeHeader, currentSchemeName);
                    schemeHeader.AddToClassList("keyconfig-scheme-group-header");
                    bindingListView.Add(schemeHeader);
                }

                if (!string.Equals(currentProfileName, entry.ProfileName, StringComparison.Ordinal))
                {
                    currentProfileName = entry.ProfileName;
                    currentActionName = null;

                    var profileHeader = new Label();
                    BindRow(profileHeader, currentProfileName);
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
                    BindRow, BindTooltip, onRebind, onAddModifier, onRemoveModifier, onReset);
                bindingNavigation.Add(
                    rowIndex,
                    bindingRow.Row,
                    bindingRow.AddModifierButton,
                    bindingRow.RemoveModifierButton,
                    bindingRow.ChangeButton,
                    bindingRow.ResetButton);
                bindingListView.Add(bindingRow.Row);

                inputStateRows.Add(new InputStateRow
                {
                    Entry = entry,
                    Row = bindingRow.Row,
                    StateLabel = bindingRow.InputStateLabel,
                    Control = bindingRow.Control
                });
            }

            bindingListView.schedule.Execute(() => bindingListView.scrollOffset = Vector2.zero);
        }

        public void UpdateInputStates(InputActionAsset inputActionAsset)
        {
            var activeCount = 0;
            for (var i = 0; i < inputStateRows.Count; i++)
            {
                var item = inputStateRows[i];
                if (!InputControlActivity.IsUsable(item.Control))
                {
                    item.Control = InputControlActivity.Resolve(item.Entry.BindingPath);
                }
                var activeControl = InputControlActivity.FindActive(item.Entry.BindingPath, item.Control);
                if (activeControl != null)
                {
                    item.Control = activeControl;
                }
                var magnitude = InputControlActivity.EvaluateMagnitude(item.Control);
                var isActive = activeControl != null;

                // Some controls do not expose a magnitude. Keep action-level input visible
                // as a fallback for custom controls and processor-driven bindings.
                if (!isActive && magnitude < 0f && inputActionAsset != null)
                {
                    var action = inputActionAsset.FindAction(item.Entry.ActionId.ToString());
                    isActive = action != null && action.IsPressed();
                }

                item.Row.EnableInClassList("input-active", isActive);
                item.StateLabel.style.display = isActive ? DisplayStyle.Flex : DisplayStyle.None;
                if (isActive)
                {
                    activeCount++;
                }
            }

            if (inputMonitorStatus != null && activeCount != lastActiveInputCount)
            {
                lastActiveInputCount = activeCount;
                if (activeCount == 0) inputMonitorBinding?.Set("keyconfig.waiting_input");
                else if (activeCount == 1) inputMonitorBinding?.Set("keyconfig.input_detected");
                else inputMonitorBinding?.Set("keyconfig.inputs_detected", activeCount);
            }

            if (inputMonitorDot != null)
            {
                inputMonitorDot.EnableInClassList("active", activeCount > 0);
            }
        }

        private void BuildFallbackUi(VisualElement root)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            container.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            container.style.paddingLeft = 24;
            container.style.paddingRight = 24;
            container.style.paddingTop = 24;
            container.style.paddingBottom = 24;
            root.Add(container);

            var title = new Label("keyconfig.title");
            title.style.fontSize = 30;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 12;
            container.Add(title);

            statusLabel = new Label("Ready");
            statusLabel.style.marginBottom = 12;
            container.Add(statusLabel);

            var monitor = new VisualElement();
            monitor.AddToClassList("keyconfig-input-monitor");
            inputMonitorDot = new Label("●");
            inputMonitorDot.AddToClassList("keyconfig-input-monitor-dot");
            inputMonitorStatus = new Label("WAITING FOR INPUT");
            inputMonitorStatus.AddToClassList("keyconfig-input-monitor-status");
            monitor.Add(inputMonitorDot);
            monitor.Add(inputMonitorStatus);
            container.Add(monitor);

            bindingGroupDropdown = new DropdownField("BindingGroup");
            bindingGroupDropdown.AddToClassList("keyconfig-binding-group-dropdown");
            container.Add(bindingGroupDropdown);

            mapTabBar = CreateMapTabBar();
            mapTabBar.AddToClassList("keyconfig-map-tabs");
            container.Add(mapTabBar);

            bindingListView = new ScrollView(ScrollViewMode.Vertical);
            bindingListView.style.flexGrow = 1;
            bindingListView.style.marginBottom = 12;
            container.Add(bindingListView);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            container.Add(buttonRow);

            loadButton = new Button { text = "keyconfig.load" };
            saveButton = new Button { text = "keyconfig.save" };
            resetAllButton = new Button { text = "keyconfig.reset_all" };
            closeButton = new Button { text = "keyconfig.close" };

            loadButton.style.width = 110;
            saveButton.style.width = 110;
            resetAllButton.style.width = 110;
            closeButton.style.width = 110;

            loadButton.style.marginLeft = 8;
            saveButton.style.marginLeft = 8;
            resetAllButton.style.marginLeft = 8;
            closeButton.style.marginLeft = 8;

            buttonRow.Add(loadButton);
            buttonRow.Add(saveButton);
            buttonRow.Add(resetAllButton);
            buttonRow.Add(closeButton);
        }

        private static ScrollView CreateMapTabBar()
        {
            var scrollView = new ScrollView(ScrollViewMode.Horizontal);
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scrollView.contentContainer.style.flexDirection = FlexDirection.Row;
            scrollView.contentContainer.style.flexWrap = Wrap.NoWrap;
            return scrollView;
        }

        private void BindRow(TextElement element, string key)
        {
            var binding = new LocalizedTextBinding(element);
            binding.Set(key);
            rowBindings.Add(binding);
        }

        private void BindTooltip(VisualElement element, string key)
        {
            localizedTooltips.Add((element, key));
            element.tooltip = KeyConfigLocalization.Get(key);
        }

        private void RefreshLocalizedUi()
        {
            RefreshBindingGroupChoices();
            for (var i = 0; i < localizedTooltips.Count; i++)
            {
                var item = localizedTooltips[i];
                if (item.element != null) item.element.tooltip = KeyConfigLocalization.Get(item.key);
            }
        }

        private void ClearRowBindings()
        {
            foreach (var binding in rowBindings)
                binding.Dispose();
            rowBindings.Clear();
            localizedTooltips.Clear();
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
