using System;
using System.Collections.Generic;
using Koiusa.KeyConfig;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.InputGuide
{
    internal sealed class InputGuideOperationPanel
    {
        private readonly VisualElement keyboardList;
        private readonly VisualElement gamepadList;
        private readonly VisualElement mapTabs;
        private readonly Func<string, bool> isInBindingGroup;
        private readonly List<MapView> mapViews = new List<MapView>();
        private int selectedMapIndex;

        private sealed class MapView
        {
            public Button Tab;
            public VisualElement KeyboardSection;
            public VisualElement GamepadSection;
        }

        public InputGuideOperationPanel(
            VisualElement keyboardList,
            VisualElement gamepadList,
            VisualElement mapTabs,
            Func<string, bool> isInBindingGroup)
        {
            this.keyboardList = keyboardList;
            this.gamepadList = gamepadList;
            this.mapTabs = mapTabs;
            this.isInBindingGroup = isInBindingGroup ?? throw new ArgumentNullException(nameof(isInBindingGroup));
        }

        public void Build(IReadOnlyList<InputActionMap> maps)
        {
            keyboardList?.Clear();
            gamepadList?.Clear();
            mapTabs?.Clear();
            mapViews.Clear();
            selectedMapIndex = 0;
            if (maps == null || keyboardList == null || gamepadList == null)
            {
                return;
            }

            for (var mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                var map = maps[mapIndex];
                if (map == null)
                {
                    continue;
                }

                var keyboardSection = BuildSection(map, false);
                var gamepadSection = BuildSection(map, true);
                if (keyboardSection == null && gamepadSection == null)
                {
                    continue;
                }

                if (keyboardSection != null) keyboardList.Add(keyboardSection);
                if (gamepadSection != null) gamepadList.Add(gamepadSection);

                var tabIndex = mapViews.Count;
                var tab = new Button(() => SelectMap(tabIndex))
                {
                    text = KeyConfigLocalization.Get(map.name)
                };
                tab.AddToClassList("input-operation-map-tab");
                mapTabs?.Add(tab);
                mapViews.Add(new MapView
                {
                    Tab = tab,
                    KeyboardSection = keyboardSection,
                    GamepadSection = gamepadSection
                });
            }

            SelectMap(0);
        }

        public void SetGamepadVisible(bool showGamepad)
        {
            if (keyboardList != null)
            {
                keyboardList.style.display = showGamepad ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (gamepadList != null)
            {
                gamepadList.style.display = showGamepad ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        internal void SelectMap(int selectedIndex)
        {
            if (mapViews.Count == 0)
            {
                selectedMapIndex = 0;
                return;
            }

            selectedMapIndex = (selectedIndex % mapViews.Count + mapViews.Count) % mapViews.Count;
            for (var i = 0; i < mapViews.Count; i++)
            {
                var selected = i == selectedMapIndex;
                var view = mapViews[i];
                view.Tab.EnableInClassList("input-operation-map-tab--selected", selected);
                view.KeyboardSection?.EnableInClassList("input-operation-section--selected", selected);
                view.GamepadSection?.EnableInClassList("input-operation-section--selected", selected);
            }
        }

        public void SelectPreviousMap() => SelectMap(selectedMapIndex - 1);

        public void SelectNextMap() => SelectMap(selectedMapIndex + 1);

        private VisualElement BuildSection(InputActionMap map, bool gamepad)
        {
            var section = new VisualElement();
            section.AddToClassList("input-operation-section");
            var sectionTitle = new Label(KeyConfigLocalization.Get(map.name));
            sectionTitle.AddToClassList("input-operation-section-title");
            section.Add(sectionTitle);
            var rowCount = 0;

            foreach (var action in map.actions)
            {
                var bindings = GetBindings(action, gamepad);
                if (bindings.Count == 0)
                {
                    continue;
                }

                AddRow(section, action.name, bindings);
                rowCount++;
            }

            return rowCount > 0 ? section : null;
        }

        private List<string> GetBindings(InputAction action, bool gamepad)
        {
            var result = new List<string>();
            for (var i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isPartOfComposite || !isInBindingGroup(binding.groups))
                {
                    continue;
                }

                var path = binding.overridePath ?? binding.path;
                var matchesDevice = binding.isComposite
                    ? CompositeMatchesDevice(action, i, gamepad)
                    : gamepad ? IsGamepadBinding(path) : IsKeyboardMouseBinding(path);
                if (!matchesDevice)
                {
                    continue;
                }

                var displayName = CompositeBindingUtility.GetDisplayString(action, i);
                if (!string.IsNullOrWhiteSpace(displayName) && !result.Contains(displayName))
                {
                    result.Add(displayName);
                }
            }

            return result;
        }

        private static bool CompositeMatchesDevice(InputAction action, int rootIndex, bool gamepad)
        {
            var parts = CompositeBindingUtility.GetPartIndices(action, rootIndex);
            if (parts.Count == 0) return false;
            for (var i = 0; i < parts.Count; i++)
            {
                var binding = action.bindings[parts[i]];
                var path = binding.overridePath ?? binding.path;
                if (gamepad ? !IsGamepadBinding(path) : !IsKeyboardMouseBinding(path)) return false;
            }
            return true;
        }

        private static void AddRow(VisualElement target, string actionName, List<string> bindings)
        {
            var row = new VisualElement();
            row.AddToClassList("input-operation-row");
            var actionLabel = new Label(KeyConfigLocalization.Get(actionName));
            actionLabel.AddToClassList("input-operation-action");
            row.Add(actionLabel);
            var bindingLabel = new Label(string.Join(" / ", bindings));
            bindingLabel.AddToClassList("input-operation-binding");
            row.Add(bindingLabel);
            target.Add(row);
        }

        private static bool IsGamepadBinding(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && (path.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.IndexOf("Joystick", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsKeyboardMouseBinding(string path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && (path.IndexOf("Keyboard", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.IndexOf("Pointer", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
