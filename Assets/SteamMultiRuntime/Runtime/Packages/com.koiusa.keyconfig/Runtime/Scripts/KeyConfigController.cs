using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Koiusa.KeyConfig
{
    public readonly struct KeyConfigBindingId : IEquatable<KeyConfigBindingId>
    {
        public KeyConfigBindingId(Guid actionId, Guid bindingId)
        {
            ActionId = actionId;
            BindingId = bindingId;
        }

        public Guid ActionId { get; }
        public Guid BindingId { get; }

        public bool Equals(KeyConfigBindingId other) => ActionId.Equals(other.ActionId) && BindingId.Equals(other.BindingId);
        public override bool Equals(object obj) => obj is KeyConfigBindingId other && Equals(other);
        public override int GetHashCode() => (ActionId.GetHashCode() * 397) ^ BindingId.GetHashCode();
    }

    public sealed class KeyConfigBinding
    {
        internal KeyConfigBinding(InputBindingService.BindingEntry entry)
        {
            Id = new KeyConfigBindingId(entry.ActionId, entry.BindingId);
            ActionMapName = entry.ActionMapName;
            ActionName = entry.ActionName;
            BindingGroup = entry.Groups;
            DeviceScheme = entry.SchemeName;
            DeviceProfile = entry.ProfileName;
            EffectivePath = entry.BindingPath;
            EffectivePaths = entry.BindingPaths;
            DisplayName = entry.DisplayName;
            IsComposite = entry.IsComposite;
            CompositePartPaths = entry.IsComposite ? entry.BindingPaths : Array.Empty<string>();
            IsRebindable = entry.IsRebindable;
            ModifierCount = entry.ModifierCount;
            BindingIndex = entry.BindingIndex;
        }

        public KeyConfigBindingId Id { get; }
        public string ActionMapName { get; }
        public string ActionName { get; }
        public string BindingGroup { get; }
        public string DeviceScheme { get; }
        public string DeviceProfile { get; }
        public string EffectivePath { get; }
        public IReadOnlyList<string> EffectivePaths { get; }
        public string DisplayName { get; }
        public bool IsComposite { get; }
        public IReadOnlyList<string> CompositePartPaths { get; }
        public bool IsRebindable { get; }
        public int ModifierCount { get; }
        public bool CanAddModifier => IsRebindable && ModifierCount < 2;
        public bool CanRemoveModifier => IsRebindable && ModifierCount > 0;
        internal Guid ActionId => Id.ActionId;
        internal Guid BindingId => Id.BindingId;
        internal int BindingIndex { get; }
        internal string SchemeName => DeviceScheme;
        internal string ProfileName => DeviceProfile;
        internal string Groups => BindingGroup;
        internal string BindingPath => EffectivePath;
        internal IReadOnlyList<string> BindingPaths => EffectivePaths;
        internal bool IsPartOfComposite => false;
    }

    public enum KeyConfigRebindStatus { Completed, Canceled, TimedOut, Failed }
    public enum KeyConfigConflictResolution { ReplaceExisting, KeepBoth, Cancel }

    public sealed class KeyConfigRebindResult
    {
        internal KeyConfigRebindResult(KeyConfigRebindStatus status, KeyConfigBindingId bindingId, string displayName, string controlPath, string errorMessage)
        {
            Status = status;
            BindingId = bindingId;
            DisplayName = displayName;
            ControlPath = controlPath;
            ErrorMessage = errorMessage;
        }

        public KeyConfigRebindStatus Status { get; }
        public KeyConfigBindingId BindingId { get; }
        public string DisplayName { get; }
        public string ControlPath { get; }
        public string ErrorMessage { get; }
    }

    public sealed class KeyConfigConflict
    {
        internal KeyConfigConflict(KeyConfigBinding target, KeyConfigBinding existing)
        {
            Target = target;
            Existing = existing;
        }

        public KeyConfigBinding Target { get; }
        public KeyConfigBinding Existing { get; }
    }

    public sealed class KeyConfigController : IDisposable
    {
        private readonly InputBindingService bindingService;
        private readonly InputRebindController rebindController;
        private KeyConfigBindingId activeBindingId;

        public KeyConfigController(InputActionAsset actions, IEnumerable<string> nonRebindableActionMaps = null)
        {
            if (actions == null) throw new ArgumentNullException(nameof(actions));
            bindingService = new InputBindingService(actions, nonRebindableActionMaps);
            rebindController = new InputRebindController(bindingService);
            rebindController.RebindCompleted += OnCompleted;
            rebindController.RebindConflict += OnConflict;
            rebindController.RebindCanceled += OnCanceled;
            rebindController.RebindTimedOut += OnTimedOut;
            rebindController.RebindFailed += OnFailed;
        }

        public bool IsRebinding => rebindController.IsBusy;
        internal InputActionAsset Actions => bindingService.InputActionAsset;
        public event Action<KeyConfigBinding> BindingChanged;
        public event Action<KeyConfigConflict> ConflictDetected;
        public event Action<KeyConfigRebindResult> RebindFinished;

        public IReadOnlyList<string> GetBindingGroups() => bindingService.GetBindingGroups();

        public IReadOnlyList<KeyConfigBinding> GetBindings(string bindingGroup = null)
        {
            var entries = bindingService.GetBindingEntries(bindingGroup);
            var result = new List<KeyConfigBinding>(entries.Count);
            for (var i = 0; i < entries.Count; i++) result.Add(new KeyConfigBinding(entries[i]));
            return result;
        }

        public bool StartRebind(KeyConfigBindingId bindingId, string bindingGroup = null)
        {
            if (!TryResolve(bindingId, out _, out var bindingIndex)) return false;
            activeBindingId = bindingId;
            return rebindController.StartRebind(bindingId.ActionId, bindingIndex, bindingGroup);
        }

        public void CancelRebind() => rebindController.CancelRebind();

        public void ResolveConflict(KeyConfigConflictResolution resolution) =>
            rebindController.ResolveConflict((RebindConflictResolution)resolution);

        public bool Reset(KeyConfigBindingId bindingId)
        {
            if (!TryResolve(bindingId, out var action, out var bindingIndex)) return false;
            bindingService.ResetBinding(action, bindingIndex);
            NotifyBindingChanged(bindingId);
            return true;
        }

        public bool AddModifier(KeyConfigBindingId bindingId) => ChangeModifier(bindingId, true);
        public bool RemoveModifier(KeyConfigBindingId bindingId) => ChangeModifier(bindingId, false);

        public void ResetAll() { bindingService.ResetAllOverrides(); NotifyAllBindingsChanged(); }
        public string ExportOverrides() => bindingService.CaptureOverrides();
        public void ImportOverrides(string json) { bindingService.RestoreOverrides(json); NotifyAllBindingsChanged(); }
        public void ClearOverrides() => ResetAll();

        public void Dispose()
        {
            rebindController.RebindCompleted -= OnCompleted;
            rebindController.RebindConflict -= OnConflict;
            rebindController.RebindCanceled -= OnCanceled;
            rebindController.RebindTimedOut -= OnTimedOut;
            rebindController.RebindFailed -= OnFailed;
            rebindController.Dispose();
        }

        private bool TryResolve(KeyConfigBindingId id, out InputAction action, out int bindingIndex)
        {
            bindingIndex = -1;
            if (!bindingService.TryFindAction(id.ActionId, out action)) return false;
            for (var i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].id != id.BindingId) continue;
                bindingIndex = i;
                return true;
            }
            return false;
        }

        private void OnCompleted(string displayName)
        {
            NotifyBindingChanged(activeBindingId);
            var binding = FindBinding(activeBindingId);
            RebindFinished?.Invoke(new KeyConfigRebindResult(KeyConfigRebindStatus.Completed, activeBindingId, displayName, binding?.EffectivePath, null));
        }

        private void OnConflict(string targetAction, string existingAction)
        {
            var existing = rebindController.PendingConflictAction;
            var existingIndex = rebindController.PendingConflictBindingIndex;
            var existingId = existing != null && existingIndex >= 0 && existingIndex < existing.bindings.Count
                ? new KeyConfigBindingId(existing.id, existing.bindings[existingIndex].id)
                : default;
            ConflictDetected?.Invoke(new KeyConfigConflict(FindBinding(activeBindingId), FindBinding(existingId)));
        }

        private void OnCanceled() =>
            RebindFinished?.Invoke(new KeyConfigRebindResult(KeyConfigRebindStatus.Canceled, activeBindingId, null, null, null));

        private void OnTimedOut() =>
            RebindFinished?.Invoke(new KeyConfigRebindResult(KeyConfigRebindStatus.TimedOut, activeBindingId, null, null, null));

        private void OnFailed(string message) =>
            RebindFinished?.Invoke(new KeyConfigRebindResult(KeyConfigRebindStatus.Failed, activeBindingId, null, null, message));

        private bool ChangeModifier(KeyConfigBindingId id, bool add)
        {
            if (!TryResolve(id, out var action, out var index)) return false;
            var changed = add ? bindingService.AddModifier(action, index) : bindingService.RemoveModifier(action, index);
            if (changed) NotifyBindingChanged(new KeyConfigBindingId(action.id, action.bindings[CompositeBindingUtility.GetRootIndex(action, index)].id));
            return changed;
        }

        private KeyConfigBinding FindBinding(KeyConfigBindingId id)
        {
            var bindings = GetBindings();
            for (var i = 0; i < bindings.Count; i++) if (bindings[i].Id.Equals(id)) return bindings[i];
            return null;
        }

        private void NotifyAllBindingsChanged()
        {
            var bindings = GetBindings();
            for (var i = 0; i < bindings.Count; i++) BindingChanged?.Invoke(bindings[i]);
        }

        private void NotifyBindingChanged(KeyConfigBindingId id)
        {
            var bindings = GetBindings();
            for (var i = 0; i < bindings.Count; i++)
                if (bindings[i].Id.Equals(id)) { BindingChanged?.Invoke(bindings[i]); return; }
        }
    }
}
