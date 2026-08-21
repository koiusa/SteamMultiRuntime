using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    internal static class KeyConfigBindingRowFactory
    {
        internal sealed class Result
        {
            public VisualElement Row;
            public Button AddModifierButton;
            public Button RemoveModifierButton;
            public Button ChangeButton;
            public Button ResetButton;
            public Label InputStateLabel;
            public InputControl Control;
        }

        public static Result Create(
            InputBindingService.BindingEntry entry,
            int entryIndex,
            int visibleRowIndex,
            bool showActionName,
            bool isInteractive,
            InputBindingIconResolver iconResolver,
            HashSet<Button> unavailableButtons,
            Action<TextElement, string> bindText,
            Action<VisualElement, string> bindTooltip,
            Action<int> onRebind,
            Action<int> onAddModifier,
            Action<int> onRemoveModifier,
            Action<int> onReset)
        {
            var row = new VisualElement { focusable = !entry.IsRebindable };
            row.AddToClassList("keyconfig-row");
            row.AddToClassList(visibleRowIndex % 2 == 0 ? "even" : "odd");
            row.RegisterCallback<FocusInEvent>(OnFocusIn);
            row.RegisterCallback<FocusOutEvent>(OnFocusOut);

            var actionText = showActionName && !entry.IsPartOfComposite
                ? entry.ActionName
                : (entry.IsPartOfComposite ? entry.DisplayName.Split('/')[0] : string.Empty);
            var actionCell = new Label(actionText);
            if (!string.IsNullOrEmpty(actionText)) bindText(actionCell, actionText);
            actionCell.AddToClassList("keyconfig-cell-action");
            if (entry.IsPartOfComposite) actionCell.AddToClassList("composite-child");
            row.Add(actionCell);

            var bindingCell = new VisualElement();
            bindingCell.AddToClassList("keyconfig-cell-binding");
            if (iconResolver != null)
            {
                var visibleIconCount = 0;
                for (var pathIndex = 0; pathIndex < entry.BindingPaths.Count; pathIndex++)
                {
                    var icon = iconResolver.Resolve(entry.BindingPaths[pathIndex]);
                    if (icon == null) continue;
                    if (visibleIconCount > 0)
                    {
                        var separator = new Label("+");
                        separator.AddToClassList("keyconfig-binding-icon-separator");
                        bindingCell.Add(separator);
                    }
                    var iconElement = new Image { image = icon };
                    iconElement.AddToClassList("keyconfig-binding-icon");
                    bindingCell.Add(iconElement);
                    visibleIconCount++;
                }
            }

            var bindingLabel = new Label(entry.DisplayName);
            bindingLabel.AddToClassList("keyconfig-binding-label");
            bindingCell.Add(bindingLabel);
            var inputStateLabel = new Label("●");
            inputStateLabel.AddToClassList("keyconfig-input-state");
            inputStateLabel.style.display = DisplayStyle.None;
            bindingCell.Add(inputStateLabel);
            row.Add(bindingCell);

            var buttonCell = new VisualElement();
            buttonCell.AddToClassList("keyconfig-cell-buttons");
            var hasConnectedDevice = InputControlActivity.HasConnectedDevice(entry.BindingPath);
            var addModifierButton = CreateButton(
                "keyconfig.add_modifier", "keyconfig.add_modifier_tooltip", "keyconfig-modifier-button",
                CanChangeModifier(entry, true, hasConnectedDevice),
                isInteractive, unavailableButtons, bindText, bindTooltip,
                () => onAddModifier?.Invoke(entryIndex));
            var removeModifierButton = CreateButton(
                "keyconfig.remove_modifier", "keyconfig.remove_modifier_tooltip", "keyconfig-modifier-button",
                CanChangeModifier(entry, false, hasConnectedDevice),
                isInteractive, unavailableButtons, bindText, bindTooltip,
                () => onRemoveModifier?.Invoke(entryIndex));
            var changeButton = CreateButton(
                "keyconfig.change", null, "keyconfig-rebind-button",
                entry.IsRebindable, isInteractive, unavailableButtons, bindText, bindTooltip,
                () => onRebind?.Invoke(entryIndex));
            var resetButton = CreateButton(
                "keyconfig.reset", null, "keyconfig-reset-button",
                entry.IsRebindable, isInteractive, unavailableButtons, bindText, bindTooltip,
                () => onReset?.Invoke(entryIndex));
            buttonCell.Add(addModifierButton);
            buttonCell.Add(removeModifierButton);
            buttonCell.Add(changeButton);
            buttonCell.Add(resetButton);
            row.Add(buttonCell);

            return new Result
            {
                Row = row,
                AddModifierButton = addModifierButton,
                RemoveModifierButton = removeModifierButton,
                ChangeButton = changeButton,
                ResetButton = resetButton,
                InputStateLabel = inputStateLabel,
                Control = InputControlActivity.Resolve(entry.BindingPath)
            };
        }

        internal static bool CanChangeModifier(
            InputBindingService.BindingEntry entry,
            bool add,
            bool hasConnectedDevice)
        {
            if (!entry.IsRebindable || !hasConnectedDevice) return false;
            return add ? entry.ModifierCount < 2 : entry.ModifierCount > 0;
        }

        private static Button CreateButton(
            string textKey,
            string tooltipKey,
            string className,
            bool available,
            bool isInteractive,
            HashSet<Button> unavailableButtons,
            Action<TextElement, string> bindText,
            Action<VisualElement, string> bindTooltip,
            Action onClick)
        {
            var button = new Button(onClick);
            bindText(button, textKey);
            if (!string.IsNullOrEmpty(tooltipKey)) bindTooltip(button, tooltipKey);
            button.AddToClassList("keyconfig-row-button");
            button.AddToClassList(className);
            if (!available) unavailableButtons.Add(button);
            button.SetEnabled(isInteractive && available);
            return button;
        }

        private static void OnFocusIn(FocusInEvent evt)
        {
            if (evt.currentTarget is VisualElement row) row.AddToClassList("focused");
        }

        private static void OnFocusOut(FocusOutEvent evt)
        {
            if (!(evt.currentTarget is VisualElement row)) return;
            row.schedule.Execute(() =>
            {
                var focused = row.panel?.focusController?.focusedElement as VisualElement;
                row.EnableInClassList("focused", focused != null && row.Contains(focused));
            });
        }
    }
}
