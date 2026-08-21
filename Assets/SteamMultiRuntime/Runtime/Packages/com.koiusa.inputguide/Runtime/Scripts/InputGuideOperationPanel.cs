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
        private readonly Func<string, bool> isInBindingGroup;

        public InputGuideOperationPanel(
            VisualElement keyboardList,
            VisualElement gamepadList,
            Func<string, bool> isInBindingGroup)
        {
            this.keyboardList = keyboardList;
            this.gamepadList = gamepadList;
            this.isInBindingGroup = isInBindingGroup ?? throw new ArgumentNullException(nameof(isInBindingGroup));
        }

        public void Build(IReadOnlyList<InputActionMap> maps)
        {
            keyboardList?.Clear();
            gamepadList?.Clear();
            if (maps == null || keyboardList == null || gamepadList == null)
            {
                return;
            }

            BuildSections(maps, keyboardList, false);
            BuildSections(maps, gamepadList, true);
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

        private void BuildSections(IReadOnlyList<InputActionMap> maps, VisualElement target, bool gamepad)
        {
            for (var mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                var map = maps[mapIndex];
                if (map == null)
                {
                    continue;
                }

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

                if (rowCount > 0)
                {
                    target.Add(section);
                }
            }
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
