using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [DisallowMultipleComponent]
    public sealed class NpcCrowdMovingPlatformAction : MonoBehaviour
    {
        private Collider groundCollider;
        private IGroundMotionSource motionSource;
        private IGroundMotionSnapshotSource snapshotSource;

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
            if (snapshotSource != null)
            {
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
            var body = collider != null ? collider.attachedRigidbody : null;
            velocity = body != null ? body.GetPointVelocity(samplePoint) : Vector3.zero;
            displacement = velocity * deltaTime;
            rotationDelta = Quaternion.identity;
        }

        internal void Clear() => Bind(null);

        private void Bind(Collider collider)
        {
            groundCollider = collider;
            motionSource = null;
            snapshotSource = null;
            if (collider == null)
                return;
            var behaviours = collider.GetComponentsInParent<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                motionSource ??= behaviours[i] as IGroundMotionSource;
                snapshotSource ??= behaviours[i] as IGroundMotionSnapshotSource;
                if (motionSource != null && snapshotSource != null)
                    break;
            }
        }
    }
}
