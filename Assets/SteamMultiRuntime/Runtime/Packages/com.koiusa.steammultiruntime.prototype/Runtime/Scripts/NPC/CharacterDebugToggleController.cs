using System;
using Koiusa.Input;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class CharacterDebugToggleController : IDisposable
    {
        private readonly Action toggle;
        private InputActionBinding binding;

        public CharacterDebugToggleController(InputActionsConfig inputActionsConfig, Action toggle)
        {
            this.toggle = toggle;
            binding = InputActionBinding.Bind(
                inputActionsConfig?.FindAction("System/CharacterDebugToggle"),
                OnTogglePerformed);
        }

        public void Dispose()
        {
            binding?.Dispose();
            binding = null;
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            toggle?.Invoke();
        }
    }
}
