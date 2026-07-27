using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Koiusa.SteamMultiRuntime.Network;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class CharacterSelectView
    {
        private const string CommonScrollStyleSheetPath = "UI/Common/SteamMultiRuntimeScrollView";
        private static readonly Color NormalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color SelectedColor = new Color(0.18f, 0.48f, 0.78f, 1f);

        private readonly UIDocument uiDocument;
        private readonly VisualTreeAsset layoutAsset;
        private readonly StyleSheet styleSheet;
        private readonly List<Button> characterButtons = new List<Button>();

        private ScrollView listScroll;
        private Label selectedNameLabel;
        private int selectedIndex = -1;

        private Action<int> onSelect;
        private Action onConfirm;

        public CharacterSelectView(UIDocument uiDocument, VisualTreeAsset layoutAsset, StyleSheet styleSheet)
        {
            this.uiDocument = uiDocument;
            this.layoutAsset = layoutAsset;
            this.styleSheet = styleSheet;
        }

        public void Build(CharacterModelIdList modelIdList)
        {
            var root = uiDocument.rootVisualElement;
            root.Clear();
            characterButtons.Clear();
            selectedIndex = -1;

            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
                root.styleSheets.Add(styleSheet);
            var commonScrollStyle = Resources.Load<StyleSheet>(CommonScrollStyleSheetPath);
            if (commonScrollStyle != null && !root.styleSheets.Contains(commonScrollStyle))
                root.styleSheets.Add(commonScrollStyle);

            if (layoutAsset != null)
            {
                layoutAsset.CloneTree(root);
                listScroll = root.Q<ScrollView>("character-list-view");
                selectedNameLabel = root.Q<Label>("selected-name-label");
            }
            else
            {
                BuildFallbackUi(root);
            }

            PopulateCharacterList(modelIdList != null ? modelIdList.modelIds : null);

            if (selectedNameLabel != null)
                selectedNameLabel.text = "選択中: なし";

        }

        public void BindActions(Action<int> onSelectCallback, Action onConfirmCallback)
        {
            onSelect = onSelectCallback;
            onConfirm = onConfirmCallback;
        }

        public void UnbindActions()
        {
            onSelect = null;
            onConfirm = null;
        }

        public void SetSelectedIndex(int index, string[] modelIds)
        {
            if (selectedIndex >= 0 && selectedIndex < characterButtons.Count)
                characterButtons[selectedIndex].style.backgroundColor = NormalColor;

            selectedIndex = index;

            if (selectedIndex >= 0 && selectedIndex < characterButtons.Count)
                characterButtons[selectedIndex].style.backgroundColor = SelectedColor;

            var displayName = (modelIds != null && index >= 0 && index < modelIds.Length)
                ? modelIds[index]
                : "なし";

            if (selectedNameLabel != null)
                selectedNameLabel.text = $"選択中: {displayName}";

        }

        public void FocusSelectedCharacter()
        {
            if (characterButtons.Count == 0)
                return;

            var index = selectedIndex >= 0 && selectedIndex < characterButtons.Count ? selectedIndex : 0;
            var button = characterButtons[index];
            button.schedule.Execute(() =>
            {
                button.Focus();
                listScroll?.ScrollTo(button);
            });
        }

        private void PopulateCharacterList(string[] modelIds)
        {
            if (listScroll == null || modelIds == null)
                return;

            for (var i = 0; i < modelIds.Length; i++)
            {
                var index = i;
                var btn = new Button(() => OnCharacterButtonClicked(index));
                btn.text = modelIds[i];
                btn.AddToClassList("character-select-option");
                btn.style.backgroundColor = NormalColor;
                btn.RegisterCallback<FocusInEvent>(_ => OnCharacterButtonFocused(index));
                characterButtons.Add(btn);
                listScroll.Add(btn);
            }
        }

        private void BuildFallbackUi(VisualElement root)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.alignItems = Align.Center;
            container.style.justifyContent = Justify.Center;
            container.style.width = new StyleLength(new Length(100f, LengthUnit.Percent));
            container.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
            root.Add(container);

            var title = new Label("キャラクター選択");
            title.style.fontSize = 32;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 24;
            container.Add(title);

            listScroll = new ScrollView(ScrollViewMode.Vertical);
            listScroll.style.width = new StyleLength(new Length(50f, LengthUnit.Percent));
            listScroll.style.minWidth = 240;
            listScroll.style.maxHeight = 320;
            listScroll.style.marginBottom = 16;
            container.Add(listScroll);

            selectedNameLabel = new Label("選択中: なし");
            selectedNameLabel.style.marginBottom = 16;
            container.Add(selectedNameLabel);

        }

        private void OnCharacterButtonClicked(int index)
        {
            onSelect?.Invoke(index);
            onConfirm?.Invoke();
        }

        private void OnCharacterButtonFocused(int index)
        {
            // Directional navigation should preview the focused character just as
            // pointing at an option and clicking it does with a mouse.
            onSelect?.Invoke(index);
        }

    }
}
