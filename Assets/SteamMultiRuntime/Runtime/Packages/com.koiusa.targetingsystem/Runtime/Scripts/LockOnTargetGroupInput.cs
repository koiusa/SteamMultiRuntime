using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    [RequireComponent(typeof(LockOnTargetGroupBinder))]
    [DisallowMultipleComponent]
    public sealed class LockOnTargetGroupInput : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference lockAction;
        [SerializeField] private InputActionReference nextTargetAction;
        [SerializeField] private InputActionReference prevTargetAction;
        [SerializeField] private InputActionReference unlockAllAction;
        [SerializeField] private InputActionReference focusAction;
        [SerializeField] private InputActionReference bulkLockAction;

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
            cameraRig.CurrentMode == TargetingCameraRig.CameraMode.NoLock;

        private void OnEnable()
        {
            if (isBound)
            {
                return;
            }

            BindAction(lockAction, OnLockPerformed);
            BindAction(nextTargetAction, OnNextTargetPerformed);
            BindAction(prevTargetAction, OnPrevTargetPerformed);
            BindAction(unlockAllAction, OnUnlockAllPerformed);
            BindAction(focusAction, OnFocusPerformed);
            BindAction(bulkLockAction, OnBulkLockPerformed);

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

            UnbindAction(bulkLockAction, OnBulkLockPerformed);
            UnbindAction(focusAction, OnFocusPerformed);
            UnbindAction(unlockAllAction, OnUnlockAllPerformed);
            UnbindAction(prevTargetAction, OnPrevTargetPerformed);
            UnbindAction(nextTargetAction, OnNextTargetPerformed);
            UnbindAction(lockAction, OnLockPerformed);

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
        }

        private void OnPrevTargetPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.SelectPrev();
        }

        private void OnUnlockAllPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.UnlockAllTargets();

            if (indicatorController != null)
            {
                indicatorController.ClearLockedTargets();
                indicatorController.SetFocusTarget(null);
            }
        }

        private void OnFocusPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.SetFocusModeEnabled(!binder.IsFocusModeEnabled);
        }

        private void OnBulkLockPerformed(InputAction.CallbackContext context)
        {
            if (!CanBulkLock || binder == null)
            {
                return;
            }

            if (cameraRig != null && cameraRig.CurrentMode == TargetingCameraRig.CameraMode.NoLock)
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
            if (indicatorController == null)
            {
                return;
            }

            indicatorController.SetTargetLocked(target, true);

            if (binder.IsFocusModeEnabled)
            {
                indicatorController.SetFocusTarget(target);
            }
        }

        private void OnTargetUnlooked(ITargetable target)
        {
            if (indicatorController == null)
            {
                return;
            }

            indicatorController.SetTargetLocked(target, false);

            if (ReferenceEquals(indicatorController.CurrentFocusTarget, target))
            {
                ITargetable nextFocus = null;
                foreach (var t in binder.LockedTargets)
                {
                    if (t != null && !ReferenceEquals(t, target))
                    {
                        nextFocus = t;
                        break;
                    }
                }

                indicatorController.SetFocusTarget(nextFocus);
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

        private static void BindAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
        {
            if (actionReference == null || actionReference.action == null)
            {
                return;
            }

            actionReference.action.Enable();
            actionReference.action.performed += callback;
        }

        private static void UnbindAction(InputActionReference actionReference, System.Action<InputAction.CallbackContext> callback)
        {
            if (actionReference == null || actionReference.action == null)
            {
                return;
            }

            actionReference.action.performed -= callback;
            actionReference.action.Disable();
        }
    }
}
