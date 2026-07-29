using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    public sealed class PlayerCompositeMotor : MonoBehaviour, IPlayerMoveInputReceiver, IPlayerMotorMotionSink
    {
        private Rigidbody rb;
        private IPlayerMotor baseMotor;
        private IPlayerTraversalCoordinator traversalCoordinator;
        private Vector2 rawMoveInput;
        private Quaternion moveReferenceRotation;
        private PlayerMotorMotionRequest activeMotion;
        private float activeMotionEndsAt;
        private bool hasActiveMotion;

        private void Awake()
        {
            EnsureRequiredComponents();
            rb = GetComponent<Rigidbody>();
            baseMotor = GetComponent<IPlayerMotor>();
            traversalCoordinator = GetComponent<IPlayerTraversalCoordinator>();
            moveReferenceRotation = transform.rotation;
        }

        private void OnValidate()
        {
            EnsureRequiredComponents();
        }

        private void EnsurePlayerMotor()
        {
            if (GetComponent<IPlayerMotor>() == null)
            {
                gameObject.AddComponent<PlayerMotor>();
            }
        }

        private void EnsureTraversalCoordinator()
        {
            if (GetComponent<IPlayerTraversalCoordinator>() == null)
            {
                gameObject.AddComponent<PlayerTraversalCoordinator>();
            }
        }

        private void EnsureRequiredComponents()
        {
            EnsurePlayerMotor();
            EnsureTraversalCoordinator();

            if (GetComponent<Rigidbody>() == null)
            {
                gameObject.AddComponent<Rigidbody>();
            }

            if (GetComponent<SlopeContactResolver>() == null)
            {
                gameObject.AddComponent<SlopeContactResolver>();
            }

            if (GetComponent<GroundMotionTracker>() == null)
            {
                gameObject.AddComponent<GroundMotionTracker>();
            }
        }

        public bool IsGrounded => baseMotor != null && baseMotor.IsEnabled && baseMotor.IsGrounded;
        public bool IsJumping => baseMotor != null && baseMotor.IsEnabled && baseMotor.IsJumping;
        public bool IsFallingAfterJump => baseMotor != null && baseMotor.IsEnabled && baseMotor.IsFallingAfterJump;
        public bool IsTraversalActive => traversalCoordinator != null
            && traversalCoordinator.IsEnabled
            && traversalCoordinator.IsTraversalActive;
        public bool IsFreefall => baseMotor != null && baseMotor.IsEnabled && baseMotor.IsFreefall && !IsTraversalActive;
        public Vector3 InheritedGroundVelocity => baseMotor != null && baseMotor.IsEnabled ? baseMotor.InheritedGroundVelocity : Vector3.zero;

        internal PlayerCompositeMotorDebugSnapshot GetDebugSnapshot()
        {
            var hasActiveExternalMotion = hasActiveMotion && Time.time < activeMotionEndsAt;
            return new PlayerCompositeMotorDebugSnapshot(
                rawMoveInput,
                moveReferenceRotation,
                hasActiveExternalMotion,
                hasActiveExternalMotion ? Mathf.Max(0f, activeMotionEndsAt - Time.time) : 0f);
        }

        public float HorizontalVelocity
        {
            get
            {
                if (rb == null)
                {
                    return baseMotor != null ? baseMotor.HorizontalVelocity : 0f;
                }

                var upAxis = GetUpAxis();
                return Vector3.ProjectOnPlane(rb.linearVelocity, upAxis).magnitude;
            }
        }

        public float VerticalVelocity
        {
            get
            {
                if (rb == null)
                {
                    return baseMotor != null ? baseMotor.VerticalVelocity : 0f;
                }

                var upAxis = GetUpAxis();
                return Vector3.Dot(rb.linearVelocity, upAxis);
            }
        }

        public void ResetState()
        {
            hasActiveMotion = false;
            baseMotor?.ResetState();
            traversalCoordinator?.ResetState();
        }

        public void SetMoveInput(Vector2 moveInput)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            rawMoveInput = moveInput;
        }

        public void SetMoveReferenceRotation(Quaternion referenceRotation)
        {
            if (isActiveAndEnabled)
            {
                moveReferenceRotation = referenceRotation;
            }
        }

        public void SetStrafeMode(bool enabled)
        {
            baseMotor?.SetStrafeMode(enabled);
        }

        public void Tick(Vector3 moveDirection, bool jumpRequested)
        {
            if (!isActiveAndEnabled || baseMotor == null || !baseMotor.IsEnabled)
            {
                return;
            }

            var motorResult = baseMotor.Tick(moveDirection, jumpRequested);
            if (traversalCoordinator != null && traversalCoordinator.IsEnabled)
            {
                traversalCoordinator.ApplyTraversal(
                    moveDirection,
                    rawMoveInput,
                    moveReferenceRotation,
                    jumpRequested && !motorResult.JumpConsumed,
                    baseMotor.IsGrounded);
            }

            ApplyActiveMotion();
        }

        public bool TryStartMotion(PlayerMotorMotionRequest request)
        {
            if (!isActiveAndEnabled || request.Direction.sqrMagnitude <= 0.0001f || request.Duration <= 0f)
            {
                return false;
            }

            if (hasActiveMotion && Time.time < activeMotionEndsAt && request.Priority < activeMotion.Priority)
            {
                return false;
            }

            activeMotion = request;
            activeMotionEndsAt = Time.time + request.Duration;
            hasActiveMotion = true;
            traversalCoordinator?.ResetState();
            return true;
        }

        public void StopMotion(int ownerId)
        {
            if (hasActiveMotion && activeMotion.OwnerId == ownerId)
            {
                hasActiveMotion = false;
            }
        }

        private void ApplyActiveMotion()
        {
            if (!hasActiveMotion || rb == null)
            {
                return;
            }

            if (Time.time >= activeMotionEndsAt)
            {
                hasActiveMotion = false;
                return;
            }

            var upAxis = GetUpAxis();
            var direction = Vector3.ProjectOnPlane(activeMotion.Direction, upAxis).normalized;
            var verticalVelocity = Vector3.Project(rb.linearVelocity, upAxis);
            rb.linearVelocity = direction * activeMotion.Speed + verticalVelocity;
        }

        private static Vector3 GetUpAxis()
        {
            return Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
        }
    }
}
