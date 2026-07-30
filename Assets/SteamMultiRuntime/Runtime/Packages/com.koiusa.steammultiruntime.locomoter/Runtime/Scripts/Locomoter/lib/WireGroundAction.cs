using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Uses a grounded player as the pivot for swinging an attached dynamic Rigidbody.</summary>
    [RequireComponent(typeof(WireTraversalFeature)), RequireComponent(typeof(SlopeContactResolver)), DisallowMultipleComponent]
    public sealed class WireGroundAction : MonoBehaviour, IWireGroundAction
    {
        [SerializeField, Min(0f)] private float objectSwingAcceleration = 12f;
        [SerializeField, Min(0f)] private float maximumObjectSwingSpeed = 10f;
        [SerializeField, Min(0f)] private float objectPullAcceleration = 55f;
        [SerializeField, Range(0f, 1f)] private float outwardVelocityDamping = 1f;
        [SerializeField, Min(0f)] private float facingRotationSpeed = 540f;
        [SerializeField, Min(0.01f), Tooltip("環境へ接続した際、通常移動からWire Strafeへ移行する減衰時間です。")]
        private float strafeBlendDamping = 0.3f;
        [SerializeField, Min(0.01f), Tooltip("アンカー方向への旋回へ移行する減衰時間です。")]
        private float facingBlendDamping = 0.12f;

        private IWireConnection connection;
        private IWireReelAction reelAction;
        private SlopeContactResolver ground;
        private Vector3 moveDirection;
        private float strafeBlend;
        private float strafeBlendVelocity;
        private float facingBlend;
        private float facingBlendVelocity;
        private bool wasUsingStrafeMovement;

        public bool IsEnabled => isActiveAndEnabled;
        private bool HasConnection => IsEnabled
            && connection != null
            && connection.IsAttached;
        private bool IsPlayerGrounded => ground != null && ground.IsGrounded;
        private bool HasDynamicAnchor => HasConnection
            && connection.AnchorBody != null
            && !connection.AnchorBody.isKinematic
            && connection.AnchorBody != connection.Body;
        public bool BlocksSwing => HandlesConnectionPhysics || (HasConnection && IsPlayerGrounded);
        public bool HandlesConnectionPhysics => HasDynamicAnchor;
        public bool UsesStrafeMovement => HasConnection && IsPlayerGrounded && !HasDynamicAnchor;
        // Keep exposing the damped value after Wire Ground ends so ActorMotor can
        // blend back to normal movement instead of snapping in a single tick.
        public float StrafeBlend => strafeBlend;
        public float FacingBlend => UsesStrafeMovement ? facingBlend : 0f;
        public float FacingRotationSpeed => facingRotationSpeed;

        private void Awake()
        {
            connection = GetComponent<IWireConnection>();
            reelAction = GetComponent<IWireReelAction>();
            ground = GetComponent<SlopeContactResolver>();
        }

        private void OnValidate()
        {
            objectSwingAcceleration = Mathf.Max(0f, objectSwingAcceleration);
            maximumObjectSwingSpeed = Mathf.Max(0f, maximumObjectSwingSpeed);
            objectPullAcceleration = Mathf.Max(0f, objectPullAcceleration);
            facingRotationSpeed = Mathf.Max(0f, facingRotationSpeed);
            strafeBlendDamping = Mathf.Max(0.01f, strafeBlendDamping);
            facingBlendDamping = Mathf.Max(0.01f, facingBlendDamping);
        }

        public void SetMoveDirection(Vector3 value)
        {
            moveDirection = Vector3.ClampMagnitude(value, 1f);
        }

        private void FixedUpdate()
        {
            var usesStrafeMovement = UsesStrafeMovement;
            if (usesStrafeMovement && !wasUsingStrafeMovement)
            {
                strafeBlend = 0f;
                strafeBlendVelocity = 0f;
                facingBlend = 0f;
                facingBlendVelocity = 0f;
            }
            strafeBlend = Mathf.SmoothDamp(
                strafeBlend,
                usesStrafeMovement ? 1f : 0f,
                ref strafeBlendVelocity,
                strafeBlendDamping,
                Mathf.Infinity,
                Time.fixedDeltaTime);
            facingBlend = Mathf.SmoothDamp(
                facingBlend,
                usesStrafeMovement ? 1f : 0f,
                ref facingBlendVelocity,
                facingBlendDamping,
                Mathf.Infinity,
                Time.fixedDeltaTime);
            wasUsingStrafeMovement = usesStrafeMovement;

            if (!HandlesConnectionPhysics) return;

            var targetBody = connection.AnchorBody;
            var pivot = connection.Body.worldCenterOfMass;
            var toPivot = pivot - targetBody.worldCenterOfMass;
            var distance = toPivot.magnitude;
            if (distance < 0.001f) return;

            var towardPivot = toPivot / distance;
            if (distance > connection.RopeLength)
            {
                var stretch = distance - connection.RopeLength;
                var useRopeConstraint = connection.ConstraintMode == WireConstraintMode.Rope
                    || (reelAction != null && reelAction.IsReelingIn);
                if (useRopeConstraint)
                {
                    targetBody.MovePosition(targetBody.position + towardPivot * stretch);
                }
                else
                {
                    targetBody.AddForce(towardPivot * (stretch * objectPullAcceleration), ForceMode.Acceleration);
                    var hardLimit = connection.RopeLength + connection.ElasticStretchLimit;
                    if (distance > hardLimit)
                    {
                        targetBody.MovePosition(targetBody.position + towardPivot * (distance - hardLimit));
                    }
                }
                var outwardSpeed = Vector3.Dot(targetBody.linearVelocity, -towardPivot);
                if (outwardSpeed > 0f)
                {
                    var reachedHardLimit = !useRopeConstraint
                        && distance >= connection.RopeLength + connection.ElasticStretchLimit;
                    var damping = useRopeConstraint || reachedHardLimit ? 1f : outwardVelocityDamping;
                    targetBody.linearVelocity += towardPivot * (outwardSpeed * damping);
                }
            }

            var tangent = Vector3.ProjectOnPlane(moveDirection, towardPivot);
            if (tangent.sqrMagnitude <= 0.0001f || objectSwingAcceleration <= 0f) return;
            var tangentialSpeed = Vector3.ProjectOnPlane(targetBody.linearVelocity, towardPivot).magnitude;
            var remaining = maximumObjectSwingSpeed > 0f
                ? Mathf.Clamp01((maximumObjectSwingSpeed - tangentialSpeed) / maximumObjectSwingSpeed)
                : 0f;
            targetBody.AddForce(tangent.normalized * (objectSwingAcceleration * remaining * remaining), ForceMode.Acceleration);
        }
    }
}
