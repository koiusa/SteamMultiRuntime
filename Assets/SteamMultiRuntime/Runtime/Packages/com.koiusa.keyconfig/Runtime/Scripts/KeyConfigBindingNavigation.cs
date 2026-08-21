using System.Collections.Generic;
using Koiusa.Input;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.Keyconfig.Runtime
{
    internal sealed class KeyConfigBindingNavigation
    {
        internal const int AddModifierColumn = 0;
        internal const int RemoveModifierColumn = 1;
        internal const int ChangeColumn = 2;
        internal const int ResetColumn = 3;

        private readonly List<Row> rows = new List<Row>();

        public int Count => rows.Count;

        public void Clear() => rows.Clear();

        public void Add(int entryIndex, VisualElement row, Button addModifier, Button removeModifier, Button change, Button reset)
        {
            rows.Add(new Row(entryIndex, row, addModifier, removeModifier, change, reset));
        }

        public bool TryHandleMove(VisualElement root, ScrollView scrollView, UiNavigationDirection direction)
        {
            if (!TryGetFocusedRow(root, out var rowIndex, out var column)) return false;

            if (direction == UiNavigationDirection.Left || direction == UiNavigationDirection.Right)
            {
                var delta = direction == UiNavigationDirection.Left ? -1 : 1;
                GetAdjacentButton(rows[rowIndex], column, delta)?.Focus();
                return true;
            }

            if (direction != UiNavigationDirection.Up && direction != UiNavigationDirection.Down) return false;
            var rowDelta = direction == UiNavigationDirection.Up ? -1 : 1;
            var targetIndex = Mathf.Clamp(rowIndex + rowDelta, 0, rows.Count - 1);
            GetAvailableButton(rows[targetIndex], column)?.Focus();
            ScrollTo(scrollView, targetIndex);
            return true;
        }

        public bool ContainsFocus(VisualElement root) => TryGetFocusedRow(root, out _, out _);

        public void FocusFirst(VisualElement root, ScrollView scrollView)
        {
            root?.schedule.Execute(() =>
            {
                if (rows.Count == 0) return;
                GetAvailableButton(rows[0], ChangeColumn)?.Focus();
                ScrollTo(scrollView, 0);
            });
        }

        public bool FocusEntry(VisualElement root, ScrollView scrollView, int entryIndex, int preferredColumn = ChangeColumn)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].EntryIndex != entryIndex) continue;
                var rowIndex = i;
                root?.schedule.Execute(() =>
                {
                    GetAvailableButton(rows[rowIndex], preferredColumn)?.Focus();
                    ScrollTo(scrollView, rowIndex);
                });
                return true;
            }

            return false;
        }

        private bool TryGetFocusedRow(VisualElement root, out int rowIndex, out int column)
        {
            var focused = root?.focusController?.focusedElement as VisualElement;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (focused == row.AddModifier) { rowIndex = i; column = 0; return true; }
                if (focused == row.RemoveModifier) { rowIndex = i; column = 1; return true; }
                if (focused == row.Change) { rowIndex = i; column = 2; return true; }
                if (focused == row.Reset) { rowIndex = i; column = 3; return true; }
                if (focused == row.Element) { rowIndex = i; column = 0; return true; }
            }

            rowIndex = -1;
            column = 0;
            return false;
        }

        private static VisualElement GetAvailableButton(Row row, int preferredColumn)
        {
            var buttons = new[] { row.AddModifier, row.RemoveModifier, row.Change, row.Reset };
            preferredColumn = Mathf.Clamp(preferredColumn, 0, buttons.Length - 1);
            if (buttons[preferredColumn] != null && buttons[preferredColumn].enabledInHierarchy) return buttons[preferredColumn];
            for (var distance = 1; distance < buttons.Length; distance++)
            {
                var left = preferredColumn - distance;
                if (left >= 0 && buttons[left] != null && buttons[left].enabledInHierarchy) return buttons[left];
                var right = preferredColumn + distance;
                if (right < buttons.Length && buttons[right] != null && buttons[right].enabledInHierarchy) return buttons[right];
            }
            return row.Element != null && row.Element.focusable ? row.Element : null;
        }

        private static VisualElement GetAdjacentButton(Row row, int currentColumn, int direction)
        {
            var buttons = new[] { row.AddModifier, row.RemoveModifier, row.Change, row.Reset };
            var availability = new bool[buttons.Length];
            for (var i = 0; i < buttons.Length; i++)
                availability[i] = buttons[i] != null && buttons[i].enabledInHierarchy;
            var targetColumn = FindAdjacentColumn(availability, currentColumn, direction);
            return targetColumn >= 0 ? buttons[targetColumn] : null;
        }

        internal static int FindAdjacentColumn(IReadOnlyList<bool> availability, int currentColumn, int direction)
        {
            if (availability == null || availability.Count == 0 || direction == 0) return -1;
            var step = direction < 0 ? -1 : 1;
            for (var offset = 1; offset < availability.Count; offset++)
            {
                var index = (currentColumn + step * offset + availability.Count) % availability.Count;
                if (availability[index]) return index;
            }
            return currentColumn >= 0 && currentColumn < availability.Count && availability[currentColumn]
                ? currentColumn
                : -1;
        }

        private void ScrollTo(ScrollView scrollView, int rowIndex)
        {
            if (scrollView == null || rowIndex < 0 || rowIndex >= rows.Count) return;
            if (rowIndex == 0)
            {
                scrollView.scrollOffset = Vector2.zero;
                return;
            }
            scrollView.ScrollTo(rows[rowIndex].Element);
        }

        private sealed class Row
        {
            public Row(int entryIndex, VisualElement element, Button addModifier, Button removeModifier, Button change, Button reset)
            {
                EntryIndex = entryIndex;
                Element = element;
                AddModifier = addModifier;
                RemoveModifier = removeModifier;
                Change = change;
                Reset = reset;
            }

            public int EntryIndex { get; }
            public VisualElement Element { get; }
            public Button AddModifier { get; }
            public Button RemoveModifier { get; }
            public Button Change { get; }
            public Button Reset { get; }
        }
    }
}
