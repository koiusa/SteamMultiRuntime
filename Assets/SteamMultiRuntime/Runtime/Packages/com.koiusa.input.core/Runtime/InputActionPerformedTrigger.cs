using Koiusa.Input;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Koiusa.Input
{
    public sealed class InputActionPerformedTrigger : MonoBehaviour
    {
        [SerializeField] private InputActionsConfig inputActionsConfig;
        [SerializeField] private string actionPath;
        [SerializeField] private UnityEvent performed = new();

        private InputAction action;
        private InputActionLease actionLease;

        public UnityEvent Performed => performed;

        private void OnEnable()
        {
            action = inputActionsConfig?.FindAction(actionPath);
            if (action == null)
            {
                Debug.LogWarning($"Input Action '{actionPath}' was not found.", this);
                return;
            }

            actionLease = InputActionLease.Acquire(action);
            action.performed += OnPerformed;
        }

        private void OnDisable()
        {
            if (action != null)
            {
                action.performed -= OnPerformed;
            }

            actionLease?.Dispose();
            actionLease = null;
            action = null;
        }

        private void OnPerformed(InputAction.CallbackContext context)
        {
            performed?.Invoke();
        }
    }
}
