using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Koiusa.KeyConfig
{
    internal sealed class KeyConfigBindingGroupView
    {
        private readonly DropdownField dropdown;
        private IReadOnlyList<string> groups;
        private string selectedGroup;
        private Action<string> onChanged;

        public KeyConfigBindingGroupView(DropdownField dropdown) => this.dropdown = dropdown;
        public VisualElement Element => dropdown;

        public void Bind(Action<string> callback)
        {
            Unbind();
            onChanged = callback;
            dropdown?.RegisterValueChangedCallback(OnValueChanged);
        }

        public void Unbind()
        {
            dropdown?.UnregisterValueChangedCallback(OnValueChanged);
            onChanged = null;
        }

        public void SetChoices(IReadOnlyList<string> values, string selected)
        {
            groups = values;
            selectedGroup = selected;
            RefreshLocalization();
        }

        public void SetInteractive(bool enabled) => dropdown?.SetEnabled(enabled);

        public bool ContainsFocus(VisualElement focused) =>
            dropdown != null && focused != null && (focused == dropdown || dropdown.Contains(focused));

        public void Focus() => dropdown?.Focus();

        public void SelectAdjacentChoice(int direction)
        {
            if (dropdown == null || dropdown.choices.Count == 0 || direction == 0) return;
            var currentIndex = Mathf.Max(0, dropdown.index);
            dropdown.index = (currentIndex + Math.Sign(direction) + dropdown.choices.Count) % dropdown.choices.Count;
        }

        public void RefreshLocalization()
        {
            if (dropdown == null) return;
            dropdown.label = KeyConfigLocalization.Get("keyconfig.binding_group");
            var allLabel = KeyConfigLocalization.Get("keyconfig.all");
            var choices = new List<string> { allLabel };
            if (groups != null) choices.AddRange(groups);
            dropdown.choices = choices;
            var value = string.IsNullOrWhiteSpace(selectedGroup) ? allLabel : selectedGroup;
            dropdown.SetValueWithoutNotify(choices.Contains(value) ? value : allLabel);
        }

        private void OnValueChanged(ChangeEvent<string> evt)
        {
            if (onChanged == null) return;
            selectedGroup = string.Equals(evt.newValue, KeyConfigLocalization.Get("keyconfig.all"), StringComparison.Ordinal)
                ? string.Empty
                : evt.newValue;
            onChanged.Invoke(selectedGroup);
        }
    }
}
