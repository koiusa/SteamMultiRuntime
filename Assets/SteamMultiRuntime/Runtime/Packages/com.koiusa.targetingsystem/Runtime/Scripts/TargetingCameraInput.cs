using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    /// <summary>
    /// Handles per-camera input actions that are only active while the camera is the current mode.
    /// Attach to each individual VCam GameObject (NoLock / SoloLock / MultiLock).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TargetingCameraInput : MonoBehaviour
    {
        [Header("Camera Identity")]
        [Tooltip("Which mode this camera corresponds to.")]
        [SerializeField] private TargetingCameraRig.CameraMode cameraMode;

        [Header("TargetingCameraRig")]
        [SerializeField] private TargetingCameraRig cameraRig;

        [Header("Camera-specific Input")]
        [SerializeField] private InputActionReference[] cameraActions;

        private bool isActive;

        private void OnEnable()
        {
            if (cameraRig != null)
            {
                cameraRig.OnModeChanged += OnRigModeChanged;
                RefreshActiveState(cameraRig.CurrentMode);
            }
        }

        private void OnDisable()
        {
            if (cameraRig != null)
            {
                cameraRig.OnModeChanged -= OnRigModeChanged;
            }

            SetActionsActive(false);
        }

        private void OnRigModeChanged(TargetingCameraRig.CameraMode mode)
        {
            RefreshActiveState(mode);
        }

        private void RefreshActiveState(TargetingCameraRig.CameraMode mode)
        {
            var shouldBeActive = mode == cameraMode;
            if (isActive == shouldBeActive)
            {
                return;
            }

            isActive = shouldBeActive;
            SetActionsActive(isActive);
        }

        private void SetActionsActive(bool active)
        {
            if (cameraActions == null)
            {
                return;
            }

            foreach (var actionRef in cameraActions)
            {
                if (actionRef == null || actionRef.action == null)
                {
                    continue;
                }

                if (active)
                {
                    actionRef.action.Enable();
                }
                else
                {
                    actionRef.action.Disable();
                }
            }
        }
    }
}
