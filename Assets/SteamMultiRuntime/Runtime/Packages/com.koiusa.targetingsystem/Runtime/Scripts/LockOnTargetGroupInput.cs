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

        private ILockOnTargetBinder binder;
        private TargetingCameraRig cameraRig;
        private bool isBound;

        private void Awake()
        {
            binder = GetComponent<ILockOnTargetBinder>();
            cameraRig = GetComponentInParent<TargetingCameraRig>();
        }

        private bool IsMultiLockMode =>
            cameraRig == null || cameraRig.CurrentMode == TargetingCameraRig.CameraMode.MultiLock;

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
        }

        private void OnFocusPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.SetFocusModeEnabled(!binder.IsFocusModeEnabled);
        }

        private void OnBulkLockPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.LockAllVisibleTargets();
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
