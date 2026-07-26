using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Uses a grounded player as the pivot for swinging an attached dynamic Rigidbody.</summary>
    [RequireComponent(typeof(WireConnection)), RequireComponent(typeof(SlopeContactResolver)), DisallowMultipleComponent]
    public sealed class WireGroundAction : MonoBehaviour, IWireGroundAction
    {
        [SerializeField, Min(0f)] private float objectSwingAcceleration = 12f;
        [SerializeField, Min(0f)] private float maximumObjectSwingSpeed = 10f;
        [SerializeField, Min(0f)] private float objectPullAcceleration = 55f;
        [SerializeField, Range(0f, 1f)] private float outwardVelocityDamping = 1f;
        [SerializeField, Min(0f)] private float facingRotationSpeed = 540f;

        private IWireConnection connection;
        private SlopeContactResolver ground;
        private Vector3 moveDirection;

        public bool IsEnabled => isActiveAndEnabled;
        private bool HasGroundConnection => IsEnabled
            && ground != null
            && ground.IsGrounded
            && connection != null
            && connection.IsAttached;
        private bool HasDynamicAnchor => connection.AnchorBody != null
            && !connection.AnchorBody.isKinematic
            && connection.AnchorBody != connection.Body;
        public bool BlocksSwing => HasGroundConnection;
        public bool HandlesConnectionPhysics => HasGroundConnection && HasDynamicAnchor;
        public bool UsesStrafeMovement => HasGroundConnection && !HasDynamicAnchor;
        public bool UsesMaximumRangeConstraint => IsEnabled
            && connection != null
            && connection.IsAttached
            && !HasDynamicAnchor;
        public float FacingRotationSpeed => facingRotationSpeed;

        private void Awake()
        {
            connection = GetComponent<IWireConnection>();
            ground = GetComponent<SlopeContactResolver>();
        }

        private void OnValidate()
        {
            objectSwingAcceleration = Mathf.Max(0f, objectSwingAcceleration);
            maximumObjectSwingSpeed = Mathf.Max(0f, maximumObjectSwingSpeed);
            objectPullAcceleration = Mathf.Max(0f, objectPullAcceleration);
            facingRotationSpeed = Mathf.Max(0f, facingRotationSpeed);
        }

        public void SetMoveDirection(Vector3 value)
        {
            moveDirection = Vector3.ClampMagnitude(value, 1f);
        }

        private void FixedUpdate()
        {
            if (!HandlesConnectionPhysics) return;

            var targetBody = connection.AnchorBody;
            var pivot = connection.Body.worldCenterOfMass;
            var toPivot = pivot - targetBody.worldCenterOfMass;
            var distance = toPivot.magnitude;
            if (distance < 0.001f) return;

            var towardPivot = toPivot / distance;
            if (distance > connection.RopeLength)
            {
                targetBody.AddForce(towardPivot * ((distance - connection.RopeLength) * objectPullAcceleration), ForceMode.Acceleration);
                var outwardSpeed = Vector3.Dot(targetBody.linearVelocity, -towardPivot);
                if (outwardSpeed > 0f) targetBody.linearVelocity += towardPivot * (outwardSpeed * outwardVelocityDamping);
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
