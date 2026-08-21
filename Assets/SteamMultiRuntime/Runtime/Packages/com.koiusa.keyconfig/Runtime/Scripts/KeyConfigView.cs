using System;
using System.Collections.Generic;
using Koiusa.Input;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    /// <summary>Facade for the Key Config visual components.</summary>
    internal sealed class KeyConfigView
    {
        private readonly UIDocument uiDocument;
        private readonly VisualTreeAsset layoutAsset;
        private readonly StyleSheet styleSheet;
        private readonly KeyConfigConflictOverlay conflictOverlay = new KeyConfigConflictOverlay();
        private readonly KeyConfigDropdownPopupStyleScope popupStyleScope = new KeyConfigDropdownPopupStyleScope();
        private VisualElement root;
        private Label statusLabel;
        private Button loadButton;
        private Button saveButton;
        private Button resetAllButton;
        private Button closeButton;
        private InputBindingIconResolver iconResolver;
        private KeyConfigBindingGroupView bindingGroupView;
        private KeyConfigBindingCatalogView catalogView;
        private KeyConfigViewNavigation navigation;
        private LocalizedVisualTree localizedTree;
        private LocalizedTextBinding statusBinding;
        private Action onLoad;
        private Action onSave;
        private Action onResetAll;
        private Action onClose;
        private bool isInteractive = true;
        private bool localeSubscribed;

        public KeyConfigView(UIDocument uiDocument, VisualTreeAsset layoutAsset, StyleSheet styleSheet)
        {
            this.uiDocument = uiDocument;
            this.layoutAsset = layoutAsset;
            this.styleSheet = styleSheet;
        }

        public bool IsRenderable => statusLabel != null && catalogView != null;

        public void Build()
        {
            SubscribeLocalization();
            root = uiDocument.rootVisualElement;
            root.Clear();
            popupStyleScope.AttachWhenPanelReady(root);
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet)) root.styleSheets.Add(styleSheet);

            Label inputMonitorDot;
            Label inputMonitorStatus;
            DropdownField bindingGroupDropdown;
            ScrollView mapTabBar;
            ScrollView bindingListView;

            if (layoutAsset != null)
            {
                layoutAsset.CloneTree(root);
                statusLabel = root.Q<Label>("status-label");
                inputMonitorDot = root.Q<Label>("input-monitor-dot");
                inputMonitorStatus = root.Q<Label>("input-monitor-status");
                bindingListView = root.Q<ScrollView>("binding-list-view");
                bindingGroupDropdown = new DropdownField("BindingGroup");
                bindingGroupDropdown.AddToClassList("keyconfig-binding-group-dropdown");
                mapTabBar = KeyConfigFallbackUiBuilder.CreateMapTabBar();
                mapTabBar.AddToClassList("keyconfig-map-tabs");
                InsertSectionControls(bindingGroupDropdown, mapTabBar, bindingListView);
                loadButton = root.Q<Button>("load-button");
                saveButton = root.Q<Button>("save-button");
                resetAllButton = root.Q<Button>("reset-all-button");
                closeButton = root.Q<Button>("close-button");
            }
            else
            {
                var fallback = KeyConfigFallbackUiBuilder.Build(root);
                statusLabel = fallback.StatusLabel;
                inputMonitorDot = fallback.InputMonitorDot;
                inputMonitorStatus = fallback.InputMonitorStatus;
                bindingGroupDropdown = fallback.BindingGroupDropdown;
                mapTabBar = fallback.MapTabBar;
                bindingListView = fallback.BindingListView;
                loadButton = fallback.LoadButton;
                saveButton = fallback.SaveButton;
                resetAllButton = fallback.ResetAllButton;
                closeButton = fallback.CloseButton;
            }

            statusBinding?.Dispose();
            statusBinding = statusLabel == null ? null : new LocalizedTextBinding(statusLabel);
            localizedTree?.Dispose();
            localizedTree = LocalizedVisualTree.Bind(root, statusLabel, inputMonitorStatus);
            catalogView?.Dispose();
            bindingGroupView = new KeyConfigBindingGroupView(bindingGroupDropdown);
            catalogView = new KeyConfigBindingCatalogView(root, mapTabBar, bindingListView, inputMonitorDot, inputMonitorStatus);
            catalogView.SetIconResolver(iconResolver);
            navigation = new KeyConfigViewNavigation(root, bindingGroupView, catalogView, conflictOverlay,
                loadButton, saveButton, resetAllButton, closeButton);
        }

        private void InsertSectionControls(DropdownField group, ScrollView tabs, ScrollView list)
        {
            var tableHeader = root.Q<VisualElement>("table-header");
            var parent = tableHeader?.parent ?? list?.parent;
            if (parent == null) return;
            var index = tableHeader != null ? parent.IndexOf(tableHeader) : parent.IndexOf(list);
            parent.Insert(index, group);
            parent.Insert(index + 1, tabs);
        }

        public void BindActions(Action load, Action save, Action resetAll, Action close, Action<string> groupChanged)
        {
            onLoad = load;
            onSave = save;
            onResetAll = resetAll;
            onClose = close;
            if (loadButton != null) loadButton.clicked += onLoad;
            if (saveButton != null) saveButton.clicked += onSave;
            if (resetAllButton != null) resetAllButton.clicked += onResetAll;
            if (closeButton != null) closeButton.clicked += onClose;
            bindingGroupView?.Bind(groupChanged);
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
            bindingGroupView?.Unbind();
            root?.UnregisterCallback<NavigationCancelEvent>(OnNavigationCancel);
            root?.UnregisterCallback<NavigationSubmitEvent>(OnNavigationSubmit, TrickleDown.TrickleDown);
            root?.UnregisterCallback<FocusInEvent>(OnRootFocusIn);
            onLoad = null;
            onSave = null;
            onResetAll = null;
            onClose = null;
        }

        public void Dispose()
        {
            popupStyleScope.Dispose();
            if (localeSubscribed)
            {
                KeyConfigLocalization.LocaleChanged -= RefreshLocalizedUi;
                localeSubscribed = false;
            }
            localizedTree?.Dispose();
            localizedTree = null;
            statusBinding?.Dispose();
            statusBinding = null;
            catalogView?.Dispose();
            catalogView = null;
            bindingGroupView = null;
            navigation = null;
        }

        public void SetLocalizedStatus(string key, params object[] arguments) => statusBinding?.Set(key, arguments);
        public void ShowConflict(string targetAction, string existingAction, Action replace, Action keep, Action cancel) =>
            conflictOverlay.Show(root, targetAction, existingAction, replace, keep, cancel);
        public void HideConflict() => conflictOverlay.Hide();

        public void SetStatus(string status)
        {
            if (statusLabel == null) return;
            statusBinding?.Clear();
            statusLabel.text = string.IsNullOrWhiteSpace(status) ? string.Empty : status;
        }

        public void SetInteractive(bool enabled, bool allowCloseWhenDisabled = false)
        {
            isInteractive = enabled;
            bindingGroupView?.SetInteractive(enabled);
            catalogView?.SetInteractive(enabled);
            root?.Query<Button>().ForEach(button => button.SetEnabled(
                catalogView?.IsUnavailable(button) != true &&
                (enabled || (allowCloseWhenDisabled && button == closeButton))));
        }

        public void FocusDefault()
        {
            root?.schedule.Execute(() =>
            {
                if (!isInteractive || root.panel == null) return;
                if (loadButton != null && loadButton.enabledInHierarchy)
                {
                    loadButton.Focus();
                    return;
                }
                var firstButton = root.Q<Button>();
                if (firstButton != null && firstButton.enabledInHierarchy) firstButton.Focus();
            });
        }

        public void SetBindingGroupChoices(IReadOnlyList<string> groups, string selectedGroup) =>
            bindingGroupView?.SetChoices(groups, selectedGroup);

        public void SetIconResolver(InputBindingIconResolver resolver)
        {
            iconResolver = resolver;
            catalogView?.SetIconResolver(resolver);
        }

        public void SelectAdjacentSection(int direction) => navigation?.SelectAdjacentSection(direction, isInteractive);

        public void RenderBindingEntries(IReadOnlyList<InputBindingService.BindingEntry> entries, Action<int> rebind,
            Action<int> addModifier, Action<int> removeModifier, Action<int> reset, bool resetScroll = false) =>
            catalogView?.Render(entries, rebind, addModifier, removeModifier, reset, resetScroll);

        public void SetNavigationSubmitBlocked(bool blocked) => navigation?.SetSubmitBlocked(blocked);
        public void UpdateInputStates(InputActionAsset asset) => catalogView?.UpdateInputStates(asset);
        public void HandleNavigationMove(UiNavigationDirection direction) => navigation?.HandleMove(direction, isInteractive);
        public void FocusBindingEntry(int entryIndex, int preferredColumn = KeyConfigBindingNavigation.ChangeColumn) =>
            navigation?.FocusBindingEntry(entryIndex, preferredColumn);

        private void OnNavigationCancel(NavigationCancelEvent evt) => navigation?.HandleCancel(evt, isInteractive, onClose);
        private void OnNavigationSubmit(NavigationSubmitEvent evt) => navigation?.HandleSubmit(evt);
        private void OnRootFocusIn(FocusInEvent evt) => navigation?.HandleFocusIn(evt);

        private void RefreshLocalizedUi()
        {
            bindingGroupView?.RefreshLocalization();
            catalogView?.RefreshLocalization();
        }

        private void SubscribeLocalization()
        {
            if (localeSubscribed) return;
            KeyConfigLocalization.LocaleChanged += RefreshLocalizedUi;
            localeSubscribed = true;
        }
    }
}
