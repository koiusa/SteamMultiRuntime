using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig
{
    internal sealed class KeyConfigView
    {
        private readonly UIDocument uiDocument;
        private readonly VisualTreeAsset layoutAsset;
        private readonly StyleSheet styleSheet;

        private Label statusLabel;
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

        public KeyConfigView(UIDocument uiDocument, VisualTreeAsset layoutAsset, StyleSheet styleSheet)
        {
            this.uiDocument = uiDocument;
            this.layoutAsset = layoutAsset;
            this.styleSheet = styleSheet;
        }

        public bool IsRenderable => statusLabel != null && bindingListView != null;

        public void Build()
        {
            var root = uiDocument.rootVisualElement;
            root.Clear();

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }

            if (layoutAsset != null)
            {
                layoutAsset.CloneTree(root);
                statusLabel = root.Q<Label>("status-label");
                bindingListView = root.Q<ScrollView>("binding-list-view");
                bindingGroupDropdown = new DropdownField("BindingGroup");
                bindingGroupDropdown.AddToClassList("keyconfig-binding-group-dropdown");
                mapTabBar = new VisualElement();
                mapTabBar.AddToClassList("keyconfig-map-tabs");

                var listParent = bindingListView.parent;
                if (listParent != null)
                {
                    var listIndex = listParent.IndexOf(bindingListView);
                    listParent.Insert(listIndex, bindingGroupDropdown);
                    listParent.Insert(listIndex + 1, mapTabBar);
                }

                loadButton = root.Q<Button>("load-button");
                saveButton = root.Q<Button>("save-button");
                resetAllButton = root.Q<Button>("reset-all-button");
                closeButton = root.Q<Button>("close-button");
                return;
            }

            BuildFallbackUi(root);
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

            onLoad = null;
            onSave = null;
            onResetAll = null;
            onClose = null;
            onBindingGroupChanged = null;
        }

        public void SetStatus(string status)
        {
            if (statusLabel != null)
            {
                statusLabel.text = string.IsNullOrWhiteSpace(status) ? string.Empty : status;
            }
        }

        public void SetInteractive(bool enabled)
        {
            loadButton?.SetEnabled(enabled);
            saveButton?.SetEnabled(enabled);
            resetAllButton?.SetEnabled(enabled);
            bindingGroupDropdown?.SetEnabled(enabled);
        }

        public void SetBindingGroupChoices(IReadOnlyList<string> groups, string selectedGroup)
        {
            if (bindingGroupDropdown == null)
            {
                return;
            }

            var choices = new List<string> { "すべて" };
            if (groups != null)
            {
                choices.AddRange(groups);
            }

            bindingGroupDropdown.choices = choices;
            var value = string.IsNullOrWhiteSpace(selectedGroup) ? "すべて" : selectedGroup;
            if (!choices.Contains(value))
            {
                value = "すべて";
            }

            bindingGroupDropdown.SetValueWithoutNotify(value);
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

            bindingListView.Clear();
            mapTabBar?.Clear();

            if (entries == null || entries.Count == 0)
            {
                selectedMapName = null;
                var emptyLabel = new Label("対象バインドがありません。");
                emptyLabel.AddToClassList("keyconfig-binding");
                bindingListView.Add(emptyLabel);
                return;
            }

            var mapNames = new List<string>();
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
                    })
                    {
                        text = mapName
                    };

                    tabButton.AddToClassList("keyconfig-map-tab-button");
                    tabButton.EnableInClassList("active", string.Equals(mapName, selectedMapName, StringComparison.Ordinal));
                    mapTabBar.Add(tabButton);
                }
            }

            string currentSchemeName = null;
            string currentProfileName = null;
            string currentActionName = null;

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

                    var schemeHeader = new Label($"Scheme: {currentSchemeName}");
                    schemeHeader.AddToClassList("keyconfig-scheme-group-header");
                    bindingListView.Add(schemeHeader);
                }

                if (!string.Equals(currentProfileName, entry.ProfileName, StringComparison.Ordinal))
                {
                    currentProfileName = entry.ProfileName;
                    currentActionName = null;

                    var profileHeader = new Label($"Profile: {currentProfileName}");
                    profileHeader.AddToClassList("keyconfig-profile-group-header");
                    bindingListView.Add(profileHeader);
                }

                if (!string.Equals(currentActionName, entry.ActionName, StringComparison.Ordinal))
                {
                    currentActionName = entry.ActionName;
                    var actionHeader = new Label(entry.ActionName);
                    actionHeader.AddToClassList("keyconfig-action-group-header");
                    bindingListView.Add(actionHeader);
                }

                var row = new VisualElement();
                row.AddToClassList("keyconfig-row");

                var bindingLabel = new Label(entry.DisplayName);
                bindingLabel.AddToClassList("keyconfig-binding");
                row.Add(bindingLabel);

                var rebindButton = new Button(() => onRebind?.Invoke(rowIndex)) { text = "変更" };
                rebindButton.AddToClassList("keyconfig-rebind-button");
                rebindButton.SetEnabled(!entry.IsComposite);
                row.Add(rebindButton);

                var resetButton = new Button(() => onReset?.Invoke(rowIndex)) { text = "戻す" };
                resetButton.AddToClassList("keyconfig-reset-button");
                resetButton.SetEnabled(!entry.IsComposite);
                row.Add(resetButton);

                bindingListView.Add(row);
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

        private void OnBindingGroupDropdownValueChanged(ChangeEvent<string> evt)
        {
            if (onBindingGroupChanged == null)
            {
                return;
            }

            var group = string.Equals(evt.newValue, "すべて", StringComparison.Ordinal)
                ? string.Empty
                : evt.newValue;
            onBindingGroupChanged.Invoke(group);
        }
    }
}
