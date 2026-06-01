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
            private readonly InputActionReference strafeToggleAction;
            private int jumpToken;
            private bool isStrafeMode;
            private bool isEnabled;

            public InputActionPlayerInputSource(InputActionReference moveAction, InputActionReference jumpAction, InputActionReference strafeToggleAction)
            {
                this.moveAction = moveAction;
                this.jumpAction = jumpAction;
                this.strafeToggleAction = strafeToggleAction;
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

                if (strafeToggleAction != null)
                {
                    strafeToggleAction.action.Enable();
                    strafeToggleAction.action.performed += OnStrafeTogglePerformed;
                }
            }

            public void Disable()
            {
                if (!isEnabled) return;
                isEnabled = false;

                if (strafeToggleAction != null)
                {
                    strafeToggleAction.action.performed -= OnStrafeTogglePerformed;
                    strafeToggleAction.action.Disable();
                }

                if (jumpAction != null)
                {
                    jumpAction.action.performed -= OnJumpPerformed;
                    jumpAction.action.Disable();
                }

                if (moveAction != null)
                {
                    moveAction.action.Disable();
                }

                jumpToken = 0;
                isStrafeMode = false;
            }

            public PlayerInputState ReadState()
            {
                var move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
                var jumpPressed = jumpToken;
                jumpToken = 0;
                return new PlayerInputState(move, jumpPressed > 0);
            }

            public bool GetStrafeMode() => isStrafeMode;

            private void OnJumpPerformed(InputAction.CallbackContext context)
            {
                jumpToken++;
            }

            private void OnStrafeTogglePerformed(InputAction.CallbackContext context)
            {
                isStrafeMode = !isStrafeMode;
            }
        }

        [Header("Input")]
        [SerializeField] private PlayerInputActionsProfile inputActionsProfile;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        private Rigidbody targetRigidbody;
        private InputActionPlayerInputSource inputSource;
        private PlayerCompositeMotor motor;
        private IPlayerMoveInputReceiver moveInputReceiver;
        private Vector3 moveDirection;
        private Vector2 moveInput;
        private int jumpToken;
        private int lastConsumedJumpToken;
        private bool isStrafeMode;

        public bool IsGrounded => motor != null && motor.IsGrounded;
        public bool IsJumping => motor != null && motor.IsJumping;
        public bool IsFreefall => motor != null && motor.IsFreefall;
        public bool IsFallingAfterJump => motor != null && motor.IsFallingAfterJump;
        public bool IsStrafeMode => isStrafeMode;
        public Vector3 InheritedGroundVelocity => motor != null ? motor.InheritedGroundVelocity : Vector3.zero;
        public Vector2 MoveInput => moveInput;
        public Vector3 MoveDirection => moveDirection;
        public float HorizontalVelocity => motor != null ? motor.HorizontalVelocity : 0f;
        public float VerticalVelocity => motor != null ? motor.VerticalVelocity : 0f;
        public float MaxMoveSpeed => 5f;

        public void SetInputProfile(PlayerInputActionsProfile profile)
        {
            if (profile == null)
            {
                Debug.LogError("InputProfile cannot be null.", this);
                return;
            }

            inputActionsProfile = profile;

            // Reinitialize input source if already created
            if (inputSource != null)
            {
                inputSource.Disable();
            }

            inputSource = new InputActionPlayerInputSource(
                profile.MoveAction,
                profile.JumpAction,
                profile.StrafeToggleAction);

            if (isActiveAndEnabled)
            {
                inputSource.Enable();
            }
        }

        private void Awake()
        {
            targetRigidbody = GetComponent<Rigidbody>();
            targetRigidbody.freezeRotation = true;

            motor = GetComponent<PlayerCompositeMotor>();
            if (motor == null)
            {
                motor = gameObject.AddComponent<PlayerCompositeMotor>();
            }

            moveInputReceiver = motor as IPlayerMoveInputReceiver;

            if (inputActionsProfile == null)
            {
                Debug.LogError("PlayerInputActionsProfile is not assigned.", this);
                enabled = false;
                return;
            }

            inputSource = new InputActionPlayerInputSource(
                inputActionsProfile.MoveAction,
                inputActionsProfile.JumpAction,
                inputActionsProfile.StrafeToggleAction);

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
            moveDirection = Vector3.zero;
            moveInput = Vector2.zero;
            jumpToken = 0;
            lastConsumedJumpToken = 0;
            isStrafeMode = false;
        }

        private void Update()
        {
            if (inputSource == null)
            {
                moveDirection = Vector3.zero;
                return;
            }

            var inputState = inputSource.ReadState();
            moveInput = inputState.Move;
            Transform referenceTransform = cameraTransform != null ? cameraTransform : transform;
            moveDirection = PlayerMotor.GetMoveDirection(referenceTransform, inputState.Move);

            if (inputState.JumpPressed)
            {
                jumpToken++;
            }

            isStrafeMode = inputSource.GetStrafeMode();
        }

        private void FixedUpdate()
        {
            if (motor == null)
            {
                return;
            }

            var jumpThisFrame = jumpToken != lastConsumedJumpToken;
            if (jumpThisFrame)
            {
                lastConsumedJumpToken = jumpToken;
            }

            var baseMotor = motor.GetComponent<IPlayerMotor>();
            if (baseMotor != null)
            {
                baseMotor.SetStrafeMode(isStrafeMode);
            }

            moveInputReceiver?.SetMoveInput(moveInput);

            motor.Tick(moveDirection, jumpThisFrame);
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
