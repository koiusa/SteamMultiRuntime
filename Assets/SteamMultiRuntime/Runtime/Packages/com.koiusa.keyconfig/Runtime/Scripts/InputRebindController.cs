using System;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    public enum RebindConflictResolution
    {
        ReplaceExisting,
        KeepBoth,
        Cancel
    }

    public sealed class InputRebindController : IDisposable
    {
        private const float RebindTimeoutSeconds = 5f;
        private readonly InputBindingService bindingService;
        private InputActionRebindingExtensions.RebindingOperation operation;
        private InputAction activeAction;
        private int activeBindingIndex = -1;
        private string activeBindingGroup;
        private bool activeActionWasEnabled;
        private InputAction pendingTargetAction;
        private int pendingTargetBindingIndex = -1;
        private string pendingTargetPreviousOverride;
        private InputAction pendingConflictAction;
        private int pendingConflictBindingIndex = -1;
        private string pendingDisplayString;

        public InputRebindController(InputBindingService bindingService)
        {
            this.bindingService = bindingService;
        }

        public bool IsRebinding => operation != null;
        public bool HasPendingConflict => pendingTargetAction != null;
        public bool IsBusy => IsRebinding || HasPendingConflict;

        public event Action RebindStarted;
        public event Action<string> RebindCompleted;
        public event Action<string, string> RebindConflict;
        public event Action RebindCanceled;
        public event Action<string> RebindFailed;

        public bool StartRebind(Guid actionId, int bindingIndex, string bindingGroup = null)
        {
            if (IsRebinding)
            {
                return false;
            }

            if (bindingService == null || !bindingService.TryFindAction(actionId, out var action))
            {
                RebindFailed?.Invoke("Action not found.");
                return false;
            }

            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                RebindFailed?.Invoke("Binding index out of range.");
                return false;
            }

            var binding = action.bindings[bindingIndex];
            if (binding.isComposite)
            {
                RebindFailed?.Invoke("Composite binding cannot be rebound directly.");
                return false;
            }

            activeAction = action;
            activeBindingIndex = bindingIndex;
            activeBindingGroup = bindingGroup;
            activeActionWasEnabled = action.enabled;

            var previousOverride = binding.overridePath;

            action.Disable();
            operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(RebindTimeoutSeconds)
                .OnComplete(op =>
                {
                    var displayString = string.Empty;
                    var errorMessage = string.Empty;
                    var hasConflict = false;
                    try
                    {
                        if (bindingService.HasDuplicateBinding(activeAction, activeBindingIndex, activeBindingGroup, out _, out _))
                        {
                            RestoreBindingOverride(activeAction, activeBindingIndex, previousOverride);
                            errorMessage = "Duplicate binding detected.";
                        }
                        else
                        {
                            displayString = bindingService.GetBindingDisplayString(activeAction, activeBindingIndex);
                            if (bindingService.TryFindConflictingBinding(activeAction, activeBindingIndex, activeBindingGroup, out var conflictAction, out var conflictBindingIndex))
                            {
                                pendingTargetAction = activeAction;
                                pendingTargetBindingIndex = activeBindingIndex;
                                pendingTargetPreviousOverride = previousOverride;
                                pendingConflictAction = conflictAction;
                                pendingConflictBindingIndex = conflictBindingIndex;
                                pendingDisplayString = displayString;
                                hasConflict = true;
                            }
                        }
                    }
                    finally
                    {
                        CleanupAfterRebind();
                    }

                    if (hasConflict) RebindConflict?.Invoke(pendingTargetAction.name, pendingConflictAction.name);
                    else if (string.IsNullOrEmpty(errorMessage)) RebindCompleted?.Invoke(displayString);
                    else RebindFailed?.Invoke(errorMessage);
                })
                .OnCancel(op =>
                {
                    try
                    {
                        RestoreBindingOverride(activeAction, activeBindingIndex, previousOverride);
                    }
                    finally
                    {
                        CleanupAfterRebind();
                    }

                    RebindCanceled?.Invoke();
                });

            RebindStarted?.Invoke();
            operation.Start();
            return true;
        }

        public void CancelRebind()
        {
            if (operation != null) operation.Cancel();
            else if (HasPendingConflict) ResolveConflict(RebindConflictResolution.Cancel);
        }

        public void ResolveConflict(RebindConflictResolution resolution)
        {
            if (!HasPendingConflict)
            {
                return;
            }

            var displayString = pendingDisplayString;
            if (resolution == RebindConflictResolution.Cancel)
            {
                RestoreBindingOverride(pendingTargetAction, pendingTargetBindingIndex, pendingTargetPreviousOverride);
                ClearPendingConflict();
                RebindCanceled?.Invoke();
                return;
            }

            if (resolution == RebindConflictResolution.ReplaceExisting)
            {
                InputBindingService.DisableBinding(pendingConflictAction, pendingConflictBindingIndex);
            }

            ClearPendingConflict();
            RebindCompleted?.Invoke(displayString);
        }

        public void Dispose()
        {
            if (HasPendingConflict)
            {
                RestoreBindingOverride(pendingTargetAction, pendingTargetBindingIndex, pendingTargetPreviousOverride);
                ClearPendingConflict();
            }
            CleanupAfterRebind();
            GC.SuppressFinalize(this);
        }

        private static void RestoreBindingOverride(InputAction action, int bindingIndex, string previousOverride)
        {
            if (string.IsNullOrEmpty(previousOverride))
            {
                action.RemoveBindingOverride(bindingIndex);
                return;
            }

            action.ApplyBindingOverride(bindingIndex, previousOverride);
        }

        private void CleanupAfterRebind()
        {
            if (operation != null)
            {
                operation.Dispose();
                operation = null;
            }

            if (activeAction != null && activeActionWasEnabled)
            {
                activeAction.Enable();
            }

            activeAction = null;
            activeBindingIndex = -1;
            activeBindingGroup = null;
            activeActionWasEnabled = false;
        }

        private void ClearPendingConflict()
        {
            pendingTargetAction = null;
            pendingTargetBindingIndex = -1;
            pendingTargetPreviousOverride = null;
            pendingConflictAction = null;
            pendingConflictBindingIndex = -1;
            pendingDisplayString = null;
        }
    }
}
