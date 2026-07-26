using Koiusa.Input;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [RequireComponent(typeof(PlayerCompositeMotor))]
    public class LocalPlayerController : MonoBehaviour, IPlayerController
    {
        [Header("Input")]
        [SerializeField] private InputActionsConfig inputActionsConfig;

        [Header("References")]
        [SerializeField] private Transform cameraTransform;

        private Rigidbody targetRigidbody;
        private PlayerGameplayInputReader inputSource;
        private PlayerCompositeMotor motor;
        private PhysicsPresentationSmoother presentationSmoother;
        private IPlayerMoveInputReceiver moveInputReceiver;
        private IPlayerTraversalCoordinator traversalCoordinator;
        private Vector3 moveDirection;
        private Vector2 moveInput;
        private int jumpToken;
        private int lastConsumedJumpToken;
        private bool isStrafeMode;
        private bool grappleHeld;
        private float reelInput;
        private Vector3 grappleAimDirection;

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
        public InputActionsConfig InputActionsConfig => inputActionsConfig;

        public void SetInputConfig(InputActionsConfig config)
        {
            if (config == null)
            {
                Debug.LogError("InputProfile cannot be null.", this);
                return;
            }

            inputActionsConfig = config;

            // Reinitialize input source if already created
            if (inputSource != null)
            {
                inputSource.Disable();
            }

            inputSource = new PlayerGameplayInputReader(config);

            if (isActiveAndEnabled)
            {
                inputSource.Enable();
            }
        }

        private void Awake()
        {
            targetRigidbody = GetComponent<Rigidbody>();
            targetRigidbody.freezeRotation = true;
            targetRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

            presentationSmoother = GetComponent<PhysicsPresentationSmoother>();
            if (presentationSmoother == null)
            {
                presentationSmoother = gameObject.AddComponent<PhysicsPresentationSmoother>();
            }
            presentationSmoother.Initialize(targetRigidbody);

            motor = GetComponent<PlayerCompositeMotor>();
            if (motor == null)
            {
                motor = gameObject.AddComponent<PlayerCompositeMotor>();
            }

            moveInputReceiver = motor as IPlayerMoveInputReceiver;
            traversalCoordinator = GetComponent<IPlayerTraversalCoordinator>();

            if (inputActionsConfig == null)
            {
                Debug.LogError("Gameplay InputActionsConfig is not assigned.", this);
                enabled = false;
                return;
            }

            inputSource = new PlayerGameplayInputReader(inputActionsConfig);

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
            grappleAimDirection = PlayerPointerAim.ResolveDirection(
                cameraTransform,
                referenceTransform,
                targetRigidbody.worldCenterOfMass,
                targetRigidbody);

            if (inputState.JumpPressed)
            {
                jumpToken++;
            }

            isStrafeMode = inputState.IsStrafeMode;
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

            motor.SetStrafeMode(isStrafeMode);

            traversalCoordinator?.SetWireInput(
                grappleHeld,
                reelInput,
                targetRigidbody.worldCenterOfMass,
                grappleAimDirection);

            moveInputReceiver?.SetMoveInput(moveInput);
            moveInputReceiver?.SetMoveReferenceRotation(cameraTransform != null ? cameraTransform.rotation : transform.rotation);
            motor.Tick(moveDirection, jumpThisFrame);
            presentationSmoother?.CapturePhysicsPose();
        }

    }
}
