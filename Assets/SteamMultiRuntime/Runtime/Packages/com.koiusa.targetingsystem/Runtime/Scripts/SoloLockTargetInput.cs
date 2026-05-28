using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    [RequireComponent(typeof(SoloLockTargetBinder))]
    [DisallowMultipleComponent]
    public sealed class SoloLockTargetInput : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference nextTargetAction;
        [SerializeField] private InputActionReference prevTargetAction;

        private ITargetBinder binder;
        private bool isBound;

        public ITargetable CurrentTarget => binder?.CurrentTarget;

        private void Awake()
        {
            binder = GetComponent<ITargetBinder>();
        }

        private void OnEnable()
        {
            if (isBound) return;
            BindAction(nextTargetAction, OnNextTargetPerformed);
            BindAction(prevTargetAction, OnPrevTargetPerformed);
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound) return;
            UnbindAction(nextTargetAction, OnNextTargetPerformed);
            UnbindAction(prevTargetAction, OnPrevTargetPerformed);
            isBound = false;
        }

        private void OnNextTargetPerformed(InputAction.CallbackContext context)
        {
            binder?.SelectNext();
        }

        private void OnPrevTargetPerformed(InputAction.CallbackContext context)
        {
            binder?.SelectPrev();
        }

        private static void BindAction(InputActionReference actionRef, Action<InputAction.CallbackContext> callback)
        {
            if (actionRef == null || actionRef.action == null) return;
            actionRef.action.Enable();
            actionRef.action.performed += callback;
        }

        private static void UnbindAction(InputActionReference actionRef, Action<InputAction.CallbackContext> callback)
        {
            if (actionRef == null || actionRef.action == null) return;
            actionRef.action.performed -= callback;
            actionRef.action.Disable();
        }
    }
}

