using System;
using UnityEngine.InputSystem;

namespace Koiusa.Input
{
    public sealed class InputActionBinding : IDisposable
    {
        private InputAction action;
        private Action<InputAction.CallbackContext> callback;
        private InputActionLease lease;

        private InputActionBinding(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            this.action = action;
            this.callback = callback;
            action.performed += callback;
            lease = InputActionLease.Acquire(action);
        }

        public static InputActionBinding Bind(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            return action == null || callback == null ? null : new InputActionBinding(action, callback);
        }

        public void Dispose()
        {
            if (action == null)
            {
                return;
            }

            action.performed -= callback;
            lease?.Dispose();
            lease = null;
            callback = null;
            action = null;
        }
    }
}
