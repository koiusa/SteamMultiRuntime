using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// Switches TargetingCameraRig mode via separate InputSystem buttons.
    /// Attach to the same GameObject as TargetingCameraRig.
    /// </summary>
    [RequireComponent(typeof(TargetingCameraRig))]
    [DisallowMultipleComponent]
    public sealed class TargetingCameraRigInput : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference noLockAction;
        [SerializeField] private InputActionReference soloLockAction;
        [SerializeField] private InputActionReference multiLockAction;

        private TargetingCameraRig cameraRig;
        private bool isBound;

        private void Awake()
        {
            cameraRig = GetComponent<TargetingCameraRig>();
        }

        private void OnEnable()
        {
            if (isBound)
            {
                return;
            }

            BindAction(noLockAction, OnNoLockPerformed);
            BindAction(soloLockAction, OnSoloLockPerformed);
            BindAction(multiLockAction, OnMultiLockPerformed);

            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            UnbindAction(multiLockAction, OnMultiLockPerformed);
            UnbindAction(soloLockAction, OnSoloLockPerformed);
            UnbindAction(noLockAction, OnNoLockPerformed);

            isBound = false;
        }

        private void OnNoLockPerformed(InputAction.CallbackContext context)
        {
            if (cameraRig == null)
            {
                return;
            }

            cameraRig.SetMode(TargetingCameraRig.CameraMode.NoLock);
        }

        private void OnSoloLockPerformed(InputAction.CallbackContext context)
        {
            if (cameraRig == null)
            {
                return;
            }

            cameraRig.SetMode(TargetingCameraRig.CameraMode.SoloLock);
        }

        private void OnMultiLockPerformed(InputAction.CallbackContext context)
        {
            if (cameraRig == null)
            {
                return;
            }

            cameraRig.SetMode(TargetingCameraRig.CameraMode.MultiLock);
        }

        private static void BindAction(InputActionReference actionRef, System.Action<InputAction.CallbackContext> callback)
        {
            if (actionRef == null || actionRef.action == null)
            {
                return;
            }

            actionRef.action.Enable();
            actionRef.action.performed += callback;
        }

        private static void UnbindAction(InputActionReference actionRef, System.Action<InputAction.CallbackContext> callback)
        {
            if (actionRef == null || actionRef.action == null)
            {
                return;
            }

            actionRef.action.performed -= callback;
            actionRef.action.Disable();
        }
    }
}
