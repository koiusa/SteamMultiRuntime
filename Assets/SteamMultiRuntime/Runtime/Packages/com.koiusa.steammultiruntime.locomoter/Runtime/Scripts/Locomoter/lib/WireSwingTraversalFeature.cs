using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Camera-aimed, Rigidbody based wire swinging. Add this component to the
    /// same GameObject as the player Rigidbody and assign the input actions.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(WireGrappleTargetingFeature))]
    [RequireComponent(typeof(WireLineVisualFeature))]
    [DisallowMultipleComponent]
    public sealed class WireSwingTraversalFeature : MonoBehaviour, IWireSwingTraversalFeature
    {
        [Header("Dependencies")]
        [SerializeField] private WireGrappleTargetingFeature targeting;
        [SerializeField] private WireLineVisualFeature visual;

        [Header("Swing")]
        [SerializeField, Min(0.1f)] private float minimumRopeLength = 2f;
        [SerializeField, Min(0f)] private float ropeSlack = 0.15f;
        [SerializeField, Min(0f)] private float pullAcceleration = 55f;
        [SerializeField, Min(0f)] private float swingAcceleration = 16f;
        [SerializeField, Min(0f)] private float maximumInputSwingSpeed = 8f;
        [SerializeField, Min(0f)] private float reelSpeed = 12f;
        [SerializeField, Min(0f)] private float jumpReelDistance = 1.5f;
        [SerializeField, Range(0f, 1f)] private float radialVelocityDamping = 1f;

        private Rigidbody rb;
        private SlopeContactResolver slopeContactResolver;
        private Transform anchorTransform;
        private Vector3 anchorLocalPoint;
        private Vector3 fixedAnchorPoint;
        private float ropeLength;
        private Vector3 motorMoveDirection;
        private float externalReelInput;
        private bool blockAttachUntilRelease;

        public bool IsAttached { get; private set; }
        public bool IsEnabled => isActiveAndEnabled;
        public Transform AimTransform => targeting != null ? targeting.AimTransform : null;
        public float MaximumRange => targeting != null ? targeting.MaximumRange : 0f;
        public Vector3 AnchorPoint => anchorTransform != null
            ? anchorTransform.TransformPoint(anchorLocalPoint)
            : fixedAnchorPoint;
        public float RopeLength => ropeLength;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            slopeContactResolver = GetComponent<SlopeContactResolver>();
            if (targeting == null) targeting = GetComponent<WireGrappleTargetingFeature>();
            if (visual == null) visual = GetComponent<WireLineVisualFeature>();
            visual?.Initialize();
        }

        private void OnDisable()
        {
            blockAttachUntilRelease = false;
            Detach();
        }

        private void OnValidate()
        {
            if (targeting == null) targeting = GetComponent<WireGrappleTargetingFeature>();
            if (visual == null) visual = GetComponent<WireLineVisualFeature>();
            minimumRopeLength = Mathf.Max(0.1f, minimumRopeLength);
            ropeSlack = Mathf.Max(0f, ropeSlack);
            maximumInputSwingSpeed = Mathf.Max(0f, maximumInputSwingSpeed);
            jumpReelDistance = Mathf.Max(0f, jumpReelDistance);
        }

        private void Update()
        {
            if (IsAttached)
            {
                visual?.UpdateEndpoints(AnchorPoint);
            }
        }

        private void FixedUpdate()
        {
            if (rb == null || rb.isKinematic)
            {
                return;
            }

            if (!IsAttached)
            {
                return;
            }

            ApplyReelInput();
            ApplySwingForce();
            ConstrainRope();
        }

        /// <summary>Supplies the world-space movement intent produced by PlayerMotor.</summary>
        public void SetMoveDirection(Vector3 moveDirection)
        {
            motorMoveDirection = Vector3.ClampMagnitude(moveDirection, 1f);
        }

        public void SetGrappleInput(bool held, Vector3 origin, Vector3 aimDirection)
        {
            if (!held)
            {
                blockAttachUntilRelease = false;
                Detach();
                return;
            }

            if (!blockAttachUntilRelease && !IsAttached && aimDirection.sqrMagnitude > 0.0001f)
            {
                TryAttach(origin, aimDirection.normalized);
            }
        }

        public void SetReelInput(float reelInput)
        {
            externalReelInput = Mathf.Clamp(reelInput, -1f, 1f);
        }

        public void ReelByJump()
        {
            if (!IsAttached || jumpReelDistance <= 0f)
            {
                return;
            }

            ropeLength = Mathf.Clamp(ropeLength - jumpReelDistance, minimumRopeLength, MaximumRange);
        }

        public void DetachUntilInputRelease()
        {
            blockAttachUntilRelease = true;
            Detach();
        }

        public void SetReplicatedState(bool isAttached, Vector3 anchorPoint, float replicatedRopeLength)
        {
            if (!isAttached)
            {
                Detach();
                return;
            }

            anchorTransform = null;
            fixedAnchorPoint = anchorPoint;
            ropeLength = Mathf.Clamp(replicatedRopeLength, minimumRopeLength, MaximumRange);
            IsAttached = true;
            visual?.SetVisible(true);
        }

        /// <summary>Attaches to the first valid collider along a world-space ray.</summary>
        public bool TryAttach(Vector3 origin, Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            if (targeting != null
                && targeting.TryResolveAnchor(origin, direction, out var point, out var movingAnchor))
            {
                Attach(point, movingAnchor);
                return true;
            }

            return false;
        }

        /// <summary>Attaches directly; useful for AI or server-authoritative input.</summary>
        public void Attach(Vector3 worldPoint, Transform movingAnchor = null)
        {
            anchorTransform = movingAnchor;
            fixedAnchorPoint = worldPoint;
            anchorLocalPoint = movingAnchor != null ? movingAnchor.InverseTransformPoint(worldPoint) : Vector3.zero;
            ropeLength = Mathf.Clamp(Vector3.Distance(rb.worldCenterOfMass, worldPoint), minimumRopeLength, MaximumRange);
            IsAttached = true;
            visual?.SetVisible(true);
        }

        public void Detach()
        {
            if (!IsAttached)
            {
                return;
            }

            IsAttached = false;
            anchorTransform = null;
            motorMoveDirection = Vector3.zero;
            visual?.SetVisible(false);
        }

        private void ApplyReelInput()
        {
            ropeLength = Mathf.Clamp(ropeLength - externalReelInput * reelSpeed * Time.fixedDeltaTime, minimumRopeLength, MaximumRange);
        }

        private void ApplySwingForce()
        {
            if (swingAcceleration <= 0f
                || maximumInputSwingSpeed <= 0f
                || (slopeContactResolver != null && slopeContactResolver.IsGrounded))
            {
                return;
            }

            var desired = motorMoveDirection;
            var ropeDirection = (AnchorPoint - rb.worldCenterOfMass).normalized;
            var tangent = Vector3.ProjectOnPlane(desired, ropeDirection);
            if (tangent.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            var tangentialVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, ropeDirection);
            var targetSpeed = maximumInputSwingSpeed * Mathf.Clamp01(tangent.magnitude);
            var remainingSpeedRatio = Mathf.Clamp01((targetSpeed - tangentialVelocity.magnitude) / targetSpeed);
            if (remainingSpeedRatio <= 0f)
            {
                return;
            }

            var accelerationFactor = remainingSpeedRatio * remainingSpeedRatio;
            rb.AddForce(tangent.normalized * (swingAcceleration * accelerationFactor), ForceMode.Acceleration);
        }

        private void ConstrainRope()
        {
            var toAnchor = AnchorPoint - rb.worldCenterOfMass;
            var distance = toAnchor.magnitude;
            if (distance <= ropeLength + ropeSlack || distance < 0.001f)
            {
                return;
            }

            var towardAnchor = toAnchor / distance;
            var stretch = distance - ropeLength;
            rb.AddForce(towardAnchor * (stretch * pullAcceleration), ForceMode.Acceleration);

            // Remove only velocity that would stretch the rope. Tangential momentum,
            // which produces the pendulum motion, is deliberately preserved.
            var awaySpeed = Vector3.Dot(rb.linearVelocity, -towardAnchor);
            if (awaySpeed > 0f)
            {
                rb.linearVelocity += towardAnchor * (awaySpeed * radialVelocityDamping);
            }
        }

    }
}
