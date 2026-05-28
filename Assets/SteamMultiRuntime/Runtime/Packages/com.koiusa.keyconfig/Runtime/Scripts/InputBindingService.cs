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
            public BindingEntry(Guid actionId, string actionName, string actionMapName, string schemeName, string profileName, int bindingIndex, Guid bindingId, string displayName, bool isComposite, bool isPartOfComposite, string groups, string bindingPath)
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
                Groups = groups;
                BindingPath = bindingPath;
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
            public string Groups { get; }
            public string BindingPath { get; }
        }

        private readonly InputActionAsset inputActionAsset;
        private readonly InputBindingOverridesRepository repository;

        public InputBindingService(InputActionAsset inputActionAsset, InputBindingOverridesRepository repository = null)
        {
            this.inputActionAsset = inputActionAsset;
            this.repository = repository ?? new InputBindingOverridesRepository();
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

            inputActionAsset.LoadBindingOverridesFromJson(json);
            return true;
        }

        public void SaveOverrides(string userId)
        {
            if (inputActionAsset == null)
            {
                return;
            }

            var json = inputActionAsset.SaveBindingOverridesAsJson();
            repository.Save(userId, json);
        }

        public void ResetAllOverrides(string userId = null)
        {
            if (inputActionAsset != null)
            {
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

            action.RemoveBindingOverride(bindingIndex);
        }

        public string GetBindingDisplayString(InputAction action, int bindingIndex)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                return string.Empty;
            }

            return action.GetBindingDisplayString(bindingIndex);
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
                    if (!IsBindingInGroup(binding, bindingGroup))
                    {
                        continue;
                    }

                    var displayName = action.GetBindingDisplayString(i);
                    entries.Add(new BindingEntry(
                        action.id,
                        action.name,
                        action.actionMap?.name ?? "(No Map)",
                        ExtractPrimaryGroupName(binding.groups),
                        ExtractDeviceProfileName(binding.effectivePath, binding.path),
                        i,
                        binding.id,
                        displayName,
                        binding.isComposite,
                        binding.isPartOfComposite,
                        binding.groups,
                        string.IsNullOrEmpty(binding.effectivePath) ? binding.path : binding.effectivePath));
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

            var targetBinding = targetAction.bindings[targetBindingIndex];
            var targetPath = targetBinding.effectivePath;
            if (string.IsNullOrEmpty(targetPath))
            {
                return false;
            }

            if (inputActionAsset == null)
            {
                return false;
            }

            foreach (var action in inputActionAsset)
            {
                for (var i = 0; i < action.bindings.Count; i++)
                {
                    if (action == targetAction && i == targetBindingIndex)
                    {
                        continue;
                    }

                    var candidate = action.bindings[i];
                    if (candidate.isComposite)
                    {
                        continue;
                    }

                    if (!IsBindingInGroup(candidate, bindingGroup))
                    {
                        continue;
                    }

                    if (!string.Equals(candidate.effectivePath, targetPath, StringComparison.OrdinalIgnoreCase))
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
