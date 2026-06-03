using UnityEngine;
using UnityEngine.AI;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public class NpcNavMeshRotationModule
    {
        [Header("Rotation")]
        [SerializeField, Min(0f)] private float manualRotationSpeed = 540f;
        [SerializeField] private bool keepUpright = true;
        [SerializeField, Min(0f)] private float minVelocityToRotate = 0.1f;
        [SerializeField, Range(0f, 180f)] private float strafeAngleToUseVelocity = 55f;

        private NavMeshAgent _agent;
        private Transform _transform;

        public bool KeepUpright => keepUpright;

        public void Initialize(NavMeshAgent agent, Transform transform)
        {
            _agent = agent;
            _transform = transform;
        }

        public void UpdateRotation()
        {
            if (_agent == null || _transform == null)
                return;

            var upAxis = PlayerMotor.GetUpAxis();
            var minSpeedSqr = minVelocityToRotate * minVelocityToRotate;
            var desiredPlanar = Vector3.ProjectOnPlane(_agent.desiredVelocity, upAxis);
            var velocityPlanar = Vector3.ProjectOnPlane(_agent.velocity, upAxis);

            if (desiredPlanar.sqrMagnitude < minSpeedSqr && velocityPlanar.sqrMagnitude < minSpeedSqr)
                return;

            var heading = ResolveHeading(upAxis, desiredPlanar, velocityPlanar, minSpeedSqr);
            RotateTowardsDirection(heading, upAxis, manualRotationSpeed);
        }

        public void StabilizeUpright()
        {
            var upAxis = PlayerMotor.GetUpAxis();
            var planarForward = Vector3.ProjectOnPlane(_transform.forward, upAxis);
            if (planarForward.sqrMagnitude <= 0.0001f)
            {
                planarForward = Vector3.ProjectOnPlane(_transform.right, upAxis);
                if (planarForward.sqrMagnitude <= 0.0001f)
                    return;
            }

            _transform.rotation = Quaternion.LookRotation(planarForward.normalized, upAxis);
        }

        private Vector3 ResolveHeading(Vector3 upAxis, Vector3 desiredPlanar, Vector3 velocityPlanar, float minSpeedSqr)
        {
            if (desiredPlanar.sqrMagnitude >= minSpeedSqr && velocityPlanar.sqrMagnitude >= minSpeedSqr)
            {
                var desiredDir = desiredPlanar.normalized;
                var velocityDir = velocityPlanar.normalized;
                if (Vector3.Angle(desiredDir, velocityDir) >= strafeAngleToUseVelocity)
                    return velocityDir;
            }

            if (_agent.hasPath && !_agent.pathPending)
            {
                var steeringHeading = Vector3.ProjectOnPlane(_agent.steeringTarget - _transform.position, upAxis);
                if (steeringHeading.sqrMagnitude > 0.0001f)
                    return steeringHeading;
            }

            if (desiredPlanar.sqrMagnitude > 0.0001f)
                return desiredPlanar;

            if (velocityPlanar.sqrMagnitude > 0.0001f)
                return velocityPlanar;

            var heading = Vector3.ProjectOnPlane(_agent.destination - _transform.position, upAxis);
            if (heading.sqrMagnitude > 0.0001f)
                return heading;

            return Vector3.ProjectOnPlane(_transform.forward, upAxis);
        }

        private void RotateTowardsDirection(Vector3 direction, Vector3 upAxis, float speed)
        {
            var targetForward = Vector3.ProjectOnPlane(direction, upAxis);
            if (targetForward.sqrMagnitude <= 0.000001f)
                return;

            var targetRotation = Quaternion.LookRotation(targetForward.normalized, upAxis);
            _transform.rotation = Quaternion.RotateTowards(_transform.rotation, targetRotation, Mathf.Max(0f, speed) * Time.deltaTime);
        }
    }
}
