using System;
using UnityEngine.InputSystem;

namespace Koiusa.Input
{
    public sealed class InputActionBinding : IDisposable
    {
        private InputAction action;
        private Action<InputAction.CallbackContext> callback;
        private Action<InputAction.CallbackContext> canceledCallback;
        private InputActionLease lease;

        private InputActionBinding(
            InputAction action,
            Action<InputAction.CallbackContext> callback,
            Action<InputAction.CallbackContext> canceledCallback = null)
        {
            this.action = action;
            this.callback = callback;
            this.canceledCallback = canceledCallback;
            action.performed += callback;
            if (canceledCallback != null)
            {
                action.canceled += canceledCallback;
            }
            lease = InputActionLease.Acquire(action);
        }

        public static InputActionBinding Bind(InputAction action, Action<InputAction.CallbackContext> callback)
        {
            return action == null || callback == null ? null : new InputActionBinding(action, callback);
        }

        public static InputActionBinding Bind(
            InputAction action,
            Action<InputAction.CallbackContext> performedCallback,
            Action<InputAction.CallbackContext> canceledCallback)
        {
            return action == null || performedCallback == null
                ? null
                : new InputActionBinding(action, performedCallback, canceledCallback);
        }

        public void Dispose()
        {
            if (action == null)
            {
                return;
            }

            action.performed -= callback;
            if (canceledCallback != null)
            {
                action.canceled -= canceledCallback;
            }
            lease?.Dispose();
            lease = null;
            callback = null;
            canceledCallback = null;
            action = null;
        }
    }
}
