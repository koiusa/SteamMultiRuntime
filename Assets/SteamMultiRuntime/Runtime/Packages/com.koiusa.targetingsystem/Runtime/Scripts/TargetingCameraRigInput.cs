using Koiusa.Input;
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
        [SerializeField] private TargetingInputActionsProfile inputProfile;

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

            noLockBinding = InputActionBinding.Bind(inputProfile?.ClearLockAction, OnNoLockPerformed);
            soloLockBinding = InputActionBinding.Bind(inputProfile?.SoloLockAction, OnSoloLockPerformed);
            multiLockBinding = InputActionBinding.Bind(inputProfile?.MultiLockAction, OnMultiLockPerformed);

            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            multiLockBinding?.Dispose();
            soloLockBinding?.Dispose();
            noLockBinding?.Dispose();
            multiLockBinding = null;
            soloLockBinding = null;
            noLockBinding = null;

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

            if (!cameraRig.CanTransitionTo(TargetingCameraRig.CameraMode.SoloLock))
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

            if (!cameraRig.CanTransitionTo(TargetingCameraRig.CameraMode.MultiLock))
            {
                return;
            }

            cameraRig.SetMode(TargetingCameraRig.CameraMode.MultiLock);
        }

        private InputActionBinding noLockBinding;
        private InputActionBinding soloLockBinding;
        private InputActionBinding multiLockBinding;
    }
}
