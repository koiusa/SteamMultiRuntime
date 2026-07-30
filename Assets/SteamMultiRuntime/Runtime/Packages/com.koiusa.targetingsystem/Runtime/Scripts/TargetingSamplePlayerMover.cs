using Koiusa.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.TargetingSystem.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class TargetingSamplePlayerMover : MonoBehaviour
    {
        [SerializeField] private InputActionsConfig inputActionsConfig;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float rotationSpeed = 12f;
        [SerializeField, Min(0f)] private float gravity = 20f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;

        private CharacterController characterController;
        private InputAction moveAction;
        private InputAction jumpAction;
        private Vector2 moveInput;
        private bool jumpRequested;
        private float verticalSpeed;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            moveAction = inputActionsConfig?.FindAction("Player/Move");
            jumpAction = inputActionsConfig?.FindAction("Player/Jump");
        }

        private void OnEnable()
        {
            SubscribeVector2(moveAction, OnMove);
            if (jumpAction != null)
            {
                jumpAction.performed += OnJump;
                jumpAction.Enable();
            }
        }

        private void OnDisable()
        {
            UnsubscribeVector2(moveAction, OnMove);
            if (jumpAction != null)
            {
                jumpAction.performed -= OnJump;
                jumpAction.Disable();
            }
            moveInput = Vector2.zero;
            jumpRequested = false;
        }

        private void Update()
        {
            var cameraTransform = viewCamera != null ? viewCamera.transform : null;
            var forward = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
            var right = cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized : Vector3.right;
            var direction = Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up),
                    1f - Mathf.Exp(-rotationSpeed * Time.deltaTime));
            }

            if (characterController.isGrounded)
            {
                verticalSpeed = jumpRequested ? Mathf.Sqrt(2f * gravity * jumpHeight) : -1f;
            }
            else
            {
                verticalSpeed -= gravity * Time.deltaTime;
            }

            jumpRequested = false;
            characterController.Move((direction * moveSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        private void OnMove(InputAction.CallbackContext context) => moveInput = context.ReadValue<Vector2>();
        private void OnJump(InputAction.CallbackContext context) => jumpRequested = true;

        private static void SubscribeVector2(InputAction action, System.Action<InputAction.CallbackContext> callback)
        {
            if (action == null)
            {
                return;
            }

            action.performed += callback;
            action.canceled += callback;
            action.Enable();
        }

        private static void UnsubscribeVector2(InputAction action, System.Action<InputAction.CallbackContext> callback)
        {
            if (action == null)
            {
                return;
            }

            action.performed -= callback;
            action.canceled -= callback;
            action.Disable();
        }
    }
}
