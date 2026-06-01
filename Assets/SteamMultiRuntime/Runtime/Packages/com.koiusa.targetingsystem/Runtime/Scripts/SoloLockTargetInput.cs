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

        [Header("References")]
        [SerializeField] private TargetIndicatorController indicatorController;

        private ITargetBinder binder;
        private TargetingCameraRig cameraRig;
        private bool isBound;

        public ITargetable CurrentTarget => binder?.CurrentTarget;

        private void Awake()
        {
            binder = GetComponent<ITargetBinder>();
            cameraRig = GetComponentInParent<TargetingCameraRig>();
            ResolveReferences();
        }

        private void OnEnable()
        {
            if (isBound) return;
            BindAction(nextTargetAction, OnNextTargetPerformed);
            BindAction(prevTargetAction, OnPrevTargetPerformed);

            if (binder is ILockOn lockOn)
            {
                lockOn.Looked += OnTargetLooked;
                lockOn.Unlooked += OnTargetUnlooked;
            }

            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound) return;
            UnbindAction(nextTargetAction, OnNextTargetPerformed);
            UnbindAction(prevTargetAction, OnPrevTargetPerformed);

            if (binder is ILockOn lockOn)
            {
                lockOn.Looked -= OnTargetLooked;
                lockOn.Unlooked -= OnTargetUnlooked;
            }

            isBound = false;
        }

        private bool IsSoloLockMode =>
            cameraRig == null || cameraRig.CurrentMode == TargetingCameraRig.CameraMode.SoloLock;

        private void OnNextTargetPerformed(InputAction.CallbackContext context)
        {
            if (!IsSoloLockMode) return;
            binder?.SelectNext();
        }

        private void OnPrevTargetPerformed(InputAction.CallbackContext context)
        {
            if (!IsSoloLockMode) return;
            binder?.SelectPrev();
        }

        private void OnTargetLooked(ITargetable target)
        {
            if (indicatorController != null)
            {
                indicatorController.SetTargetLocked(target, true);
                indicatorController.SetFocusTarget(target);
            }
        }

        private void OnTargetUnlooked(ITargetable target)
        {
            if (indicatorController != null)
            {
                indicatorController.SetTargetLocked(target, false);

                if (ReferenceEquals(indicatorController.CurrentFocusTarget, target))
                {
                    indicatorController.SetFocusTarget(null);
                }
            }
        }

        private void ResolveReferences()
        {
            if (indicatorController == null)
            {
                indicatorController = GetComponent<TargetIndicatorController>();
            }

            if (indicatorController == null)
            {
                indicatorController = GetComponentInParent<TargetIndicatorController>();
            }
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

