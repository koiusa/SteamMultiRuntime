using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SoloLockTargetSwitchInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject binderObject;
        [SerializeField] private GameObject cameraRigObject;

        [Header("Input")]
        [SerializeField] private TargetingInputActionsConfig inputActionsConfig;

        private bool isBound;

        private void OnEnable()
        {
            if (isBound)
            {
                return;
            }

            targetSwitchBinding = InputActionBinding.Bind(inputActionsConfig?.SoloLockAction, OnTargetSwitchPerformed);
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            targetSwitchBinding?.Dispose();
            targetSwitchBinding = null;
            isBound = false;
        }

        private void OnTargetSwitchPerformed(InputAction.CallbackContext context)
        {
            if (cameraRigObject == null || binderObject == null)
            {
                return;
            }

            var rig = cameraRigObject.GetComponent<TargetingCameraRig>();
            if (rig == null || rig.CurrentMode != TargetingCameraRig.CameraMode.SoloLock)
            {
                return;
            }

            binderObject.SendMessage("ToggleClosestVisibleTarget", SendMessageOptions.DontRequireReceiver);
        }

        private InputActionBinding targetSwitchBinding;
    }
}
