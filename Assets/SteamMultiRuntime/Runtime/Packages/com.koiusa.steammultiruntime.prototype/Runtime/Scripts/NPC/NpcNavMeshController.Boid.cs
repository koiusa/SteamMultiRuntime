using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public partial class NpcNavMeshController
    {
        private Vector3 BuildBoidSteeringPlanar(Vector3 upAxis, Vector3 goalPlanarVelocity)
        {
            if (_hasCrowdSteering)
                return Vector3.ProjectOnPlane(_crowdSteeringPlanar, upAxis);

            var goalPlanar = Vector3.ProjectOnPlane(goalPlanarVelocity, upAxis);
            if (goalPlanar.sqrMagnitude <= 0.000001f)
                return goalPlanarVelocity;

            var radius = Mathf.Max(0.1f, boidSeparationRadius);
            var count = GetSpatialNeighbors(this, radius, _npcNeighborBuffer);
            if (count <= 0)
                return goalPlanarVelocity;

            var separation = Vector3.zero;
            var neighborCount = 0;
            var maxNeighbors = Mathf.Clamp(boidMaxNeighbors, 1, _npcNeighborBuffer.Length);
            var radiusSqr = radius * radius;
            var separationExponent = Mathf.Max(1f, boidSeparationExponent);
            var selfPosition = transform.position;

            var referenceForward = goalPlanar;
            if (referenceForward.sqrMagnitude <= 0.0001f)
                referenceForward = Vector3.ProjectOnPlane(transform.forward, upAxis);
            if (referenceForward.sqrMagnitude > 0.0001f)
                referenceForward.Normalize();

            for (var i = 0; i < count && neighborCount < maxNeighbors; i++)
            {
                var other = _npcNeighborBuffer[i];
                if (other == null)
                    continue;
                var otherBody = other._rigidbody;
                var neighborPosition = otherBody != null ? otherBody.worldCenterOfMass : other.transform.position;
                var delta = selfPosition - neighborPosition;
                var planarDelta = Vector3.ProjectOnPlane(delta, upAxis);
                var sqr = planarDelta.sqrMagnitude;
                if (sqr <= 0.0001f || sqr > radiusSqr)
                    continue;

                var distance = Mathf.Sqrt(sqr);
                var separationDirection = planarDelta / distance;
                var directionToNeighbor = -separationDirection;
                if (boidUseForwardNeighborFilter && referenceForward.sqrMagnitude > 0.0001f)
                {
                    var forwardDot = Vector3.Dot(referenceForward, directionToNeighbor);
                    if (forwardDot < boidNeighborForwardDotMin)
                        continue;
                }

                var normalizedDistance = Mathf.Clamp01(distance / radius);
                var strength = 1f - normalizedDistance;
                strength = Mathf.Pow(strength, separationExponent);
                separation += separationDirection * strength;
                neighborCount++;
            }

            if (neighborCount == 0)
                return goalPlanarVelocity;

            separation /= neighborCount;

            if (goalPlanar.sqrMagnitude > 0.0001f)
            {
                var goalDir = goalPlanar.normalized;
                var lateralSeparation = Vector3.ProjectOnPlane(separation, upAxis);
                var forwardComponent = Vector3.Dot(lateralSeparation, goalDir);
                if (forwardComponent > 0f)
                    lateralSeparation -= goalDir * forwardComponent;
                separation = lateralSeparation;
            }

            var goalContribution = goalPlanarVelocity * boidGoalWeight;
            var separationContribution = separation * boidSeparationWeight;
            separationContribution = Vector3.ClampMagnitude(
                separationContribution,
                goalContribution.magnitude * 0.75f);
            var blended = goalContribution + separationContribution;
            return Vector3.ProjectOnPlane(blended, upAxis);
        }
    }
}
