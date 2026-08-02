using Unity.Profiling;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class NpcCrowdMovingPlatformAction : MonoBehaviour
    {
        private static readonly ProfilerMarker BindingMarker = new("Physics.NpcCrowd.MovingPlatformBinding");
        internal event System.Action<bool> MovingPlatformBindingChanged;
        internal event System.Action<IGroundMotionPhysicsPoseSource> PhysicsPoseSourceBindingChanged;

        private Collider groundCollider;
        private Collider actorCollider;
        private IGroundMotionSource motionSource;
        private IGroundMotionSnapshotSource snapshotSource;
        private bool hasMovingPlatformBinding;
        private Vector3 physicsFollowVelocity;

        internal bool HasPhysicsPoseSource => snapshotSource is IGroundMotionPhysicsPoseSource;

        internal void Initialize(Collider locomotionCollider) => actorCollider = locomotionCollider;

        internal void IgnorePhysicsPair(Collider collider)
        {
            if (actorCollider != null && collider != null && collider != actorCollider)
                Physics.IgnoreCollision(actorCollider, collider, true);
        }

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
                // Physics-pose sources already push their exact fixed-step delta to
                // bound followers. Do not apply the same displacement again at the
                // lower-rate Crowd step, regardless of the concrete implementation.
                if (snapshotSource is IGroundMotionPhysicsPoseSource)
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

        internal void ClearPhysicsPoseSource(IGroundMotionPhysicsPoseSource source)
        {
            if (source != null && ReferenceEquals(snapshotSource, source))
                Bind(null);
        }

        internal bool IsBoundTo(Collider collider) => collider != null && collider == groundCollider;

        internal bool TrySamplePhysicsFollow(
            IGroundMotionPhysicsPoseSource source,
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
            using var marker = BindingMarker.Auto();
            var previousPhysicsPoseSource = snapshotSource as IGroundMotionPhysicsPoseSource;
            groundCollider = collider;
            motionSource = null;
            snapshotSource = null;
            physicsFollowVelocity = Vector3.zero;
            if (collider == null)
            {
                if (previousPhysicsPoseSource != null)
                    PhysicsPoseSourceBindingChanged?.Invoke(null);
                SetMovingPlatformBinding(false);
                return;
            }

            GroundMotionSourceResolver.Resolve(collider.transform, out motionSource, out snapshotSource);
            var physicsPoseSource = snapshotSource as IGroundMotionPhysicsPoseSource;
            // Registered moving floors are prepared before contact. Retain this only
            // for ordinary ground so the contact path never repeats moving-floor setup.
            if (physicsPoseSource == null)
                IgnorePhysicsPair(collider);
            if (!ReferenceEquals(previousPhysicsPoseSource, physicsPoseSource))
                PhysicsPoseSourceBindingChanged?.Invoke(physicsPoseSource);
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
