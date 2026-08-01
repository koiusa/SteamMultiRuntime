using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class NpcCrowdMovingPlatformAction : MonoBehaviour
    {
        internal event System.Action<bool> MovingPlatformBindingChanged;

        private Collider groundCollider;
        private IGroundMotionSource motionSource;
        private IGroundMotionSnapshotSource snapshotSource;
        private bool hasMovingPlatformBinding;
        private Vector3 physicsFollowVelocity;

        internal void Sample(
            Collider collider,
            Vector3 samplePoint,
            float deltaTime,
            out Vector3 velocity,
            out Vector3 displacement,
            out Quaternion rotationDelta)
        {
            if (collider != groundCollider)
                Bind(collider);
            SampleBound(samplePoint, deltaTime, out velocity, out displacement, out rotationDelta);
        }

        internal void SampleBound(
            Vector3 samplePoint,
            float deltaTime,
            out Vector3 velocity,
            out Vector3 displacement,
            out Quaternion rotationDelta)
        {
            if (snapshotSource != null)
            {
                if (snapshotSource is PrototypeMotionMover)
                {
                    velocity = physicsFollowVelocity;
                    displacement = Vector3.zero;
                    rotationDelta = Quaternion.identity;
                    return;
                }
                snapshotSource.GetGroundMotion(samplePoint, deltaTime, out velocity, out displacement, out rotationDelta);
                return;
            }
            if (motionSource != null)
            {
                velocity = motionSource.GetPointVelocity(samplePoint);
                displacement = motionSource.GetPointDisplacement(samplePoint, deltaTime);
                rotationDelta = motionSource.GetRotationDelta(deltaTime);
                return;
            }
            var body = groundCollider != null ? groundCollider.attachedRigidbody : null;
            velocity = body != null ? body.GetPointVelocity(samplePoint) : Vector3.zero;
            displacement = velocity * deltaTime;
            rotationDelta = Quaternion.identity;
        }

        internal void Clear() => Bind(null);

        internal bool IsBoundTo(Collider collider) => collider != null && collider == groundCollider;

        internal bool TrySamplePhysicsFollow(
            PrototypeMotionMover source,
            Vector3 samplePoint,
            float deltaTime,
            out Vector3 displacement,
            out Quaternion rotationDelta)
        {
            if (!hasMovingPlatformBinding || source == null || !ReferenceEquals(snapshotSource, source))
            {
                displacement = Vector3.zero;
                rotationDelta = Quaternion.identity;
                return false;
            }

            source.GetGroundMotion(
                samplePoint,
                deltaTime,
                out physicsFollowVelocity,
                out displacement,
                out rotationDelta);
            return true;
        }

        internal bool CanRetainMovingPlatformBinding(Vector3 samplePoint, float maxDistance)
        {
            if (!hasMovingPlatformBinding || groundCollider == null || !groundCollider.enabled)
                return false;
            var closestPoint = groundCollider.ClosestPoint(samplePoint);
            return (closestPoint - samplePoint).sqrMagnitude <= maxDistance * maxDistance;
        }

        private void Bind(Collider collider)
        {
            groundCollider = collider;
            motionSource = null;
            snapshotSource = null;
            physicsFollowVelocity = Vector3.zero;
            if (collider == null)
            {
                SetMovingPlatformBinding(false);
                return;
            }
            var behaviours = collider.GetComponentsInParent<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                motionSource ??= behaviours[i] as IGroundMotionSource;
                snapshotSource ??= behaviours[i] as IGroundMotionSnapshotSource;
                if (motionSource != null && snapshotSource != null)
                    break;
            }
            SetMovingPlatformBinding(motionSource != null || snapshotSource != null);
        }

        private void SetMovingPlatformBinding(bool value)
        {
            if (hasMovingPlatformBinding == value)
                return;
            hasMovingPlatformBinding = value;
            MovingPlatformBindingChanged?.Invoke(value);
        }
    }
}
