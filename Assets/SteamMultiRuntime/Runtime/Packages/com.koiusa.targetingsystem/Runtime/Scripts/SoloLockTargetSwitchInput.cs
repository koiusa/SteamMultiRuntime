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
        [SerializeField] private InputActionReference targetSwitchAction;

        private bool isBound;

        private void OnEnable()
        {
            if (isBound)
            {
                return;
            }

            BindAction(targetSwitchAction, OnTargetSwitchPerformed);
            isBound = true;
        }

        private void OnDisable()
        {
            if (!isBound)
            {
                return;
            }

            UnbindAction(targetSwitchAction, OnTargetSwitchPerformed);
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
