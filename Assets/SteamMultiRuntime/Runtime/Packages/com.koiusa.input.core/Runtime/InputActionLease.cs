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
        private static readonly Dictionary<InputAction, int> RefCounts = new();
        private InputAction action;

        private InputActionLease(InputAction action)
        {
            this.action = action;
            if (!RefCounts.TryGetValue(action, out var count))
            {
                count = 0;
            }

            RefCounts[action] = count + 1;
            if (count == 0)
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

            if (RefCounts.TryGetValue(action, out var count) && count > 1)
            {
                RefCounts[action] = count - 1;
            }
            else
            {
                RefCounts.Remove(action);
                action.Disable();
            }

            action = null;
        }
    }
}
