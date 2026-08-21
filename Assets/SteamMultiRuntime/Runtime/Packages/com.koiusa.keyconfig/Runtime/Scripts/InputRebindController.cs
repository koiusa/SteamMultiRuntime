using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    public enum RebindConflictResolution { ReplaceExisting, KeepBoth, Cancel }

    public sealed class InputRebindController : IDisposable
    {
        private const float RebindTimeoutSeconds = 5f;
        private readonly InputBindingService bindingService;
        private InputActionRebindingExtensions.RebindingOperation operation;
        private InputAction activeAction;
        private int activeBindingIndex = -1;
        private string activeBindingGroup;
        private bool activeActionWasEnabled;
        private List<int> rebindIndices;
        private int rebindPart;
        private HashSet<string> excludedActuatedControlPaths;
        private List<InputControl> controlsAwaitingRelease;
        private bool waitingForRelease;
        private float releaseWaitDeadline;
        private Dictionary<int, string> previousOverrides;
        private InputAction pendingTargetAction;
        private int pendingTargetBindingIndex = -1;
        private Dictionary<int, string> pendingTargetPreviousOverrides;
        private InputAction pendingConflictAction;
        private int pendingConflictBindingIndex = -1;
        private string pendingDisplayString;

        public InputRebindController(InputBindingService bindingService) => this.bindingService = bindingService;
        public bool IsRebinding => operation != null || waitingForRelease;
        public bool HasPendingConflict => pendingTargetAction != null;
        public bool IsBusy => IsRebinding || HasPendingConflict;
        public event Action RebindStarted;
        public event Action<string> RebindCompleted;
        public event Action<string, string> RebindConflict;
        public event Action RebindCanceled;
        public event Action<string> RebindFailed;

        public bool StartRebind(Guid actionId, int bindingIndex, string bindingGroup = null)
        {
            if (IsBusy) return false;
            if (bindingService == null || !bindingService.TryFindAction(actionId, out var action)) return Fail("Action not found.");
            var rootIndex = CompositeBindingUtility.GetRootIndex(action, bindingIndex);
            if (rootIndex < 0) return Fail("Binding index out of range.");
            var binding = action.bindings[rootIndex];
            if (binding.isComposite && !CompositeBindingUtility.IsSupportedModifierComposite(action, rootIndex)) return Fail("This composite binding is not rebindable.");

            activeAction = action;
            activeBindingIndex = rootIndex;
            activeBindingGroup = bindingGroup;
            activeActionWasEnabled = action.enabled;
            rebindIndices = binding.isComposite ? CompositeBindingUtility.GetPartIndices(action, rootIndex) : new List<int> { rootIndex };
            if (rebindIndices.Count == 0) return FailAndCleanup("Composite binding has no parts.");
            previousOverrides = CaptureOverrides(action, rebindIndices);
            excludedActuatedControlPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            rebindPart = 0;
            action.Disable();
            RebindStarted?.Invoke();
            StartCurrentPart();
            return true;
        }

        private void StartCurrentPart()
        {
            operation = activeAction.PerformInteractiveRebinding(rebindIndices[rebindPart]);
            ExcludePreviouslyReboundControls(operation, activeAction, rebindIndices, rebindPart);
            foreach (var path in excludedActuatedControlPaths) operation.WithControlsExcluding(path);
            operation.WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(RebindTimeoutSeconds)
                .OnComplete(_ => OnPartComplete())
                .OnCancel(_ => OnCanceled());
            operation.Start();
        }

        internal static void ExcludePreviouslyReboundControls(
            InputActionRebindingExtensions.RebindingOperation rebindOperation,
            InputAction action,
            IReadOnlyList<int> bindingIndices,
            int currentPart)
        {
            if (rebindOperation == null || action == null || bindingIndices == null) return;
            var paths = GetPreviouslyReboundControlPaths(action, bindingIndices, currentPart);
            for (var i = 0; i < paths.Count; i++) rebindOperation.WithControlsExcluding(paths[i]);
        }

        internal static List<string> GetPreviouslyReboundControlPaths(
            InputAction action,
            IReadOnlyList<int> bindingIndices,
            int currentPart)
        {
            var result = new List<string>();
            if (action == null || bindingIndices == null) return result;
            var count = Math.Min(currentPart, bindingIndices.Count);
            for (var i = 0; i < count; i++)
            {
                var bindingIndex = bindingIndices[i];
                if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) continue;
                var path = action.bindings[bindingIndex].effectivePath;
                if (!string.IsNullOrWhiteSpace(path)) result.Add(path);
            }
            return result;
        }

        private void OnPartComplete()
        {
            controlsAwaitingRelease = CaptureCurrentlyActuatedControls(excludedActuatedControlPaths);
            DisposeOperation();
            rebindPart++;
            if (rebindPart < rebindIndices.Count)
            {
                if (controlsAwaitingRelease.Count > 0) BeginReleaseWait();
                else StartCurrentPart();
                return;
            }
            var display = bindingService.GetBindingDisplayString(activeAction, activeBindingIndex);
            if (bindingService.HasDuplicateBinding(activeAction, activeBindingIndex, activeBindingGroup, out _, out _))
            {
                RestoreOverrides(activeAction, previousOverrides);
                CleanupAfterRebind();
                RebindFailed?.Invoke("Duplicate binding detected.");
                return;
            }
            if (bindingService.TryFindConflictingBinding(activeAction, activeBindingIndex, activeBindingGroup, out var conflictAction, out var conflictIndex))
            {
                pendingTargetAction = activeAction;
                pendingTargetBindingIndex = activeBindingIndex;
                pendingTargetPreviousOverrides = previousOverrides;
                pendingConflictAction = conflictAction;
                pendingConflictBindingIndex = conflictIndex;
                pendingDisplayString = display;
                CleanupAfterRebind();
                RebindConflict?.Invoke(pendingTargetAction.name, pendingConflictAction.name);
                return;
            }
            CleanupAfterRebind();
            RebindCompleted?.Invoke(display);
        }

        internal static List<InputControl> CaptureCurrentlyActuatedControls(ISet<string> paths)
        {
            var result = new List<InputControl>();
            var devices = InputSystem.devices;
            for (var deviceIndex = 0; deviceIndex < devices.Count; deviceIndex++)
            {
                var device = devices[deviceIndex];
                if (device == null || !device.added) continue;
                var controls = device.allControls;
                for (var controlIndex = 0; controlIndex < controls.Count; controlIndex++)
                {
                    var control = controls[controlIndex];
                    if (!InputControlActivity.IsActive(control)) continue;
                    result.Add(control);
                    if (paths != null && !string.IsNullOrWhiteSpace(control.path)) paths.Add(control.path);
                }
            }
            return result;
        }

        private void BeginReleaseWait()
        {
            waitingForRelease = true;
            releaseWaitDeadline = Time.realtimeSinceStartup + RebindTimeoutSeconds;
            InputSystem.onAfterUpdate -= OnAfterInputUpdate;
            InputSystem.onAfterUpdate += OnAfterInputUpdate;
        }

        private void OnAfterInputUpdate()
        {
            if (!waitingForRelease) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Time.realtimeSinceStartup >= releaseWaitDeadline)
            {
                OnCanceled();
                return;
            }

            for (var i = 0; i < controlsAwaitingRelease.Count; i++)
            {
                if (InputControlActivity.IsActive(controlsAwaitingRelease[i])) return;
            }

            StopReleaseWait();
            StartCurrentPart();
        }

        private void StopReleaseWait()
        {
            InputSystem.onAfterUpdate -= OnAfterInputUpdate;
            waitingForRelease = false;
            releaseWaitDeadline = 0f;
            controlsAwaitingRelease = null;
        }

        private void OnCanceled()
        {
            RestoreOverrides(activeAction, previousOverrides);
            CleanupAfterRebind();
            RebindCanceled?.Invoke();
        }

        public void CancelRebind()
        {
            if (operation != null) operation.Cancel();
            else if (waitingForRelease) OnCanceled();
            else if (HasPendingConflict) ResolveConflict(RebindConflictResolution.Cancel);
        }

        public void ResolveConflict(RebindConflictResolution resolution)
        {
            if (!HasPendingConflict) return;
            var display = pendingDisplayString;
            if (resolution == RebindConflictResolution.Cancel)
            {
                RestoreOverrides(pendingTargetAction, pendingTargetPreviousOverrides);
                ClearPendingConflict();
                RebindCanceled?.Invoke();
                return;
            }
            if (resolution == RebindConflictResolution.ReplaceExisting) InputBindingService.DisableBinding(pendingConflictAction, pendingConflictBindingIndex);
            ClearPendingConflict();
            RebindCompleted?.Invoke(display);
        }

        public void Dispose()
        {
            if (HasPendingConflict) RestoreOverrides(pendingTargetAction, pendingTargetPreviousOverrides);
            ClearPendingConflict();
            if (activeAction != null) RestoreOverrides(activeAction, previousOverrides);
            CleanupAfterRebind();
            GC.SuppressFinalize(this);
        }

        private bool Fail(string message) { RebindFailed?.Invoke(message); return false; }
        private bool FailAndCleanup(string message) { CleanupAfterRebind(); return Fail(message); }
        private static Dictionary<int, string> CaptureOverrides(InputAction action, IReadOnlyList<int> indices)
        {
            var result = new Dictionary<int, string>();
            for (var i = 0; i < indices.Count; i++) result[indices[i]] = action.bindings[indices[i]].overridePath;
            return result;
        }
        private static void RestoreOverrides(InputAction action, IReadOnlyDictionary<int, string> values)
        {
            if (action == null || values == null) return;
            foreach (var pair in values)
            {
                if (string.IsNullOrEmpty(pair.Value)) action.RemoveBindingOverride(pair.Key);
                else action.ApplyBindingOverride(pair.Key, pair.Value);
            }
        }
        private void DisposeOperation() { operation?.Dispose(); operation = null; }
        private void CleanupAfterRebind()
        {
            StopReleaseWait();
            DisposeOperation();
            if (activeAction != null && activeActionWasEnabled) activeAction.Enable();
            activeAction = null;
            activeBindingIndex = -1;
            activeBindingGroup = null;
            activeActionWasEnabled = false;
            rebindIndices = null;
            rebindPart = 0;
            excludedActuatedControlPaths = null;
            previousOverrides = null;
        }
        private void ClearPendingConflict()
        {
            pendingTargetAction = null;
            pendingTargetBindingIndex = -1;
            pendingTargetPreviousOverrides = null;
            pendingConflictAction = null;
            pendingConflictBindingIndex = -1;
            pendingDisplayString = null;
        }
    }
}
