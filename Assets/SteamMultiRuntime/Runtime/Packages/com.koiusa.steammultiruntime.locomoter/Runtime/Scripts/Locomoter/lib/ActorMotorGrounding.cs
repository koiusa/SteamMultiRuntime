using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class ActorMotorGrounding
    {
        private const float NearbyGroundProbeOffset = 0.05f;
        private const float GroundedGraceMaxUpwardSpeed = 1.5f;

        private readonly RaycastHit[] nearbyGroundHits = new RaycastHit[8];
        private float lastGroundedTime = float.NegativeInfinity;

        public void ResetState()
        {
            lastGroundedTime = float.NegativeInfinity;
        }

        public bool ResolveGroundedState(
            bool canUseGroundContacts,
            bool hasGroundContact,
            bool isAirborneFromJump,
            Vector3 velocity,
            Vector3 upAxis,
            Rigidbody rb,
            Collider bodyCollider,
            ActorMotorSettings settings)
        {
            if (hasGroundContact)
            {
                lastGroundedTime = Time.time;
                return true;
            }

            if (CanCheckNearbyGround(canUseGroundContacts, isAirborneFromJump, velocity, upAxis, bodyCollider, settings) &&
                TryGetNearbyGround(upAxis, rb, bodyCollider, settings))
            {
                lastGroundedTime = Time.time;
                return true;
            }

            return CanRetainGroundedByGrace(canUseGroundContacts, isAirborneFromJump, velocity, upAxis, settings);
        }

        private bool CanCheckNearbyGround(
            bool canUseGroundContacts,
            bool isAirborneFromJump,
            Vector3 velocity,
            Vector3 upAxis,
            Collider bodyCollider,
            ActorMotorSettings settings)
        {
            if (bodyCollider == null || settings.NearbyGroundDistance <= 0f)
            {
                return false;
            }

            return CanUseGroundedGrace(canUseGroundContacts, isAirborneFromJump, velocity, upAxis, settings);
        }

        private bool CanRetainGroundedByGrace(
            bool canUseGroundContacts,
            bool isAirborneFromJump,
            Vector3 velocity,
            Vector3 upAxis,
            ActorMotorSettings settings)
        {
            return CanUseGroundedGrace(canUseGroundContacts, isAirborneFromJump, velocity, upAxis, settings);
        }

        private bool CanUseGroundedGrace(
            bool canUseGroundContacts,
            bool isAirborneFromJump,
            Vector3 velocity,
            Vector3 upAxis,
            ActorMotorSettings settings)
        {
            if (!canUseGroundContacts || isAirborneFromJump || settings.GroundedGraceTime <= 0f)
            {
                return false;
            }

            if (Time.time - lastGroundedTime > settings.GroundedGraceTime)
            {
                return false;
            }

            return Vector3.Dot(velocity, upAxis) <= GroundedGraceMaxUpwardSpeed;
        }

        private bool TryGetNearbyGround(Vector3 upAxis, Rigidbody rb, Collider bodyCollider, ActorMotorSettings settings)
        {
            if (!TryGetNearbyGroundCapsule(bodyCollider, upAxis, out var point1, out var point2, out var radius))
            {
                return false;
            }

            var hitCount = Physics.CapsuleCastNonAlloc(
                point1 + upAxis * NearbyGroundProbeOffset,
                point2 + upAxis * NearbyGroundProbeOffset,
                radius,
                -upAxis,
                nearbyGroundHits,
                NearbyGroundProbeOffset + settings.NearbyGroundDistance,
                settings.GroundLayer,
                QueryTriggerInteraction.Ignore);

            if (hitCount <= 0)
            {
                return false;
            }

            var foundGround = false;
            var bestHitDistance = float.PositiveInfinity;

            for (var i = 0; i < hitCount; i++)
            {
                var hit = nearbyGroundHits[i];
                if (IsOwnCollider(hit.collider, rb, bodyCollider))
                {
                    continue;
                }

                var upDot = Vector3.Dot(hit.normal, upAxis);
                if (upDot < settings.MinGroundNormalDot || hit.distance >= bestHitDistance)
                {
                    continue;
                }

                bestHitDistance = hit.distance;
                foundGround = true;
            }

            if (!foundGround)
            {
                return false;
            }

            var currentGap = bestHitDistance - NearbyGroundProbeOffset;
            return currentGap <= settings.NearbyGroundDistance;
        }

        private static bool TryGetNearbyGroundCapsule(Collider bodyCollider, Vector3 upAxis, out Vector3 point1, out Vector3 point2, out float radius)
        {
            var bounds = bodyCollider.bounds;
            if (bounds.size.sqrMagnitude <= Mathf.Epsilon)
            {
                point1 = default;
                point2 = default;
                radius = 0f;
                return false;
            }

            var axisA = GetPerpendicularAxis(upAxis);
            var axisB = Vector3.Cross(upAxis, axisA).normalized;
            var extentAlongUp = GetExtentAlongAxis(bounds.extents, upAxis);
            var extentAlongA = GetExtentAlongAxis(bounds.extents, axisA);
            var extentAlongB = GetExtentAlongAxis(bounds.extents, axisB);
            radius = Mathf.Min(extentAlongA, extentAlongB);

            if (radius <= Mathf.Epsilon)
            {
                point1 = default;
                point2 = default;
                radius = 0f;
                return false;
            }

            var center = bounds.center;
            var halfSegment = Mathf.Max(0f, extentAlongUp - radius);
            point1 = center + upAxis * halfSegment;
            point2 = center - upAxis * halfSegment;
            return true;
        }

        private static bool IsOwnCollider(Collider collider, Rigidbody rb, Collider bodyCollider)
        {
            if (collider == null)
            {
                return true;
            }

            if (collider == bodyCollider)
            {
                return true;
            }

            return collider.attachedRigidbody == rb || collider.transform.IsChildOf(rb.transform);
        }

        private static float GetExtentAlongAxis(Vector3 extents, Vector3 axis)
        {
            return Mathf.Abs(extents.x * axis.x) + Mathf.Abs(extents.y * axis.y) + Mathf.Abs(extents.z * axis.z);
        }

        private static Vector3 GetPerpendicularAxis(Vector3 axis)
        {
            var referenceAxis = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up;
            return Vector3.Cross(axis, referenceAxis).normalized;
        }
    }
}
