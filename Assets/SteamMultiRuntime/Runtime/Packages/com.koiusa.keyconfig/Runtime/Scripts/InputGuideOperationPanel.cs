using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
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

        public void Build(InputActionMap map)
        {
            keyboardList?.Clear();
            gamepadList?.Clear();
            if (map == null || keyboardList == null || gamepadList == null)
            {
                return;
            }

            BuildSections(map, keyboardList, false);
            BuildSections(map, gamepadList, true);
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

        private void BuildSections(InputActionMap map, VisualElement target, bool gamepad)
        {
            for (var sectionIndex = 0; sectionIndex < 4; sectionIndex++)
            {
                var section = new VisualElement();
                section.AddToClassList("input-operation-section");
                var sectionTitle = new Label(GetSectionTitle(sectionIndex));
                sectionTitle.AddToClassList("input-operation-section-title");
                section.Add(sectionTitle);
                var rowCount = 0;

                foreach (var action in map.actions)
                {
                    if (GetSection(action.name) != sectionIndex)
                    {
                        continue;
                    }

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
                if (binding.isComposite || !isInBindingGroup(binding.groups))
                {
                    continue;
                }

                var path = binding.overridePath ?? binding.path;
                if (gamepad ? !IsGamepadBinding(path) : !IsKeyboardMouseBinding(path))
                {
                    continue;
                }

                var displayName = action.GetBindingDisplayString(i);
                if (!string.IsNullOrWhiteSpace(displayName) && !result.Contains(displayName))
                {
                    result.Add(displayName);
                }
            }

            return result;
        }

        private static int GetSection(string actionName)
        {
            return actionName switch
            {
                "Move" or "Jump" or "Sprint" or "Crouch" or "Dash" or "Strafe" => 0,
                "Attack" or "Guard" or "Heal" or "LockOn" or "Previous" or "Next" => 1,
                "Grapple" or "GrappleFire" or "Reel" => 2,
                _ => 3
            };
        }

        private static string GetSectionTitle(int sectionIndex)
        {
            return sectionIndex switch
            {
                0 => KeyConfigLocalization.Get("keyconfig.section_movement"),
                1 => KeyConfigLocalization.Get("keyconfig.section_combat"),
                2 => KeyConfigLocalization.Get("keyconfig.section_grapple"),
                _ => KeyConfigLocalization.Get("keyconfig.section_camera")
            };
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
