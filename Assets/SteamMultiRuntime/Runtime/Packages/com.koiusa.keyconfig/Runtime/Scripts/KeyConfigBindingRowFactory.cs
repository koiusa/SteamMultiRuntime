using System;
using System.Collections.Generic;
using Koiusa.Input.Icons;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig
{
    internal static class KeyConfigBindingRowFactory
    {
        internal sealed class BuildContext
        {
            public bool IsInteractive;
            public KeyConfigIconSet IconResolver;
            public HashSet<Button> UnavailableButtons;
            public Action<TextElement, string> BindText;
            public Action<VisualElement, string> BindTooltip;
            public Action<int> Rebind;
            public Action<int> AddModifier;
            public Action<int> RemoveModifier;
            public Action<int> Reset;
        }

        internal sealed class RowRequest
        {
            public KeyConfigBinding Entry;
            public int EntryIndex;
            public int VisibleRowIndex;
            public bool ShowActionName;
        }

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

        public static Result Create(RowRequest request, BuildContext context)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var entry = request.Entry ?? throw new ArgumentNullException(nameof(request.Entry));
            var row = new VisualElement { focusable = !entry.IsRebindable };
            row.AddToClassList("keyconfig-row");
            row.AddToClassList(request.VisibleRowIndex % 2 == 0 ? "even" : "odd");
            row.RegisterCallback<FocusInEvent>(OnFocusIn);
            row.RegisterCallback<FocusOutEvent>(OnFocusOut);

            var actionText = request.ShowActionName && !entry.IsPartOfComposite
                ? entry.ActionName
                : (entry.IsPartOfComposite ? entry.DisplayName.Split('/')[0] : string.Empty);
            var actionCell = new Label(actionText);
            if (!string.IsNullOrEmpty(actionText)) context.BindText(actionCell, actionText);
            actionCell.AddToClassList("keyconfig-cell-action");
            if (entry.IsPartOfComposite) actionCell.AddToClassList("composite-child");
            row.Add(actionCell);

            var bindingCell = new VisualElement();
            bindingCell.AddToClassList("keyconfig-cell-binding");
            if (context.IconResolver != null)
            {
                var visibleIconCount = 0;
                for (var pathIndex = 0; pathIndex < entry.BindingPaths.Count; pathIndex++)
                {
                    var icon = context.IconResolver.Resolve(entry.BindingPaths[pathIndex]);
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
                context.IsInteractive, context.UnavailableButtons, context.BindText, context.BindTooltip,
                () => context.AddModifier?.Invoke(request.EntryIndex));
            var removeModifierButton = CreateButton(
                "keyconfig.remove_modifier", "keyconfig.remove_modifier_tooltip", "keyconfig-modifier-button",
                CanChangeModifier(entry, false, hasConnectedDevice),
                context.IsInteractive, context.UnavailableButtons, context.BindText, context.BindTooltip,
                () => context.RemoveModifier?.Invoke(request.EntryIndex));
            var changeButton = CreateButton(
                "keyconfig.change", null, "keyconfig-rebind-button",
                entry.IsRebindable, context.IsInteractive, context.UnavailableButtons, context.BindText, context.BindTooltip,
                () => context.Rebind?.Invoke(request.EntryIndex));
            var resetButton = CreateButton(
                "keyconfig.reset", null, "keyconfig-reset-button",
                entry.IsRebindable, context.IsInteractive, context.UnavailableButtons, context.BindText, context.BindTooltip,
                () => context.Reset?.Invoke(request.EntryIndex));
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
            KeyConfigBinding entry,
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
