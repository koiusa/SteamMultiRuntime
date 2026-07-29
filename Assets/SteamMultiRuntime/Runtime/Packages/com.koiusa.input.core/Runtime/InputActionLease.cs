using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Koiusa.Input
{
    /// <summary>
    /// Reference-counted ownership for shared actions. One consumer releasing an
    /// action can no longer disable it while another consumer is still using it.
    /// </summary>
    public sealed class InputActionLease : IDisposable
    {
        private sealed class LeaseState
        {
            public int Count;
            public bool DisableOnRelease;
        }

        private static readonly Dictionary<InputAction, LeaseState> States = new();
        private InputAction action;

        private InputActionLease(InputAction action)
        {
            this.action = action;
            if (!States.TryGetValue(action, out var state))
            {
                state = new LeaseState
                {
                    DisableOnRelease = !action.enabled
                };
                States.Add(action, state);
            }

            state.Count++;
            if (!action.enabled)
            {
                action.Enable();
            }
        }

        public static InputActionLease Acquire(InputAction action)
        {
            return action == null ? null : new InputActionLease(action);
        }

        public void Dispose()
        {
            if (action == null)
            {
                return;
            }

            if (States.TryGetValue(action, out var state) && state.Count > 1)
            {
                state.Count--;
            }
            else
            {
                States.Remove(action);
                if (state != null && state.DisableOnRelease)
                {
                    action.Disable();
                }
            }

            action = null;
        }
    }
}
