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
            private readonly InputAction grappleAction;
            private readonly InputAction reelAction;
            private int jumpToken;
            private bool isStrafeMode;
            private bool isEnabled;

            public InputActionPlayerInputSource(InputActionReference moveAction, InputActionReference jumpAction, InputActionReference strafeToggleAction, InputAction grappleAction, InputAction reelAction)
            {
                this.moveAction = moveAction;
                this.jumpAction = jumpAction;
                this.strafeToggleAction = strafeToggleAction;
                this.grappleAction = grappleAction;
                this.reelAction = reelAction;
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

                grappleAction?.Enable();
                reelAction?.Enable();
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

                grappleAction?.Disable();
                reelAction?.Disable();

                jumpToken = 0;
                isStrafeMode = false;
            }

            public PlayerInputState ReadState()
            {
                var move = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;
                var jumpPressed = jumpToken;
                jumpToken = 0;
                var grappleHeld = grappleAction != null && grappleAction.IsPressed();
                var reelInput = reelAction != null ? reelAction.ReadValue<float>() : 0f;
                return new PlayerInputState(move, jumpPressed > 0, grappleHeld, reelInput);
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
        private IWireSwingTraversalFeature wireSwingFeature;
        private Vector3 moveDirection;
        private Vector2 moveInput;
        private int jumpToken;
        private int lastConsumedJumpToken;
        private bool isStrafeMode;
        private bool grappleHeld;
        private float reelInput;
        private Vector3 grappleAimDirection;
        private bool blockGrappleUntilRelease;

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
                profile.StrafeToggleAction,
                profile.GrappleInputAction,
                profile.ReelInputAction);

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
            wireSwingFeature = GetComponent<IWireSwingTraversalFeature>();

            if (inputActionsProfile == null)
            {
                Debug.LogError("PlayerInputActionsProfile is not assigned.", this);
                enabled = false;
                return;
            }

            inputSource = new InputActionPlayerInputSource(
                inputActionsProfile.MoveAction,
                inputActionsProfile.JumpAction,
                inputActionsProfile.StrafeToggleAction,
                inputActionsProfile.GrappleInputAction,
                inputActionsProfile.ReelInputAction);

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
            grappleHeld = false;
            reelInput = 0f;
            grappleAimDirection = Vector3.zero;
            blockGrappleUntilRelease = false;
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
            grappleHeld = inputState.GrappleHeld;
            reelInput = inputState.ReelInput;
            grappleAimDirection = referenceTransform.forward;

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

            if (wireSwingFeature != null && wireSwingFeature.IsEnabled)
            {
                wireSwingFeature.SetReelInput(reelInput);
                if (!grappleHeld)
                {
                    blockGrappleUntilRelease = false;
                    wireSwingFeature.SetGrappleInput(false, targetRigidbody.worldCenterOfMass, grappleAimDirection);
                }
                else if (!blockGrappleUntilRelease)
                {
                    wireSwingFeature.SetGrappleInput(true, targetRigidbody.worldCenterOfMass, grappleAimDirection);
                }
            }

            moveInputReceiver?.SetMoveInput(moveInput);
            moveInputReceiver?.SetMoveReferenceRotation(cameraTransform != null ? cameraTransform.rotation : transform.rotation);
            motor.Tick(moveDirection, jumpThisFrame);
            if (jumpThisFrame && grappleHeld)
            {
                blockGrappleUntilRelease = true;
            }
        }

    }
}
