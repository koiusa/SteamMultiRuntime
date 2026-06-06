using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public partial class NpcNavMeshController
    {
        private Vector3 BuildBoidSteeringPlanar(Vector3 upAxis, Vector3 goalPlanarVelocity)
        {
            var radius = Mathf.Max(0.1f, boidSeparationRadius);
            var count = Physics.OverlapSphereNonAlloc(transform.position, radius, _boidNeighborBuffer, ~0, QueryTriggerInteraction.Ignore);
            if (count <= 0)
                return goalPlanarVelocity;

            var separation = Vector3.zero;
            var neighborCount = 0;
            var maxNeighbors = Mathf.Clamp(boidMaxNeighbors, 1, _boidNeighborBuffer.Length);
            var radiusSqr = radius * radius;
            var uniqueNeighborIds = new int[32];
            var uniqueNeighborCount = 0;

            var referenceForward = Vector3.ProjectOnPlane(goalPlanarVelocity, upAxis);
            if (referenceForward.sqrMagnitude <= 0.0001f)
                referenceForward = Vector3.ProjectOnPlane(transform.forward, upAxis);
            if (referenceForward.sqrMagnitude > 0.0001f)
                referenceForward.Normalize();

            for (var i = 0; i < count && neighborCount < maxNeighbors; i++)
            {
                var col = _boidNeighborBuffer[i];
                if (col == null)
                    continue;
                if (col.attachedRigidbody == _rigidbody)
                    continue;

                var other = col.GetComponentInParent<IPlayerController>();
                if (other == null)
                    continue;

                var neighborKey = col.attachedRigidbody != null
                    ? col.attachedRigidbody.GetInstanceID()
                    : col.transform.root.GetInstanceID();

                var alreadyAdded = false;
                for (var keyIndex = 0; keyIndex < uniqueNeighborCount; keyIndex++)
                {
                    if (uniqueNeighborIds[keyIndex] != neighborKey)
                        continue;
                    alreadyAdded = true;
                    break;
                }

                if (alreadyAdded)
                    continue;

                uniqueNeighborIds[uniqueNeighborCount++] = neighborKey;

                var neighborPosition = col.attachedRigidbody != null ? col.attachedRigidbody.worldCenterOfMass : col.bounds.center;
                var delta = transform.position - neighborPosition;
                var planarDelta = Vector3.ProjectOnPlane(delta, upAxis);
                var sqr = planarDelta.sqrMagnitude;
                if (sqr <= 0.0001f || sqr > radiusSqr)
                    continue;

                var directionToNeighbor = -planarDelta.normalized;
                if (boidUseForwardNeighborFilter && referenceForward.sqrMagnitude > 0.0001f)
                {
                    var forwardDot = Vector3.Dot(referenceForward, directionToNeighbor);
                    if (forwardDot < boidNeighborForwardDotMin)
                        continue;
                }

                var distance = Mathf.Sqrt(sqr);
                var normalizedDistance = Mathf.Clamp01(distance / radius);
                var strength = 1f - normalizedDistance;
                var exponent = Mathf.Max(1f, boidSeparationExponent);
                strength = Mathf.Pow(strength, exponent);
                separation += planarDelta.normalized * strength;
                neighborCount++;
            }

            if (neighborCount == 0)
                return goalPlanarVelocity;

            separation /= neighborCount;

            var goalPlanar = Vector3.ProjectOnPlane(goalPlanarVelocity, upAxis);
            if (goalPlanar.sqrMagnitude > 0.0001f)
            {
                var goalDir = goalPlanar.normalized;
                var lateralSeparation = Vector3.ProjectOnPlane(separation, upAxis);
                var forwardComponent = Vector3.Dot(lateralSeparation, goalDir);
                if (forwardComponent > 0f)
                    lateralSeparation -= goalDir * forwardComponent;
                separation = lateralSeparation;
            }

            var blended = goalPlanarVelocity * boidGoalWeight + separation * boidSeparationWeight;
            return Vector3.ProjectOnPlane(blended, upAxis);
        }
    }
}
