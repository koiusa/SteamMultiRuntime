using System;
using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    [RequireComponent(typeof(SoloLockTargetBinder))]
    [DisallowMultipleComponent]
    public sealed class SoloLockTargetInput : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private TargetingInputActionsConfig inputActionsConfig;

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
            nextTargetBinding = InputActionBinding.Bind(inputActionsConfig?.NextTargetAction, OnNextTargetPerformed);
            prevTargetBinding = InputActionBinding.Bind(inputActionsConfig?.PreviousTargetAction, OnPrevTargetPerformed);

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
            nextTargetBinding?.Dispose();
            prevTargetBinding?.Dispose();
            nextTargetBinding = null;
            prevTargetBinding = null;

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
                indicatorController.SetTargetsState(new[] { target }, target);
            }
        }

        private void OnTargetUnlooked(ITargetable target)
        {
            if (indicatorController != null)
            {
                indicatorController.SetTargetsState(null, null);
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

        private InputActionBinding nextTargetBinding;
        private InputActionBinding prevTargetBinding;
    }
}

