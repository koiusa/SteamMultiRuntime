using System;
using UnityEngine.InputSystem;

namespace Koiusa.Keyconfig.Runtime
{
    public sealed class InputRebindController : IDisposable
    {
        private readonly InputBindingService bindingService;
        private InputActionRebindingExtensions.RebindingOperation operation;
        private InputAction activeAction;
        private int activeBindingIndex = -1;
        private string activeBindingGroup;

        public InputRebindController(InputBindingService bindingService)
        {
            this.bindingService = bindingService;
        }

        public bool IsRebinding => operation != null;

        public event Action RebindStarted;
        public event Action<string> RebindCompleted;
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

            var previousOverride = binding.overridePath;

            action.Disable();
            operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(op =>
                {
                    var displayString = string.Empty;
                    try
                    {
                        if (bindingService.HasDuplicateBinding(activeAction, activeBindingIndex, activeBindingGroup, out _, out _))
                        {
                            RestoreBindingOverride(activeAction, activeBindingIndex, previousOverride);
                            RebindFailed?.Invoke("Duplicate binding detected.");
                            return;
                        }

                        displayString = bindingService.GetBindingDisplayString(activeAction, activeBindingIndex);
                        RebindCompleted?.Invoke(displayString);
                    }
                    finally
                    {
                        CleanupAfterRebind();
                    }
                })
                .OnCancel(op =>
                {
                    try
                    {
                        RestoreBindingOverride(activeAction, activeBindingIndex, previousOverride);
                        RebindCanceled?.Invoke();
                    }
                    finally
                    {
                        CleanupAfterRebind();
                    }
                });

            RebindStarted?.Invoke();
            operation.Start();
            return true;
        }

        public void CancelRebind()
        {
            operation?.Cancel();
        }

        public void Dispose()
        {
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

            if (activeAction != null)
            {
                activeAction.Enable();
            }

            activeAction = null;
            activeBindingIndex = -1;
            activeBindingGroup = null;
        }
    }
}
