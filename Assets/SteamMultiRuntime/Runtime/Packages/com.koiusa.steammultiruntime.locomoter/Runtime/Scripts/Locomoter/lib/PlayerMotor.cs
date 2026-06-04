using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [DisallowMultipleComponent]
    public sealed class PlayerMotor : MonoBehaviour, IPlayerMotor
    {
        [SerializeField] private PlayerMotorSettings settings;

        private Rigidbody rb;
        private Collider bodyCollider;
        private GroundMotionTracker groundMotionTracker;
        private SlopeContactResolver slopeContactResolver;
        private PlayerMotorGrounding grounding;

        private float jumpDetachUntilTime;
        private Vector3 inheritedGroundVelocity;
        private bool isAirborneFromJump;
        private bool forcedStrafeMode;

        [SerializeField]
        [HideInInspector]
        private PlayerMotorSettings initialSettings;
        private bool wasInitialized;

        public bool IsGrounded { get; private set; }
        public bool IsAirborneFromJump => isAirborneFromJump;
        public bool IsJumping => !IsGrounded && isAirborneFromJump && VerticalVelocity > 0f;
        public bool IsFallingAfterJump => !IsGrounded && isAirborneFromJump && VerticalVelocity <= 0f;
        public bool IsFreefall => !IsGrounded && !isAirborneFromJump;
        public Vector3 InheritedGroundVelocity => inheritedGroundVelocity;
        public float HorizontalVelocity { get; private set; }
        public float VerticalVelocity { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();
            groundMotionTracker = GetComponent<GroundMotionTracker>();
            slopeContactResolver = GetComponent<SlopeContactResolver>();
            grounding = new PlayerMotorGrounding();

            if (IsSettingsEmpty(settings))
            {
                settings = PlayerMotorSettings.CreateDefault();
            }

            if (!wasInitialized)
            {
                initialSettings = settings;
                wasInitialized = true;
            }
        }

        private static bool IsSettingsEmpty(PlayerMotorSettings s)
        {
            return s.MoveSpeed == 0f && s.GroundAcceleration == 0f && s.JumpForce == 0f;
        }

        private void OnValidate()
        {
            if (IsSettingsEmpty(settings))
            {
                settings = PlayerMotorSettings.CreateDefault();
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
            isAirborneFromJump = false;
            IsGrounded = true;
            HorizontalVelocity = 0f;
            VerticalVelocity = 0f;
        }

        public void SetStrafeMode(bool enabled)
        {
            forcedStrafeMode = enabled;
        }

        public void UpdateSettings(
            float moveSpeed,
            float groundAcceleration,
            float airAcceleration,
            float rotationSpeed,
            float jumpForce,
            float fallMultiplier,
            float jumpDetachDuration,
            float minGroundNormalDot,
            float strafeMoveSpeedMultiplier,
            float strafeAccelerationMultiplier,
            float strafeRotationSpeed,
            float backwardSpeedMultiplier)
        {
            settings.MoveSpeed = moveSpeed;
            settings.GroundAcceleration = groundAcceleration;
            settings.AirAcceleration = airAcceleration;
            settings.RotationSpeed = rotationSpeed;
            settings.JumpForce = jumpForce;
            settings.FallMultiplier = fallMultiplier;
            settings.JumpDetachDuration = jumpDetachDuration;
            settings.MinGroundNormalDot = minGroundNormalDot;
            settings.StrafeMoveSpeedMultiplier = strafeMoveSpeedMultiplier;
            settings.StrafeAccelerationMultiplier = strafeAccelerationMultiplier;
            settings.StrafeRotationSpeed = strafeRotationSpeed;
            settings.BackwardSpeedMultiplier = backwardSpeedMultiplier;
        }

        public PlayerMotorSettings GetSettings() => settings;

        public void UpdateSettingsFromStruct(PlayerMotorSettings newSettings)
        {
            settings = newSettings;
        }

        public void Tick(Vector3 moveDirection, bool jumpRequested)
        {
            if (rb == null || rb.isKinematic)
            {
                IsGrounded = true;
                isAirborneFromJump = false;
                HorizontalVelocity = 0f;
                VerticalVelocity = 0f;
                return;
            }

            var upAxis = GetUpAxis();
            var velocity = rb.linearVelocity;
            var canUseGroundContacts = Time.time >= jumpDetachUntilTime;
            var hasGroundContact = canUseGroundContacts && slopeContactResolver.IsGrounded;
            var isGrounded = grounding != null && grounding.ResolveGroundedState(
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
                velocity = PlayerMotorMovementLogic.AccelerateOnGround(
                    velocity,
                    moveDirection,
                    upAxis,
                    slopeContactResolver,
                    settings,
                    forcedStrafeMode);
                velocity = PlayerMotorMovementLogic.ApplyGroundStepAssist(
                    velocity,
                    moveDirection,
                    upAxis,
                    rb,
                    bodyCollider,
                    slopeContactResolver,
                    settings);
            }
            else if (isOnSteepSlope)
            {
                inheritedGroundVelocity = Vector3.zero;
                if (slopeContactResolver.CanJumpOnSteepSlope)
                {
                    groundMotionTracker.TryGetGroundMotion(rb.position, out groundVelocity, out _, out _);
                }

                velocity = PlayerMotorMovementLogic.AccelerateOnSteepSlope(
                    velocity,
                    moveDirection,
                    upAxis,
                    slopeContactResolver,
                    settings,
                    forcedStrafeMode);
            }
            else
            {
                velocity = PlayerMotorMovementLogic.AccelerateInAir(
                    velocity,
                    moveDirection,
                    upAxis,
                    inheritedGroundVelocity,
                    slopeContactResolver,
                    settings,
                    forcedStrafeMode);
            }

            var nextRotation = PlayerMotorMovementLogic.CalculateRotation(
                rb.rotation,
                moveDirection,
                upAxis,
                groundRotationDelta,
                settings,
                forcedStrafeMode);
            rb.MoveRotation(nextRotation);

            var jumpResult = PlayerMotorJumpLogic.ApplyJumpIfRequested(
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

            velocity = jumpResult.Velocity;
            jumpDetachUntilTime = jumpResult.JumpDetachUntilTime;
            inheritedGroundVelocity = jumpResult.InheritedGroundVelocity;
            isAirborneFromJump = jumpResult.IsAirborneFromJump;

            velocity = PlayerMotorJumpLogic.ApplyExtraFallGravity(
                !isGrounded,
                isOnSteepSlope,
                upAxis,
                settings.FallMultiplier,
                velocity);

            rb.linearVelocity = velocity;
            HorizontalVelocity = Vector3.ProjectOnPlane(velocity - inheritedGroundVelocity, upAxis).magnitude;
            VerticalVelocity = Vector3.Dot(velocity, upAxis);
        }

        public void OnCollisionEnter(Collision collision)
        {
            var upAxis = GetUpAxis();
            slopeContactResolver.UpdateCollisionContacts(collision, upAxis, settings.GroundLayer, settings.MinGroundNormalDot);
            groundMotionTracker.UpdateGroundContact(collision, upAxis, settings.GroundLayer, settings.MinGroundNormalDot);
        }

        public void OnCollisionStay(Collision collision)
        {
            var upAxis = GetUpAxis();
            slopeContactResolver.UpdateCollisionContacts(collision, upAxis, settings.GroundLayer, settings.MinGroundNormalDot);
            groundMotionTracker.UpdateGroundContact(collision, upAxis, settings.GroundLayer, settings.MinGroundNormalDot);
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
