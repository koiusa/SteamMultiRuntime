using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    [RequireComponent(typeof(LockOnTargetGroupBinder))]
    [DisallowMultipleComponent]
    public sealed class LockOnTargetGroupInput : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private TargetingInputActions inputActionsConfig;

        [Header("References")]
        [SerializeField] private TargetIndicatorController indicatorController;

        private ILockOnTargetBinder binder;
        private TargetingCameraRig cameraRig;
        private bool isBound;

        private void Awake()
        {
            binder = GetComponent<ILockOnTargetBinder>();
            cameraRig = GetComponentInParent<TargetingCameraRig>();
            ResolveReferences();
        }

        private bool IsMultiLockMode =>
            cameraRig == null || cameraRig.CurrentMode == TargetingCameraRig.CameraMode.MultiLock;

        private bool CanBulkLock =>
            cameraRig == null ||
            cameraRig.CurrentMode == TargetingCameraRig.CameraMode.MultiLock ||
            cameraRig.CurrentMode == TargetingCameraRig.CameraMode.NoLock ||
            cameraRig.CurrentMode == TargetingCameraRig.CameraMode.SoloLock;

        private void OnEnable()
        {
            if (isBound)
            {
                return;
            }

            lockBinding = InputActionBinding.Bind(inputActionsConfig?.MultiLockAction, OnLockPerformed);
            nextBinding = InputActionBinding.Bind(inputActionsConfig?.NextTargetAction, OnNextTargetPerformed);
            previousBinding = InputActionBinding.Bind(inputActionsConfig?.PreviousTargetAction, OnPrevTargetPerformed);
            clearBinding = InputActionBinding.Bind(inputActionsConfig?.ClearLockAction, OnUnlockAllPerformed);
            focusBinding = InputActionBinding.Bind(inputActionsConfig?.FocusAction, OnFocusPerformed);
            bulkBinding = InputActionBinding.Bind(inputActionsConfig?.BulkLockAction, OnBulkLockPerformed);

            if (binder is ILockOn lockOn)
            {
                lockOn.Looked += OnTargetLooked;
                lockOn.Unlooked += OnTargetUnlooked;
            }

            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            bulkBinding?.Dispose();
            focusBinding?.Dispose();
            clearBinding?.Dispose();
            previousBinding?.Dispose();
            nextBinding?.Dispose();
            lockBinding?.Dispose();
            bulkBinding = null;
            focusBinding = null;
            clearBinding = null;
            previousBinding = null;
            nextBinding = null;
            lockBinding = null;

            if (binder is ILockOn lockOn)
            {
                lockOn.Looked -= OnTargetLooked;
                lockOn.Unlooked -= OnTargetUnlooked;
            }

            isBound = false;
        }

        private void OnLockPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.LockClosestVisibleTarget();
        }

        private void OnNextTargetPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.SelectNext();
            SyncFocusIndicator();
        }

        private void OnPrevTargetPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.SelectPrev();
            SyncFocusIndicator();
        }

        private void OnUnlockAllPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.UnlockAllTargets();

            if (indicatorController != null)
            {
                indicatorController.SetTargetsState(null, null);
            }
        }

        private void OnFocusPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.SetFocusModeEnabled(!binder.IsFocusModeEnabled);
            SyncFocusIndicator();
        }

        private void OnBulkLockPerformed(InputAction.CallbackContext context)
        {
            if (!CanBulkLock || binder == null)
            {
                return;
            }

            if (cameraRig != null && cameraRig.CurrentMode != TargetingCameraRig.CameraMode.MultiLock)
            {
                if (!cameraRig.CanTransitionTo(TargetingCameraRig.CameraMode.MultiLock))
                {
                    return;
                }

                cameraRig.SetMode(TargetingCameraRig.CameraMode.MultiLock);
            }

            binder.LockAllVisibleTargets();
        }

        private void OnTargetLooked(ITargetable target)
        {
            SyncIndicatorState();
        }

        private void OnTargetUnlooked(ITargetable target)
        {
            SyncIndicatorState();
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

        private void SyncFocusIndicator()
        {
            SyncIndicatorState();
        }

        private void SyncIndicatorState()
        {
            if (indicatorController == null || binder == null)
            {
                return;
            }

            indicatorController.SetTargetsState(
                binder.LockedTargets,
                binder.IsFocusModeEnabled ? binder.CurrentFocusTarget : null);
        }

        private InputActionBinding lockBinding;
        private InputActionBinding nextBinding;
        private InputActionBinding previousBinding;
        private InputActionBinding clearBinding;
        private InputActionBinding focusBinding;
        private InputActionBinding bulkBinding;
    }
}
