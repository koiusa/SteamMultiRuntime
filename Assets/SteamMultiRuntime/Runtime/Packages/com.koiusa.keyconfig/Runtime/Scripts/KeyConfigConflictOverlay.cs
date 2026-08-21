using System;
using System.Collections.Generic;
using Koiusa.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig
{
    internal sealed class KeyConfigConflictOverlay
    {
        private const float InputGuardSeconds = 0.2f;

        private readonly List<Button> buttons = new List<Button>();
        private VisualElement overlay;
        private Action onCancel;
        private float inputUnlockTime;

        public bool IsVisible => overlay != null;

        public void Show(
            VisualElement root,
            string targetAction,
            string existingAction,
            Action replaceExisting,
            Action keepBoth,
            Action cancel)
        {
            Hide();
            if (root == null) return;

            overlay = new VisualElement();
            overlay.AddToClassList("keyconfig-conflict-overlay");
            var panel = new VisualElement();
            panel.AddToClassList("keyconfig-conflict-panel");
            var message = new Label(KeyConfigLocalization.Get("keyconfig.conflict_message", targetAction, existingAction));
            message.AddToClassList("keyconfig-conflict-message");
            panel.Add(message);

            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("keyconfig-conflict-buttons");
            AddButton(buttonRow, "keyconfig.conflict_replace", replaceExisting);
            AddButton(buttonRow, "keyconfig.conflict_keep", keepBoth);
            AddButton(buttonRow, "keyconfig.conflict_cancel", cancel);
            panel.Add(buttonRow);
            overlay.Add(panel);
            root.Add(overlay);
            onCancel = cancel;
            inputUnlockTime = Time.unscaledTime + InputGuardSeconds;
            overlay.schedule.Execute(() => buttons[buttons.Count - 1].Focus());
        }

        public void Hide()
        {
            overlay?.RemoveFromHierarchy();
            overlay = null;
            buttons.Clear();
            onCancel = null;
            inputUnlockTime = 0f;
        }

        public bool HandleCancel(VisualElement root, NavigationCancelEvent evt)
        {
            if (!IsVisible) return false;

            if (IsInputGuarded)
            {
                Consume(root, evt);
                return true;
            }

            var cancel = onCancel;
            Hide();
            Consume(root, evt);
            cancel?.Invoke();
            return true;
        }

        public bool HandleSubmit(VisualElement root, NavigationSubmitEvent evt)
        {
            if (!IsVisible || !IsInputGuarded) return false;
            Consume(root, evt);
            return true;
        }

        public bool HandleMove(VisualElement root, UiNavigationDirection direction)
        {
            if (!IsVisible) return false;
            if (IsInputGuarded) return true;

            if (direction == UiNavigationDirection.Left || direction == UiNavigationDirection.Right)
            {
                var focused = root?.focusController?.focusedElement as Button;
                var index = buttons.IndexOf(focused);
                var delta = direction == UiNavigationDirection.Left ? -1 : 1;
                index = index < 0 ? buttons.Count - 1 : (index + delta + buttons.Count) % buttons.Count;
                buttons[index].Focus();
            }

            return true;
        }

        private bool IsInputGuarded => Time.unscaledTime < inputUnlockTime;

        private void AddButton(VisualElement parent, string localizationKey, Action action)
        {
            var button = new Button(() =>
            {
                Hide();
                action?.Invoke();
            })
            {
                text = KeyConfigLocalization.Get(localizationKey)
            };
            button.AddToClassList("keyconfig-button");
            parent.Add(button);
            buttons.Add(button);
        }

        private static void Consume(VisualElement root, EventBase evt)
        {
            root?.focusController?.IgnoreEvent(evt);
            evt.StopImmediatePropagation();
        }
    }
}
