using UnityEngine;
using UnityEngine.InputSystem;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [RequireComponent(typeof(PlayerCompositeMotor))]
    public class LocalPlayerController : MonoBehaviour, IPlayerController
    {
        private sealed class InputActionPlayerInputSource : IPlayerInputSource
        {
            private readonly InputActionReference moveAction;
            private readonly InputActionReference jumpAction;
            private bool jumpRequested;
            private bool isEnabled;

            public InputActionPlayerInputSource(InputActionReference moveAction, InputActionReference jumpAction)
            {
                this.moveAction = moveAction;
                this.jumpAction = jumpAction;
            }

            public void Enable()
            {
                if (isEnabled) return;
                isEnabled = true;

                if (moveAction != null)
                {
                    moveAction.action.Enable();
                }

                if (jumpAction != null)
                {
                    jumpAction.action.Enable();
                    jumpAction.action.performed += OnJumpPerformed;
                }
            }

            public void Disable()
            {
                if (isEnabled) return;
                isEnabled = false;

                if (jumpAction != null)
                {
                    jumpAction.action.performed -= OnJumpPerformed;
                    jumpAction.action.Disable();
                }

                if (moveAction != null)
                {
                    moveAction.action.Disable();
                }

                jumpRequested = false;
            }

            public PlayerInputState ReadState()
            {
                var move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
                var jumpPressed = jumpRequested;
                jumpRequested = false;
                return new PlayerInputState(move, jumpPressed);
            }

            private void OnJumpPerformed(InputAction.CallbackContext context)
            {
                jumpRequested = true;
            }
        }

        [Header("Input")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference jumpAction;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        private IPlayerInputSource inputSource;
        private PlayerCompositeMotor motor;
        private Vector2 moveInput;
        private bool jumpRequested;

        public bool IsGrounded => motor != null && motor.IsGrounded;
        public bool IsJumping => motor != null && motor.IsJumping;
        public bool IsFreefall => motor != null && motor.IsFreefall;
        public bool IsFallingAfterJump => motor != null && motor.IsFallingAfterJump;
        public Vector3 InheritedGroundVelocity => motor != null ? motor.InheritedGroundVelocity : Vector3.zero;
        public float HorizontalVelocity => motor != null ? motor.HorizontalVelocity : 0f;
        public float VerticalVelocity => motor != null ? motor.VerticalVelocity : 0f;
        public float MaxMoveSpeed => 5f;

        private void Awake()
        {
            motor = GetComponent<PlayerCompositeMotor>();
            if (motor == null)
            {
                motor = gameObject.AddComponent<PlayerCompositeMotor>();
            }

            inputSource = new InputActionPlayerInputSource(moveAction, jumpAction);

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            inputSource?.Enable();
        }

        private void OnDisable()
        {
            inputSource?.Disable();
            motor?.ResetState();
            moveInput = Vector2.zero;
            jumpRequested = false;
        }

        private void Update()
        {
            if (inputSource == null)
            {
                moveInput = Vector2.zero;
                return;
            }

            var inputState = inputSource.ReadState();
            moveInput = inputState.Move;
            if (inputState.JumpPressed)
            {
                jumpRequested = true;
            }
        }

        private void FixedUpdate()
        {
            if (motor == null)
            {
                return;
            }

            Transform referenceTransform = cameraTransform != null ? cameraTransform : transform;
            var moveDirection = PlayerMotor.GetMoveDirection(referenceTransform, moveInput);
            motor.Tick(moveDirection, jumpRequested);
            jumpRequested = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            motor?.OnCollisionEnter(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            motor?.OnCollisionStay(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            motor?.OnCollisionExit(collision);
        }
    }
}
