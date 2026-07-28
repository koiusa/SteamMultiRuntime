using System;
using System.Collections.Generic;
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
        private VisualElement mapTabBar;
        private ScrollView bindingListView;
        private Button loadButton;
        private Button saveButton;
        private Button resetAllButton;
        private Button closeButton;

        private Action onLoad;
        private Action onSave;
        private Action onResetAll;
        private Action onClose;
        private Action<string> onBindingGroupChanged;

        private IReadOnlyList<InputBindingService.BindingEntry> cachedEntries;
        private Action<int> cachedOnRebind;
        private Action<int> cachedOnReset;
        private string selectedMapName;
        private readonly List<InputStateRow> inputStateRows = new List<InputStateRow>();
        private LocalizedVisualTree localizedTree;
        private LocalizedTextBinding statusBinding;
        private LocalizedTextBinding inputMonitorBinding;
        private readonly List<LocalizedTextBinding> rowBindings = new List<LocalizedTextBinding>();
        private int lastActiveInputCount = int.MinValue;
        private IReadOnlyList<string> cachedBindingGroups;
        private string cachedSelectedBindingGroup;
        private bool isInteractive = true;
        private readonly List<string> mapNames = new List<string>();
        private readonly List<Button> mapTabButtons = new List<Button>();
        private readonly List<BindingRowNavigation> bindingRows = new List<BindingRowNavigation>();

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
            KeyConfigLocalization.LocaleChanged += RefreshBindingGroupChoices;
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
                mapTabBar = new VisualElement();
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
            root?.RegisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);
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
            root?.UnregisterCallback<NavigationMoveEvent>(OnNavigationMove, TrickleDown.TrickleDown);

            onLoad = null;
            onSave = null;
            onResetAll = null;
            onClose = null;
            onBindingGroupChanged = null;
        }

        public void Dispose()
        {
            DetachDropdownPopupStyle();
            KeyConfigLocalization.LocaleChanged -= RefreshBindingGroupChoices;
            localizedTree?.Dispose();
            localizedTree = null;
            statusBinding?.Dispose();
            statusBinding = null;
            inputMonitorBinding?.Dispose();
            inputMonitorBinding = null;
            ClearRowBindings();
        }

        private sealed class BindingRowNavigation
        {
            public VisualElement Row;
            public Button RebindButton;
            public Button ResetButton;
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

        public void SetStatus(string status)
        {
            if (statusLabel != null)
            {
                statusBinding?.Clear();
                statusLabel.text = string.IsNullOrWhiteSpace(status) ? string.Empty : status;
            }
        }

        public void SetInteractive(bool enabled)
        {
            isInteractive = enabled;
            bindingGroupDropdown?.SetEnabled(enabled);
            root?.Query<Button>().ForEach(button => button.SetEnabled(button == closeButton || enabled));
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

        public void SelectAdjacentMap(int direction)
        {
            if (!isInteractive || mapNames.Count == 0 || direction == 0)
            {
                return;
            }

            var currentIndex = mapNames.IndexOf(selectedMapName);
            if (currentIndex < 0) currentIndex = 0;
            var nextIndex = (currentIndex + Math.Sign(direction) + mapNames.Count) % mapNames.Count;
            selectedMapName = mapNames[nextIndex];
            RenderBindingEntries(cachedEntries, cachedOnRebind, cachedOnReset);
            root?.schedule.Execute(() =>
            {
                if (nextIndex < mapTabButtons.Count && mapTabButtons[nextIndex].enabledInHierarchy)
                {
                    mapTabButtons[nextIndex].Focus();
                }
            });
        }

        public void RenderBindingEntries(
            IReadOnlyList<InputBindingService.BindingEntry> entries,
            Action<int> onRebind,
            Action<int> onReset)
        {
            if (bindingListView == null)
            {
                return;
            }

            cachedEntries = entries;
            cachedOnRebind = onRebind;
            cachedOnReset = onReset;

            ClearRowBindings();
            bindingListView.Clear();
            mapTabBar?.Clear();
            inputStateRows.Clear();
            mapNames.Clear();
            mapTabButtons.Clear();
            bindingRows.Clear();

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
                        RenderBindingEntries(cachedEntries, cachedOnRebind, cachedOnReset);
                        EnterBindingList();
                    })
                    {
                        text = mapName
                    };

                    tabButton.AddToClassList("keyconfig-map-tab-button");
                    tabButton.EnableInClassList("active", string.Equals(mapName, selectedMapName, StringComparison.Ordinal));
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

                    var schemeHeader = new Label(currentSchemeName);
                    schemeHeader.AddToClassList("keyconfig-scheme-group-header");
                    bindingListView.Add(schemeHeader);
                }

                if (!string.Equals(currentProfileName, entry.ProfileName, StringComparison.Ordinal))
                {
                    currentProfileName = entry.ProfileName;
                    currentActionName = null;

                    var profileHeader = new Label(currentProfileName);
                    profileHeader.AddToClassList("keyconfig-profile-group-header");
                    bindingListView.Add(profileHeader);
                }

                var isNewAction = !string.Equals(currentActionName, entry.ActionName, StringComparison.Ordinal);
                if (isNewAction)
                {
                    currentActionName = entry.ActionName;
                }

                // --- 行 ---
                var row = new VisualElement();
                row.AddToClassList("keyconfig-row");
                row.AddToClassList(rowCounter % 2 == 0 ? "even" : "odd");
                row.RegisterCallback<FocusInEvent>(OnBindingRowFocusIn);
                row.RegisterCallback<FocusOutEvent>(OnBindingRowFocusOut);
                rowCounter++;

                // アクション名セル（アクション初出のみ表示）
                var actionText = isNewAction && !entry.IsPartOfComposite
                    ? entry.ActionName
                    : (entry.IsPartOfComposite ? entry.DisplayName.Split('/')[0] : string.Empty);
                var actionCell = new Label(actionText);
                if (!string.IsNullOrEmpty(actionText))
                    BindRow(actionCell, actionText);
                actionCell.AddToClassList("keyconfig-cell-action");
                if (entry.IsPartOfComposite)
                {
                    actionCell.AddToClassList("composite-child");
                }
                row.Add(actionCell);

                // バインドセル（アイコン + ラベル）
                var bindingCell = new VisualElement();
                bindingCell.AddToClassList("keyconfig-cell-binding");

                if (iconResolver != null && !entry.IsComposite)
                {
                    var icon = iconResolver.Resolve(entry.BindingPath);
                    var iconElement = new Image();
                    iconElement.AddToClassList("keyconfig-binding-icon");
                    iconElement.image = icon;
                    iconElement.style.display = icon != null ? DisplayStyle.Flex : DisplayStyle.None;
                    bindingCell.Add(iconElement);
                }

                var bindingLabel = new Label(entry.IsComposite ? string.Empty : entry.DisplayName);
                bindingLabel.AddToClassList("keyconfig-binding-label");
                bindingCell.Add(bindingLabel);

                var inputStateLabel = new Label("●");
                inputStateLabel.AddToClassList("keyconfig-input-state");
                inputStateLabel.style.display = DisplayStyle.None;
                bindingCell.Add(inputStateLabel);
                row.Add(bindingCell);

                // ボタンセル
                var buttonCell = new VisualElement();
                buttonCell.AddToClassList("keyconfig-cell-buttons");

                var rebindButton = new Button(() => onRebind?.Invoke(rowIndex));
                BindRow(rebindButton, "keyconfig.change");
                rebindButton.AddToClassList("keyconfig-row-button");
                rebindButton.AddToClassList("keyconfig-rebind-button");
                rebindButton.SetEnabled(isInteractive && !entry.IsComposite && entry.IsRebindable);
                buttonCell.Add(rebindButton);

                var resetButton = new Button(() => onReset?.Invoke(rowIndex));
                BindRow(resetButton, "keyconfig.reset");
                resetButton.AddToClassList("keyconfig-row-button");
                resetButton.AddToClassList("keyconfig-reset-button");
                resetButton.SetEnabled(isInteractive && !entry.IsComposite && entry.IsRebindable);
                buttonCell.Add(resetButton);

                if (!entry.IsComposite && entry.IsRebindable)
                {
                    bindingRows.Add(new BindingRowNavigation
                    {
                        Row = row,
                        RebindButton = rebindButton,
                        ResetButton = resetButton
                    });
                }

                row.Add(buttonCell);
                bindingListView.Add(row);

                if (!entry.IsComposite)
                {
                    inputStateRows.Add(new InputStateRow
                    {
                        Entry = entry,
                        Row = row,
                        StateLabel = inputStateLabel,
                        Control = InputControlActivity.Resolve(entry.BindingPath)
                    });
                }
            }
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

            var title = new Label("キーコンフィグ");
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

            mapTabBar = new VisualElement();
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

            loadButton = new Button { text = "読込" };
            saveButton = new Button { text = "保存" };
            resetAllButton = new Button { text = "全リセット" };
            closeButton = new Button { text = "閉じる" };

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

        private void BindRow(TextElement element, string key)
        {
            var binding = new LocalizedTextBinding(element);
            binding.Set(key);
            rowBindings.Add(binding);
        }

        private void ClearRowBindings()
        {
            foreach (var binding in rowBindings)
                binding.Dispose();
            rowBindings.Clear();
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
            if (!isInteractive || onClose == null)
            {
                return;
            }

            if (TryGetFocusedBindingRow(out _, out _))
            {
                FocusSelectedMapTab();
                root?.focusController?.IgnoreEvent(evt);
                evt.StopImmediatePropagation();
                return;
            }

            evt.StopImmediatePropagation();
            onClose.Invoke();
        }

        private static void OnBindingRowFocusIn(FocusInEvent evt)
        {
            if (evt.currentTarget is VisualElement row)
            {
                row.AddToClassList("focused");
            }
        }

        private static void OnBindingRowFocusOut(FocusOutEvent evt)
        {
            if (evt.currentTarget is not VisualElement row)
            {
                return;
            }

            row.schedule.Execute(() =>
            {
                var focusedElement = row.panel?.focusController?.focusedElement as VisualElement;
                row.EnableInClassList("focused", focusedElement != null && row.Contains(focusedElement));
            });
        }

        private void OnNavigationMove(NavigationMoveEvent evt)
        {
            if (TryGetFocusedBindingRow(out var focusedRowIndex, out var resetColumn))
            {
                if (evt.direction == NavigationMoveEvent.Direction.Left ||
                    evt.direction == NavigationMoveEvent.Direction.Right)
                {
                    var target = evt.direction == NavigationMoveEvent.Direction.Left
                        ? bindingRows[focusedRowIndex].RebindButton
                        : bindingRows[focusedRowIndex].ResetButton;
                    target.Focus();
                    ConsumeNavigationMove(evt);
                    return;
                }

                if (evt.direction == NavigationMoveEvent.Direction.Up ||
                    evt.direction == NavigationMoveEvent.Direction.Down)
                {
                    var delta = evt.direction == NavigationMoveEvent.Direction.Up ? -1 : 1;
                    var targetIndex = Mathf.Clamp(focusedRowIndex + delta, 0, bindingRows.Count - 1);
                    var targetRow = bindingRows[targetIndex];
                    var target = resetColumn ? targetRow.ResetButton : targetRow.RebindButton;
                    target.Focus();
                    bindingListView?.ScrollTo(targetRow.Row);
                    ConsumeNavigationMove(evt);
                    return;
                }
            }

            if (evt.direction == NavigationMoveEvent.Direction.Left ||
                evt.direction == NavigationMoveEvent.Direction.Right)
            {
                FocusAdjacentFunctionButton(evt.direction == NavigationMoveEvent.Direction.Left ? -1 : 1);
                root?.focusController?.IgnoreEvent(evt);
                evt.StopImmediatePropagation();
                return;
            }

            var focusedElement = root?.focusController?.focusedElement as VisualElement;
            if (bindingGroupDropdown != null && focusedElement != null &&
                (focusedElement == bindingGroupDropdown || bindingGroupDropdown.Contains(focusedElement)))
            {
                return;
            }

            if (bindingListView == null ||
                (evt.direction != NavigationMoveEvent.Direction.Up &&
                 evt.direction != NavigationMoveEvent.Direction.Down))
            {
                return;
            }

            var direction = evt.direction == NavigationMoveEvent.Direction.Up ? -1f : 1f;
            var currentOffset = bindingListView.scrollOffset;
            var contentHeight = bindingListView.contentContainer.layout.height;
            var viewportHeight = bindingListView.contentViewport.layout.height;
            var hasResolvedLayout = !float.IsNaN(contentHeight) && !float.IsNaN(viewportHeight);
            var maxOffset = hasResolvedLayout ? Mathf.Max(0f, contentHeight - viewportHeight) : 0f;
            bindingListView.scrollOffset = new Vector2(
                currentOffset.x,
                Mathf.Clamp(currentOffset.y + direction * NavigationScrollStep, 0f, maxOffset));
            root?.focusController?.IgnoreEvent(evt);
            evt.StopImmediatePropagation();
        }

        private void EnterBindingList()
        {
            root?.schedule.Execute(() =>
            {
                if (bindingRows.Count == 0)
                {
                    return;
                }

                var firstRow = bindingRows[0];
                firstRow.RebindButton.Focus();
                bindingListView?.ScrollTo(firstRow.Row);
            });
        }

        private void FocusSelectedMapTab()
        {
            var index = mapNames.IndexOf(selectedMapName);
            if (index >= 0 && index < mapTabButtons.Count)
            {
                mapTabButtons[index].Focus();
            }
        }

        private bool TryGetFocusedBindingRow(out int rowIndex, out bool resetColumn)
        {
            var focusedElement = root?.focusController?.focusedElement as VisualElement;
            for (var i = 0; i < bindingRows.Count; i++)
            {
                if (focusedElement == bindingRows[i].RebindButton)
                {
                    rowIndex = i;
                    resetColumn = false;
                    return true;
                }

                if (focusedElement == bindingRows[i].ResetButton)
                {
                    rowIndex = i;
                    resetColumn = true;
                    return true;
                }
            }

            rowIndex = -1;
            resetColumn = false;
            return false;
        }

        private void ConsumeNavigationMove(NavigationMoveEvent evt)
        {
            root?.focusController?.IgnoreEvent(evt);
            evt.StopImmediatePropagation();
        }

        private void FocusAdjacentFunctionButton(int direction)
        {
            VisualElement[] controls = { bindingGroupDropdown, loadButton, saveButton, resetAllButton, closeButton };
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
