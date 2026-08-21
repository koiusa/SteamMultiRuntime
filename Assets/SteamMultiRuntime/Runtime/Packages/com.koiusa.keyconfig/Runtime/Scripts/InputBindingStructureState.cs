using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    [Serializable]
    internal sealed class InputBindingStateEnvelope
    {
        public int version = 1;
        public string overridesJson;
        public List<InputBindingStructureRecord> structures = new List<InputBindingStructureRecord>();
    }

    [Serializable]
    internal sealed class InputBindingStructureRecord
    {
        public string actionId;
        public string rootBindingId;
        public List<InputBindingDefinition> bindings = new List<InputBindingDefinition>();
    }

    [Serializable]
    internal sealed class InputBindingDefinition
    {
        public string name;
        public string id;
        public string path;
        public string interactions;
        public string processors;
        public string groups;
        public bool isComposite;
        public bool isPartOfComposite;

        public static InputBindingDefinition From(InputBinding binding) => new InputBindingDefinition
        {
            name = binding.name,
            id = binding.id.ToString(),
            path = binding.path,
            interactions = binding.interactions,
            processors = binding.processors,
            groups = binding.groups,
            isComposite = binding.isComposite,
            isPartOfComposite = binding.isPartOfComposite
        };

        public InputBinding ToBinding() => new InputBinding
        {
            name = name,
            id = Guid.Parse(id),
            path = path,
            interactions = interactions,
            processors = processors,
            groups = groups,
            isComposite = isComposite,
            isPartOfComposite = isPartOfComposite
        };
    }

    internal sealed class InputBindingStructureState
    {
        private readonly InputActionAsset asset;
        private readonly Dictionary<Guid, InputBindingStructureRecord> originalRecords = new Dictionary<Guid, InputBindingStructureRecord>();
        private readonly HashSet<Guid> transformedRootIds = new HashSet<Guid>();

        public InputBindingStructureState(InputActionAsset asset)
        {
            this.asset = asset;
            CaptureOriginalRecords();
        }

        public bool ChangeModifierCount(InputAction action, int bindingIndex, int delta)
        {
            var rootIndex = CompositeBindingUtility.GetRootIndex(action, bindingIndex);
            if (rootIndex < 0) return false;
            var root = action.bindings[rootIndex];
            if (root.isComposite && !CompositeBindingUtility.IsSupportedModifierComposite(action, rootIndex)) return false;
            var currentCount = CompositeBindingUtility.GetModifierCount(action, rootIndex);
            var targetCount = Math.Max(0, Math.Min(2, currentCount + delta));
            if (targetCount == currentCount) return false;

            var replacement = targetCount == 0
                ? CreateSingleRecord(action, rootIndex)
                : CreateModifierRecord(action, rootIndex, targetCount);
            ReplaceLogicalBinding(action, rootIndex, replacement.bindings);
            transformedRootIds.Add(Guid.Parse(replacement.rootBindingId));
            return true;
        }

        public bool RestoreOriginal(InputAction action, int bindingIndex)
        {
            var rootIndex = CompositeBindingUtility.GetRootIndex(action, bindingIndex);
            if (rootIndex < 0) return false;
            var rootId = action.bindings[rootIndex].id;
            if (!transformedRootIds.Contains(rootId) || !originalRecords.TryGetValue(rootId, out var original)) return false;
            ReplaceLogicalBinding(action, rootIndex, original.bindings);
            transformedRootIds.Remove(rootId);
            return true;
        }

        public void RestoreAllOriginal()
        {
            var ids = new List<Guid>(transformedRootIds);
            for (var i = 0; i < ids.Count; i++)
            {
                if (!TryFindRoot(ids[i], out var action, out var rootIndex) || !originalRecords.TryGetValue(ids[i], out var original)) continue;
                ReplaceLogicalBinding(action, rootIndex, original.bindings);
            }
            transformedRootIds.Clear();
        }

        public string Capture(string overridesJson)
        {
            if (transformedRootIds.Count == 0) return overridesJson;
            var envelope = new InputBindingStateEnvelope { overridesJson = overridesJson };
            foreach (var rootId in transformedRootIds)
            {
                if (TryFindRoot(rootId, out var action, out var rootIndex)) envelope.structures.Add(CaptureRecord(action, rootIndex));
            }
            return JsonUtility.ToJson(envelope);
        }

        public string Restore(string persistedJson)
        {
            RestoreAllOriginal();
            if (!TryParseEnvelope(persistedJson, out var envelope)) return persistedJson;
            for (var i = 0; i < envelope.structures.Count; i++)
            {
                var record = envelope.structures[i];
                if (!Guid.TryParse(record.rootBindingId, out var rootId) || !TryFindRoot(rootId, out var action, out var rootIndex)) continue;
                ReplaceLogicalBinding(action, rootIndex, record.bindings);
                transformedRootIds.Add(rootId);
            }
            return envelope.overridesJson;
        }

        private void CaptureOriginalRecords()
        {
            if (asset == null) return;
            foreach (var action in asset)
            {
                for (var i = 0; i < action.bindings.Count; i++)
                {
                    if (CompositeBindingUtility.GetRootIndex(action, i) != i) continue;
                    var record = CaptureRecord(action, i);
                    originalRecords[Guid.Parse(record.rootBindingId)] = record;
                }
            }
        }

        private static InputBindingStructureRecord CaptureRecord(InputAction action, int rootIndex)
        {
            var record = new InputBindingStructureRecord
            {
                actionId = action.id.ToString(),
                rootBindingId = action.bindings[rootIndex].id.ToString()
            };
            record.bindings.Add(InputBindingDefinition.From(action.bindings[rootIndex]));
            var parts = CompositeBindingUtility.GetPartIndices(action, rootIndex);
            for (var i = 0; i < parts.Count; i++) record.bindings.Add(InputBindingDefinition.From(action.bindings[parts[i]]));
            return record;
        }

        private static InputBindingStructureRecord CreateSingleRecord(InputAction action, int rootIndex)
        {
            var root = action.bindings[rootIndex];
            var parts = CompositeBindingUtility.GetPartIndices(action, rootIndex);
            var buttonIndex = parts.Count > 0 ? parts[parts.Count - 1] : rootIndex;
            var button = action.bindings[buttonIndex];
            var definition = InputBindingDefinition.From(root);
            definition.name = string.Empty;
            definition.path = button.effectivePath;
            definition.isComposite = false;
            definition.isPartOfComposite = false;
            return new InputBindingStructureRecord
            {
                actionId = action.id.ToString(), rootBindingId = root.id.ToString(), bindings = new List<InputBindingDefinition> { definition }
            };
        }

        private static InputBindingStructureRecord CreateModifierRecord(InputAction action, int rootIndex, int modifierCount)
        {
            var source = action.bindings[rootIndex];
            var existingParts = CompositeBindingUtility.GetPartIndices(action, rootIndex);
            var buttonBinding = existingParts.Count > 0 ? action.bindings[existingParts[existingParts.Count - 1]] : source;
            var buttonPath = buttonBinding.effectivePath;
            var gamepad = buttonPath != null && buttonPath.IndexOf("Gamepad", StringComparison.OrdinalIgnoreCase) >= 0;
            var root = InputBindingDefinition.From(source);
            root.name = modifierCount == 1 ? "ButtonWithOneModifier" : "ButtonWithTwoModifiers";
            root.path = root.name;
            root.isComposite = true;
            root.isPartOfComposite = false;
            var definitions = new List<InputBindingDefinition> { root };
            for (var i = 0; i < modifierCount; i++)
            {
                var existing = i < existingParts.Count - 1 ? action.bindings[existingParts[i]] : default;
                var path = i < existingParts.Count - 1
                    ? existing.effectivePath
                    : gamepad ? (i == 0 ? "<Gamepad>/leftShoulder" : "<Gamepad>/rightShoulder")
                    : (i == 0 ? "<Keyboard>/leftCtrl" : "<Keyboard>/leftShift");
                definitions.Add(new InputBindingDefinition
                {
                    name = modifierCount == 1 ? "Modifier" : $"Modifier{i + 1}",
                    id = i < existingParts.Count - 1 ? existing.id.ToString() : Guid.NewGuid().ToString(),
                    path = path,
                    groups = string.IsNullOrWhiteSpace(existing.groups) ? source.groups : existing.groups,
                    isPartOfComposite = true
                });
            }
            var button = new InputBindingDefinition
            {
                name = "Button",
                id = existingParts.Count > 0 ? buttonBinding.id.ToString() : Guid.NewGuid().ToString(),
                path = buttonPath,
                groups = string.IsNullOrWhiteSpace(buttonBinding.groups) ? source.groups : buttonBinding.groups,
                isPartOfComposite = true
            };
            definitions.Add(button);
            return new InputBindingStructureRecord
            {
                actionId = action.id.ToString(), rootBindingId = source.id.ToString(), bindings = definitions
            };
        }

        private static void ReplaceLogicalBinding(InputAction action, int rootIndex, IReadOnlyList<InputBindingDefinition> definitions)
        {
            var wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();

            var logicalEnd = rootIndex + 1;
            while (logicalEnd < action.bindings.Count && action.bindings[logicalEnd].isPartOfComposite) logicalEnd++;
            var rebuiltBindings = new List<InputBinding>(action.bindings.Count - (logicalEnd - rootIndex) + definitions.Count);
            for (var i = 0; i < rootIndex; i++) rebuiltBindings.Add(action.bindings[i]);
            for (var i = 0; i < definitions.Count; i++) rebuiltBindings.Add(definitions[i].ToBinding());
            for (var i = logicalEnd; i < action.bindings.Count; i++) rebuiltBindings.Add(action.bindings[i]);

            while (action.bindings.Count > 0) action.ChangeBinding(0).Erase();
            for (var i = 0; i < rebuiltBindings.Count; i++) action.AddBinding(rebuiltBindings[i]);
            if (wasEnabled) action.Enable();
        }

        private bool TryFindRoot(Guid rootId, out InputAction action, out int rootIndex)
        {
            action = null;
            rootIndex = -1;
            if (asset == null) return false;
            foreach (var candidateAction in asset)
            {
                for (var i = 0; i < candidateAction.bindings.Count; i++)
                {
                    if (candidateAction.bindings[i].id != rootId || CompositeBindingUtility.GetRootIndex(candidateAction, i) != i) continue;
                    action = candidateAction;
                    rootIndex = i;
                    return true;
                }
            }
            return false;
        }

        private static bool TryParseEnvelope(string json, out InputBindingStateEnvelope envelope)
        {
            envelope = null;
            if (string.IsNullOrWhiteSpace(json) || json.IndexOf("\"structures\"", StringComparison.Ordinal) < 0) return false;
            try
            {
                envelope = JsonUtility.FromJson<InputBindingStateEnvelope>(json);
                return envelope != null && envelope.version == 1 && envelope.structures != null;
            }
            catch (ArgumentException) { return false; }
        }
    }
}
