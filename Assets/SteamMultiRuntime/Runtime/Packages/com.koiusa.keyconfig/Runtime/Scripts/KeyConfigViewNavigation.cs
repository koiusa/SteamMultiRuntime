using System;
using Koiusa.Input;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig
{
    internal sealed class KeyConfigViewNavigation
    {
        private const float ScrollStep = 72f;

        private readonly VisualElement root;
        private readonly KeyConfigBindingGroupView bindingGroup;
        private readonly KeyConfigBindingCatalogView catalog;
        private readonly KeyConfigConflictOverlay conflictOverlay;
        private readonly Button[] functionButtons;
        private bool submitBlocked;

        public KeyConfigViewNavigation(
            VisualElement root,
            KeyConfigBindingGroupView bindingGroup,
            KeyConfigBindingCatalogView catalog,
            KeyConfigConflictOverlay conflictOverlay,
            params Button[] functionButtons)
        {
            this.root = root;
            this.bindingGroup = bindingGroup;
            this.catalog = catalog;
            this.conflictOverlay = conflictOverlay;
            this.functionButtons = functionButtons ?? Array.Empty<Button>();
        }

        public void SetSubmitBlocked(bool blocked) => submitBlocked = blocked;

        public void SelectAdjacentSection(int direction, bool interactive)
        {
            if (!interactive || catalog.MapCount == 0 || direction == 0) return;
            var focused = root?.focusController?.focusedElement as VisualElement;
            var currentSection = bindingGroup.ContainsFocus(focused) ? 0 : catalog.SelectedMapIndex + 1;
            var sectionCount = catalog.MapCount + 1;
            var nextSection = (currentSection + Math.Sign(direction) + sectionCount) % sectionCount;
            if (nextSection == 0)
            {
                bindingGroup.Focus();
                return;
            }
            catalog.SelectMap(nextSection - 1, true);
        }

        public void HandleMove(UiNavigationDirection direction, bool interactive)
        {
            if (conflictOverlay.HandleMove(root, direction) || !interactive) return;
            if (catalog.TryHandleBindingMove(direction)) return;

            if (direction == UiNavigationDirection.Left || direction == UiNavigationDirection.Right)
            {
                FocusAdjacentFunctionButton(direction == UiNavigationDirection.Left ? -1 : 1);
                return;
            }

            var focused = root?.focusController?.focusedElement as VisualElement;
            if (bindingGroup.ContainsFocus(focused))
            {
                if (direction == UiNavigationDirection.Up || direction == UiNavigationDirection.Down)
                    bindingGroup.SelectAdjacentChoice(direction == UiNavigationDirection.Up ? -1 : 1);
                return;
            }

            if (direction == UiNavigationDirection.Up || direction == UiNavigationDirection.Down)
                catalog.Scroll(direction == UiNavigationDirection.Up ? -1 : 1, ScrollStep);
        }

        public void HandleCancel(NavigationCancelEvent evt, bool interactive, Action onClose)
        {
            if (conflictOverlay.HandleCancel(root, evt) || !interactive || onClose == null) return;
            if (catalog.ContainsBindingFocus())
            {
                catalog.FocusSelectedMapTab();
                Consume(evt);
                return;
            }
            evt.StopImmediatePropagation();
            onClose.Invoke();
        }

        public void HandleSubmit(NavigationSubmitEvent evt)
        {
            if (submitBlocked)
            {
                Consume(evt);
                return;
            }
            conflictOverlay.HandleSubmit(root, evt);
        }

        public void HandleFocusIn(FocusInEvent evt) => catalog.UpdateTabHighlights(evt.target as VisualElement);
        public void FocusBindingEntry(int entryIndex, int preferredColumn) => catalog.FocusEntry(entryIndex, preferredColumn);

        private void FocusAdjacentFunctionButton(int direction)
        {
            var focused = root?.focusController?.focusedElement as VisualElement;
            var currentIndex = Array.IndexOf(functionButtons, focused);
            for (var offset = 1; offset <= functionButtons.Length; offset++)
            {
                var index = currentIndex < 0
                    ? (direction > 0 ? offset - 1 : functionButtons.Length - offset)
                    : (currentIndex + direction * offset + functionButtons.Length) % functionButtons.Length;
                var button = functionButtons[index];
                if (button != null && button.enabledInHierarchy)
                {
                    button.Focus();
                    return;
                }
            }
        }

        private void Consume(EventBase evt)
        {
            root?.focusController?.IgnoreEvent(evt);
            evt.StopImmediatePropagation();
        }
    }
}
