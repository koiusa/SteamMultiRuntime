using Koiusa.Input;
using UnityEngine;

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

        [Header("Input")]
        [SerializeField] private TargetingInputActionsConfig inputActionsConfig;

        private bool isActive;
        private InputActionLease lookLease;

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

            SetLookActive(false);
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
            SetLookActive(isActive);
        }

        private void SetLookActive(bool active)
        {
            if (active)
            {
                lookLease ??= InputActionLease.Acquire(inputActionsConfig?.LookAction);
                return;
            }

            lookLease?.Dispose();
            lookLease = null;
        }
    }
}
