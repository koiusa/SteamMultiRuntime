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
        [SerializeField] private InputActionReference unlockAction;
        [SerializeField] private InputActionReference unlockAllAction;

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
            BindAction(unlockAction, OnUnlockPerformed);
            BindAction(unlockAllAction, OnUnlockAllPerformed);
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            UnbindAction(unlockAllAction, OnUnlockAllPerformed);
            UnbindAction(unlockAction, OnUnlockPerformed);
            UnbindAction(lockAction, OnLockPerformed);
            isBound = false;
        }

        private void OnLockPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.LockClosestVisibleTarget();
        }

        private void OnUnlockPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.UnlockLastLockedTarget();
        }

        private void OnUnlockAllPerformed(InputAction.CallbackContext context)
        {
            if (!IsMultiLockMode || binder == null) return;
            binder.UnlockAllTargets();
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
