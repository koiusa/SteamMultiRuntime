using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [DisallowMultipleComponent]
    public sealed class ActorMotor : MonoBehaviour, IActorMotor
    {
        [SerializeField] private ActorMotorSettings settings;

        private Rigidbody rb;
        private Collider bodyCollider;
        private GroundMotionTracker groundMotionTracker;
        private SlopeContactResolver slopeContactResolver;
        private ActorMotorGrounding grounding;
        private IActorTraversalCoordinator traversalCoordinator;

        private float jumpDetachUntilTime;
        private Vector3 inheritedGroundVelocity;
        private bool isAirborneFromJump;
        private bool forcedStrafeMode;
        private ActorFacingRequest facingRequest;

        [SerializeField]
        [HideInInspector]
        private ActorMotorSettings initialSettings;
        private bool wasInitialized;

        public bool IsGrounded { get; private set; }
        public bool IsEnabled => isActiveAndEnabled;
        public bool IsAirborneFromJump => isAirborneFromJump;
        public bool IsJumping => !IsGrounded && isAirborneFromJump && VerticalVelocity > 0f;
        public bool IsFallingAfterJump => !IsGrounded && isAirborneFromJump && VerticalVelocity <= 0f;
        public bool IsFreefall => !IsGrounded && !isAirborneFromJump;
        private bool IsWireSwinging => traversalCoordinator != null
            && traversalCoordinator.IsEnabled
            && traversalCoordinator.IsWireAttached
            && !traversalCoordinator.IsWireGroundActionActive;
        public Vector3 InheritedGroundVelocity => inheritedGroundVelocity;
        public float HorizontalVelocity { get; private set; }
        public float VerticalVelocity { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();
            groundMotionTracker = GetComponent<GroundMotionTracker>();
            slopeContactResolver = GetComponent<SlopeContactResolver>();
            grounding = new ActorMotorGrounding();
            traversalCoordinator = GetComponent<IActorTraversalCoordinator>();

            if (IsSettingsEmpty(settings))
            {
                settings = ActorMotorSettings.CreateDefault();
            }

            if (!wasInitialized)
            {
                initialSettings = settings;
                wasInitialized = true;
            }
        }

        private static bool IsSettingsEmpty(ActorMotorSettings s)
        {
            return s.MoveSpeed == 0f && s.GroundAcceleration == 0f && s.JumpForce == 0f;
        }

        private void OnValidate()
        {
            if (IsSettingsEmpty(settings))
            {
                settings = ActorMotorSettings.CreateDefault();
            }
        }

        private void OnDestroy()
        {
            if (wasInitialized && Application.isEditor && !Application.isPlaying)
            {
                settings = initialSettings;
            }
        }

        private void OnDisable()
        {
            if (wasInitialized && Application.isEditor && !Application.isPlaying)
            {
                settings = initialSettings;
            }
        }

        public void ResetState()
        {
            slopeContactResolver.Clear();
            groundMotionTracker.ClearGroundContacts();
            grounding?.ResetState();
            jumpDetachUntilTime = 0f;
            inheritedGroundVelocity = Vector3.zero;
            facingRequest = default;
            isAirborneFromJump = false;
            IsGrounded = true;
            HorizontalVelocity = 0f;
            VerticalVelocity = 0f;
        }

        public void SetStrafeMode(bool enabled)
        {
            forcedStrafeMode = enabled;
        }

        public void SetFacingRequest(ActorFacingRequest request)
        {
            facingRequest = request;
        }

        public ActorMotorSettings GetSettings() => settings;

        public void ApplySettings(ActorMotorSettings newSettings)
        {
            settings = newSettings;
        }

        public ActorMotorTickResult Tick(Vector3 moveDirection, bool jumpRequested)
        {
            if (!IsEnabled)
            {
                return new ActorMotorTickResult(false);
            }

            if (rb == null || rb.isKinematic)
            {
                IsGrounded = true;
                isAirborneFromJump = false;
                HorizontalVelocity = 0f;
                VerticalVelocity = 0f;
                return new ActorMotorTickResult(false);
            }

            var upAxis = GetUpAxis();
            var velocity = rb.linearVelocity;
            var wireGroundStrafeBlend = traversalCoordinator != null
                ? traversalCoordinator.WireGroundStrafeBlend
                : 0f;
            var effectiveStrafeBlend = forcedStrafeMode ? 1f : wireGroundStrafeBlend;
            var isWireSwinging = IsWireSwinging;
            var jumpConsumed = traversalCoordinator != null
                && traversalCoordinator.ProcessMotorInput(moveDirection, jumpRequested, slopeContactResolver.IsGrounded);
            if (jumpConsumed)
            {
                jumpRequested = false;
            }
            isWireSwinging = IsWireSwinging;

            var canUseGroundContacts = Time.time >= jumpDetachUntilTime;
            var hasGroundContact = !isWireSwinging && canUseGroundContacts && slopeContactResolver.IsGrounded;
            var isGrounded = !isWireSwinging && grounding != null && grounding.ResolveGroundedState(
                canUseGroundContacts,
                hasGroundContact,
                isAirborneFromJump,
                velocity,
                upAxis,
                rb,
                bodyCollider,
                settings);

            var isOnSteepSlope = canUseGroundContacts && !isGrounded && slopeContactResolver.IsOnSteepSlope;
            var canJump = isGrounded || (isOnSteepSlope && slopeContactResolver.CanJumpOnSteepSlope);
            var groundVelocity = Vector3.zero;
            var groundDisplacement = Vector3.zero;
            var groundRotationDelta = Quaternion.identity;

            IsGrounded = isGrounded;
            if (isGrounded)
            {
                isAirborneFromJump = false;
            }

            if (isGrounded)
            {
                inheritedGroundVelocity = Vector3.zero;
                groundMotionTracker.TryGetGroundMotion(rb.position, out groundVelocity, out groundDisplacement, out groundRotationDelta);
                rb.MovePosition(rb.position + groundDisplacement);
                velocity = ActorMotorMovementLogic.AccelerateOnGround(
                    velocity,
                    moveDirection,
                    upAxis,
                    slopeContactResolver,
                    settings,
                    effectiveStrafeBlend);
                velocity = ActorMotorMovementLogic.ApplyGroundStepAssist(
                    velocity,
                    moveDirection,
                    upAxis,
                    rb,
                    bodyCollider,
                    slopeContactResolver,
                    settings,
                    effectiveStrafeBlend);
            }
            else if (isOnSteepSlope)
            {
                inheritedGroundVelocity = Vector3.zero;
                if (slopeContactResolver.CanJumpOnSteepSlope)
                {
                    groundMotionTracker.TryGetGroundMotion(rb.position, out groundVelocity, out _, out _);
                }

                velocity = ActorMotorMovementLogic.AccelerateOnSteepSlope(
                    velocity,
                    moveDirection,
                    upAxis,
                    slopeContactResolver,
                    settings,
                    effectiveStrafeBlend);
            }
            else if (!isWireSwinging
                && (traversalCoordinator == null || !traversalCoordinator.IsTraversalActive))
            {
                velocity = ActorMotorMovementLogic.AccelerateInAir(
                    velocity,
                    moveDirection,
                    upAxis,
                    inheritedGroundVelocity,
                    slopeContactResolver,
                    settings,
                    effectiveStrafeBlend);
            }

            var preserveWallRunFacing = traversalCoordinator != null
                && traversalCoordinator.IsEnabled
                && traversalCoordinator.CurrentState == ActorTraversalState.WallRun;
            if (!preserveWallRunFacing)
            {
                var normalRotation = ActorMotorMovementLogic.CalculateRotation(
                    rb.rotation,
                    moveDirection,
                    upAxis,
                    groundRotationDelta,
                    settings,
                    effectiveStrafeBlend);
                var nextRotation = normalRotation;
                var requestedDirection = facingRequest.IsValid
                    ? Vector3.ProjectOnPlane(facingRequest.Direction, upAxis)
                    : Vector3.zero;
                if (requestedDirection.sqrMagnitude > 0.0001f)
                {
                    var targetRotation = Quaternion.LookRotation(requestedDirection.normalized, upAxis);
                    var rotationSpeed = facingRequest.RotationSpeed > 0f
                        ? facingRequest.RotationSpeed
                        : settings.RotationSpeed;
                    var requestedRotation = rotationSpeed > 0f
                        ? Quaternion.RotateTowards(
                            rb.rotation,
                            targetRotation,
                            rotationSpeed * Time.fixedDeltaTime)
                        : rb.rotation;
                    nextRotation = Quaternion.Slerp(normalRotation, requestedRotation, facingRequest.Blend);
                }
                rb.MoveRotation(nextRotation);
            }

            var jumpResult = ActorMotorJumpLogic.ApplyJumpIfRequested(
                jumpRequested,
                canJump,
                upAxis,
                settings.JumpForce,
                settings.JumpDetachDuration,
                groundVelocity,
                slopeContactResolver,
                groundMotionTracker,
                velocity,
                jumpDetachUntilTime,
                inheritedGroundVelocity,
                isAirborneFromJump);
            jumpConsumed |= jumpRequested && canJump;

            velocity = jumpResult.Velocity;
            jumpDetachUntilTime = jumpResult.JumpDetachUntilTime;
            inheritedGroundVelocity = jumpResult.InheritedGroundVelocity;
            isAirborneFromJump = jumpResult.IsAirborneFromJump;

            velocity = ActorMotorJumpLogic.ApplyExtraFallGravity(
                !isGrounded
                    && !isWireSwinging
                    && (traversalCoordinator == null || !traversalCoordinator.IsTraversalActive),
                isOnSteepSlope,
                upAxis,
                settings.FallMultiplier,
                velocity);

            rb.linearVelocity = velocity;
            HorizontalVelocity = Vector3.ProjectOnPlane(velocity - inheritedGroundVelocity, upAxis).magnitude;
            VerticalVelocity = Vector3.Dot(velocity, upAxis);
            return new ActorMotorTickResult(jumpConsumed);
        }

        public void OnCollisionEnter(Collision collision)
        {
            var upAxis = GetUpAxis();
            var bestUpDot = slopeContactResolver.UpdateCollisionContacts(collision, upAxis, settings.GroundLayer, settings.MinGroundNormalDot);
            groundMotionTracker.UpdateGroundContact(collision, bestUpDot, settings.GroundLayer, settings.MinGroundNormalDot);
        }

        public void OnCollisionStay(Collision collision)
        {
            var upAxis = GetUpAxis();
            var bestUpDot = slopeContactResolver.UpdateCollisionContacts(collision, upAxis, settings.GroundLayer, settings.MinGroundNormalDot);
            groundMotionTracker.UpdateGroundContact(collision, bestUpDot, settings.GroundLayer, settings.MinGroundNormalDot);
        }

        public void OnCollisionExit(Collision collision)
        {
            slopeContactResolver.RemoveCollision(collision.collider);
            groundMotionTracker.RemoveGroundContact(collision.collider);
        }

        public static Vector3 GetUpAxis()
        {
            return Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
        }

        public static Vector3 GetMoveDirection(Transform referenceTransform, Vector2 moveInput)
        {
            var upAxis = GetUpAxis();

            var direction = CalculateRelativeDirection(referenceTransform, moveInput, upAxis);
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        private static Vector3 CalculateRelativeDirection(Transform referenceTransform, Vector2 moveInput, Vector3 upAxis)
        {
            var forward = Vector3.ProjectOnPlane(referenceTransform.forward, upAxis).normalized;
            var right = Vector3.ProjectOnPlane(referenceTransform.right, upAxis).normalized;
            return forward * moveInput.y + right * moveInput.x;
        }
    }
}
