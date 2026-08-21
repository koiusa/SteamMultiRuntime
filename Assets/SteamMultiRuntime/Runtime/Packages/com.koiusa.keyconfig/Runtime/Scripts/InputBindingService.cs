using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    public sealed class InputBindingService
    {
        public readonly struct BindingEntry
        {
            public BindingEntry(
                Guid actionId,
                string actionName,
                string actionMapName,
                string schemeName,
                string profileName,
                int bindingIndex,
                Guid bindingId,
                string displayName,
                bool isComposite,
                bool isPartOfComposite,
                bool isRebindable,
                string groups,
                string bindingPath,
                int modifierCount = 0)
                : this(
                    actionId, actionName, actionMapName, schemeName, profileName, bindingIndex, bindingId,
                    displayName, isComposite, isPartOfComposite, isRebindable, groups, bindingPath,
                    modifierCount, null)
            {
            }

            public BindingEntry(
                Guid actionId,
                string actionName,
                string actionMapName,
                string schemeName,
                string profileName,
                int bindingIndex,
                Guid bindingId,
                string displayName,
                bool isComposite,
                bool isPartOfComposite,
                bool isRebindable,
                string groups,
                string bindingPath,
                int modifierCount,
                IReadOnlyList<string> bindingPaths)
            {
                ActionId = actionId;
                ActionName = actionName;
                ActionMapName = actionMapName;
                SchemeName = schemeName;
                ProfileName = profileName;
                BindingIndex = bindingIndex;
                BindingId = bindingId;
                DisplayName = displayName;
                IsComposite = isComposite;
                IsPartOfComposite = isPartOfComposite;
                IsRebindable = isRebindable;
                Groups = groups;
                BindingPath = bindingPath;
                ModifierCount = modifierCount;
                BindingPaths = bindingPaths ??
                    (string.IsNullOrWhiteSpace(bindingPath) ? Array.Empty<string>() : new[] { bindingPath });
            }

            public Guid ActionId { get; }
            public string ActionName { get; }
            public string ActionMapName { get; }
            public string SchemeName { get; }
            public string ProfileName { get; }
            public int BindingIndex { get; }
            public Guid BindingId { get; }
            public string DisplayName { get; }
            public bool IsComposite { get; }
            public bool IsPartOfComposite { get; }
            public bool IsRebindable { get; }
            public string Groups { get; }
            public string BindingPath { get; }
            public int ModifierCount { get; }
            public IReadOnlyList<string> BindingPaths { get; }
        }

        private readonly InputActionAsset inputActionAsset;
        private readonly InputBindingOverridesRepository repository;
        private readonly HashSet<string> nonRebindableActionMaps;
        private readonly InputBindingStructureState structureState;

        public InputBindingService(InputActionAsset inputActionAsset, InputBindingOverridesRepository repository = null, IEnumerable<string> nonRebindableActionMaps = null)
        {
            this.inputActionAsset = inputActionAsset;
            this.repository = repository ?? new InputBindingOverridesRepository();
            this.nonRebindableActionMaps = new HashSet<string>(nonRebindableActionMaps ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            structureState = new InputBindingStructureState(inputActionAsset);
            RemoveProtectedOverrides();
        }

        public InputActionAsset InputActionAsset => inputActionAsset;

        public static bool TryCreate(InputActionAsset asset, out InputBindingService service)
        {
            service = null;
            if (asset == null)
            {
                return false;
            }

            service = new InputBindingService(asset);
            return true;
        }

        public bool TryLoadOverrides(string userId)
        {
            if (inputActionAsset == null)
            {
                return false;
            }

            if (!repository.TryLoad(userId, out var json))
            {
                return false;
            }

            var overridesJson = structureState.Restore(json);
            if (!string.IsNullOrWhiteSpace(overridesJson)) inputActionAsset.LoadBindingOverridesFromJson(overridesJson);
            RemoveProtectedOverrides();
            return true;
        }

        public void SaveOverrides(string userId)
        {
            if (inputActionAsset == null)
            {
                return;
            }

            var json = CaptureOverrides();
            repository.Save(userId, json);
        }

        public string CaptureOverrides()
        {
            return inputActionAsset != null
                ? structureState.Capture(inputActionAsset.SaveBindingOverridesAsJson())
                : string.Empty;
        }

        public void RestoreOverrides(string overridesJson)
        {
            if (inputActionAsset == null)
            {
                return;
            }

            var bindingOverridesJson = structureState.Restore(overridesJson);
            if (string.IsNullOrWhiteSpace(bindingOverridesJson))
            {
                inputActionAsset.RemoveAllBindingOverrides();
            }
            else
            {
                inputActionAsset.LoadBindingOverridesFromJson(bindingOverridesJson);
            }
            RemoveProtectedOverrides();
        }

        public void ResetAllOverrides(string userId = null)
        {
            if (inputActionAsset != null)
            {
                structureState.RestoreAllOriginal();
                inputActionAsset.RemoveAllBindingOverrides();
            }

            if (!string.IsNullOrWhiteSpace(userId))
            {
                repository.Delete(userId);
            }
        }

        public void ResetBinding(InputAction action, int bindingIndex)
        {
            if (action == null)
            {
                return;
            }

            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                return;
            }

            if (!structureState.RestoreOriginal(action, bindingIndex)) CompositeBindingUtility.Reset(action, bindingIndex);
        }

        public bool AddModifier(InputAction action, int bindingIndex)
        {
            if (!IsActionRebindable(action)) return false;
            return structureState.ChangeModifierCount(action, bindingIndex, 1);
        }

        public bool RemoveModifier(InputAction action, int bindingIndex)
        {
            if (!IsActionRebindable(action)) return false;
            return structureState.ChangeModifierCount(action, bindingIndex, -1);
        }

        public string GetBindingDisplayString(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                return string.Empty;
            }

            return CompositeBindingUtility.GetDisplayString(action, bindingIndex);
        }

        public List<BindingEntry> GetBindingEntries(string bindingGroup = null)
        {
            var entries = new List<BindingEntry>();
            if (inputActionAsset == null)
            {
                return entries;
            }

            foreach (var action in inputActionAsset)
            {
                for (var i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.isPartOfComposite)
                    {
                        continue;
                    }
                    if (!IsLogicalBindingInGroup(action, i, bindingGroup))
                    {
                        continue;
                    }

                    var displayName = CompositeBindingUtility.GetDisplayString(action, i);
                    var resolvedBindingPath = binding.overridePath != null ? binding.overridePath : binding.path;
                    IReadOnlyList<string> resolvedBindingPaths = null;
                    if (binding.isComposite)
                    {
                        var parts = CompositeBindingUtility.GetPartIndices(action, i);
                        if (parts.Count > 0)
                        {
                            var partPaths = new List<string>(parts.Count);
                            for (var partIndex = 0; partIndex < parts.Count; partIndex++)
                            {
                                var part = action.bindings[parts[partIndex]];
                                partPaths.Add(part.overridePath ?? part.path);
                            }
                            resolvedBindingPaths = partPaths;
                            var representative = action.bindings[parts[parts.Count - 1]];
                            resolvedBindingPath = representative.overridePath ?? representative.path;
                        }
                    }
                    entries.Add(new BindingEntry(
                        action.id,
                        action.name,
                        action.actionMap?.name ?? "(No Map)",
                        ExtractPrimaryGroupName(binding.groups),
                        ExtractDeviceProfileName(resolvedBindingPath, binding.path),
                        i,
                        binding.id,
                        displayName,
                        binding.isComposite,
                        binding.isPartOfComposite,
                        IsActionMapRebindable(action.actionMap?.name) && (!binding.isComposite || CompositeBindingUtility.IsSupportedModifierComposite(action, i)),
                        binding.groups,
                        resolvedBindingPath,
                        CompositeBindingUtility.GetModifierCount(action, i),
                        resolvedBindingPaths));
                }
            }

            entries.Sort((left, right) =>
            {
                var mapCompare = string.Compare(left.ActionMapName, right.ActionMapName, StringComparison.OrdinalIgnoreCase);
                if (mapCompare != 0)
                {
                    return mapCompare;
                }

                var schemeCompare = string.Compare(left.SchemeName, right.SchemeName, StringComparison.OrdinalIgnoreCase);
                if (schemeCompare != 0)
                {
                    return schemeCompare;
                }

                var profileCompare = string.Compare(left.ProfileName, right.ProfileName, StringComparison.OrdinalIgnoreCase);
                if (profileCompare != 0)
                {
                    return profileCompare;
                }

                var actionCompare = string.Compare(left.ActionName, right.ActionName, StringComparison.OrdinalIgnoreCase);
                if (actionCompare != 0)
                {
                    return actionCompare;
                }

                return left.BindingIndex.CompareTo(right.BindingIndex);
            });

            return entries;
        }

        public IReadOnlyList<string> GetBindingGroups()
        {
            if (inputActionAsset == null)
            {
                return Array.Empty<string>();
            }

            var groups = new List<string>();
            foreach (var action in inputActionAsset)
            {
                for (var i = 0; i < action.bindings.Count; i++)
                {
                    var bindingGroups = action.bindings[i].groups;
                    if (string.IsNullOrWhiteSpace(bindingGroups))
                    {
                        continue;
                    }

                    var tokens = bindingGroups.Split(';');
                    for (var j = 0; j < tokens.Length; j++)
                    {
                        var group = tokens[j].Trim();
                        if (!string.IsNullOrWhiteSpace(group))
                        {
                            groups.Add(group);
                        }
                    }
                }
            }

            return groups
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public bool HasDuplicateBinding(InputAction targetAction, int targetBindingIndex, string bindingGroup, out InputAction conflictAction, out int conflictBindingIndex)
        {
            conflictAction = null;
            conflictBindingIndex = -1;

            if (targetAction == null || targetBindingIndex < 0 || targetBindingIndex >= targetAction.bindings.Count)
            {
                return false;
            }

            var targetRoot = CompositeBindingUtility.GetRootIndex(targetAction, targetBindingIndex);
            var targetIdentity = CompositeBindingUtility.GetIdentity(targetAction, targetRoot);
            if (string.IsNullOrEmpty(targetIdentity))
            {
                return false;
            }

            if (inputActionAsset == null)
            {
                return false;
            }

            foreach (var action in inputActionAsset)
            {
                if (action != targetAction)
                {
                    continue;
                }

                for (var i = 0; i < action.bindings.Count; i++)
                {
                    if (i != CompositeBindingUtility.GetRootIndex(action, i))
                    {
                        continue;
                    }
                    if (action == targetAction && i == targetRoot)
                    {
                        continue;
                    }

                    if (!IsLogicalBindingInGroup(action, i, bindingGroup))
                    {
                        continue;
                    }

                    if (!string.Equals(CompositeBindingUtility.GetIdentity(action, i), targetIdentity, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    conflictAction = action;
                    conflictBindingIndex = i;
                    return true;
                }
            }

            return false;
        }

        public bool TryFindConflictingBinding(InputAction targetAction, int targetBindingIndex, string bindingGroup, out InputAction conflictAction, out int conflictBindingIndex)
        {
            conflictAction = null;
            conflictBindingIndex = -1;
            if (targetAction == null || targetBindingIndex < 0 || targetBindingIndex >= targetAction.bindings.Count)
            {
                return false;
            }

            var targetRoot = CompositeBindingUtility.GetRootIndex(targetAction, targetBindingIndex);
            var targetIdentity = CompositeBindingUtility.GetIdentity(targetAction, targetRoot);
            if (string.IsNullOrEmpty(targetIdentity) || inputActionAsset == null)
            {
                return false;
            }

            foreach (var action in inputActionAsset)
            {
                if (action == targetAction || !IsActionMapRebindable(action.actionMap?.name))
                {
                    continue;
                }

                for (var i = 0; i < action.bindings.Count; i++)
                {
                    if (i != CompositeBindingUtility.GetRootIndex(action, i) || !IsLogicalBindingInGroup(action, i, bindingGroup))
                    {
                        continue;
                    }

                    if (string.Equals(CompositeBindingUtility.GetIdentity(action, i), targetIdentity, StringComparison.OrdinalIgnoreCase))
                    {
                        conflictAction = action;
                        conflictBindingIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        public static void DisableBinding(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                return;
            }

            CompositeBindingUtility.Disable(action, bindingIndex);
        }

        public bool TryFindAction(Guid actionId, out InputAction action)
        {
            action = null;
            if (inputActionAsset == null)
            {
                return false;
            }

            action = inputActionAsset.FindAction(actionId);
            return action != null;
        }

        public bool IsActionRebindable(InputAction action) =>
            action != null && IsActionMapRebindable(action.actionMap?.name);

        private bool IsActionMapRebindable(string actionMapName) =>
            string.IsNullOrWhiteSpace(actionMapName) || !nonRebindableActionMaps.Contains(actionMapName);

        private void RemoveProtectedOverrides()
        {
            if (inputActionAsset == null || nonRebindableActionMaps.Count == 0)
            {
                return;
            }

            foreach (var actionMap in inputActionAsset.actionMaps)
            {
                if (!nonRebindableActionMaps.Contains(actionMap.name))
                {
                    continue;
                }

                foreach (var action in actionMap.actions)
                {
                    action.RemoveAllBindingOverrides();
                }
            }
        }

        private static bool IsBindingInGroup(InputBinding binding, string bindingGroup)
        {
            if (string.IsNullOrWhiteSpace(bindingGroup))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(binding.groups))
            {
                return false;
            }

            var groups = binding.groups.Split(';');
            for (var i = 0; i < groups.Length; i++)
            {
                if (string.Equals(groups[i].Trim(), bindingGroup, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLogicalBindingInGroup(InputAction action, int bindingIndex, string bindingGroup)
        {
            if (IsBindingInGroup(action.bindings[bindingIndex], bindingGroup)) return true;
            if (!action.bindings[bindingIndex].isComposite) return false;
            var parts = CompositeBindingUtility.GetPartIndices(action, bindingIndex);
            for (var i = 0; i < parts.Count; i++)
            {
                if (IsBindingInGroup(action.bindings[parts[i]], bindingGroup)) return true;
            }
            return false;
        }

        private static string ExtractPrimaryGroupName(string groups)
        {
            if (string.IsNullOrWhiteSpace(groups))
            {
                return "Default";
            }

            var values = groups.Split(';');
            for (var i = 0; i < values.Length; i++)
            {
                var value = values[i].Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return "Default";
        }

        private static string ExtractDeviceProfileName(string effectivePath, string path)
        {
            var sourcePath = string.IsNullOrWhiteSpace(effectivePath) ? path : effectivePath;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return "Any";
            }

            var start = sourcePath.IndexOf('<');
            if (start < 0)
            {
                return "Any";
            }

            var end = sourcePath.IndexOf('>', start + 1);
            if (end <= start + 1)
            {
                return "Any";
            }

            return sourcePath.Substring(start + 1, end - start - 1);
        }
    }
}
