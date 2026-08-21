using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Koiusa.KeyConfig
{
    internal static class CompositeBindingUtility
    {
        private const string ButtonWithOneModifier = "ButtonWithOneModifier";
        private const string ButtonWithTwoModifiers = "ButtonWithTwoModifiers";

        public static int GetRootIndex(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                return -1;
            }

            if (!action.bindings[bindingIndex].isPartOfComposite)
            {
                return bindingIndex;
            }

            for (var i = bindingIndex - 1; i >= 0; i--)
            {
                if (action.bindings[i].isComposite)
                {
                    return i;
                }
                if (!action.bindings[i].isPartOfComposite)
                {
                    break;
                }
            }
            return -1;
        }

        public static bool IsButtonWithOneModifier(InputAction action, int rootIndex)
        {
            if (action == null || rootIndex < 0 || rootIndex >= action.bindings.Count)
            {
                return false;
            }
            var binding = action.bindings[rootIndex];
            return binding.isComposite && binding.path.StartsWith(ButtonWithOneModifier, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsButtonWithTwoModifiers(InputAction action, int rootIndex)
        {
            if (action == null || rootIndex < 0 || rootIndex >= action.bindings.Count) return false;
            var binding = action.bindings[rootIndex];
            return binding.isComposite && binding.path.StartsWith(ButtonWithTwoModifiers, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSupportedModifierComposite(InputAction action, int rootIndex) =>
            IsButtonWithOneModifier(action, rootIndex) || IsButtonWithTwoModifiers(action, rootIndex);

        public static int GetModifierCount(InputAction action, int rootIndex)
        {
            if (IsButtonWithTwoModifiers(action, rootIndex)) return 2;
            return IsButtonWithOneModifier(action, rootIndex) ? 1 : 0;
        }

        public static List<int> GetPartIndices(InputAction action, int rootIndex)
        {
            var result = new List<int>();
            if (action == null || rootIndex < 0 || rootIndex >= action.bindings.Count || !action.bindings[rootIndex].isComposite)
            {
                return result;
            }
            for (var i = rootIndex + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; i++)
            {
                result.Add(i);
            }
            return result;
        }

        public static string GetDisplayString(InputAction action, int bindingIndex)
        {
            var rootIndex = GetRootIndex(action, bindingIndex);
            if (rootIndex < 0)
            {
                return string.Empty;
            }
            if (!action.bindings[rootIndex].isComposite)
            {
                return action.GetBindingDisplayString(rootIndex);
            }

            var parts = GetPartIndices(action, rootIndex);
            var values = new List<string>(parts.Count);
            for (var i = 0; i < parts.Count; i++)
            {
                var value = action.GetBindingDisplayString(parts[i]);
                if (!string.IsNullOrWhiteSpace(value)) values.Add(NormalizeModifierDisplay(value));
            }
            return string.Join("+", values);
        }

        public static string GetIdentity(InputAction action, int bindingIndex)
        {
            var rootIndex = GetRootIndex(action, bindingIndex);
            if (rootIndex < 0) return string.Empty;
            if (!action.bindings[rootIndex].isComposite)
            {
                return NormalizePath(action.bindings[rootIndex].effectivePath);
            }

            var parts = GetPartIndices(action, rootIndex);
            var values = new List<string>(parts.Count + 1) { action.bindings[rootIndex].path.Split('(')[0].ToLowerInvariant() };
            for (var i = 0; i < parts.Count; i++) values.Add(NormalizePath(action.bindings[parts[i]].effectivePath));
            return string.Join("|", values);
        }

        public static void Reset(InputAction action, int bindingIndex)
        {
            var rootIndex = GetRootIndex(action, bindingIndex);
            if (rootIndex < 0) return;
            action.RemoveBindingOverride(rootIndex);
            var parts = GetPartIndices(action, rootIndex);
            for (var i = 0; i < parts.Count; i++) action.RemoveBindingOverride(parts[i]);
        }

        public static void Disable(InputAction action, int bindingIndex)
        {
            var rootIndex = GetRootIndex(action, bindingIndex);
            if (rootIndex < 0) return;
            if (!action.bindings[rootIndex].isComposite)
            {
                action.ApplyBindingOverride(rootIndex, string.Empty);
                return;
            }
            var parts = GetPartIndices(action, rootIndex);
            for (var i = 0; i < parts.Count; i++) action.ApplyBindingOverride(parts[i], string.Empty);
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return path.ToLowerInvariant()
                .Replace("/leftctrl", "/ctrl").Replace("/rightctrl", "/ctrl")
                .Replace("/leftshift", "/shift").Replace("/rightshift", "/shift")
                .Replace("/leftalt", "/alt").Replace("/rightalt", "/alt");
        }

        private static string NormalizeModifierDisplay(string value)
        {
            return value.Replace("Left Control", "Ctrl").Replace("Right Control", "Ctrl")
                .Replace("Left Ctrl", "Ctrl").Replace("Right Ctrl", "Ctrl")
                .Replace("Left Shift", "Shift").Replace("Right Shift", "Shift")
                .Replace("Left Alt", "Alt").Replace("Right Alt", "Alt");
        }
    }
}
