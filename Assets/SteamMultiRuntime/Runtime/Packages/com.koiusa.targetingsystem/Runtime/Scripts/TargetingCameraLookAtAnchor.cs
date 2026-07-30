using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetingCameraLookAtAnchor : MonoBehaviour
    {
        private Transform target;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            UpdateRotation();
        }

        private void LateUpdate() => UpdateRotation();

        private void UpdateRotation()
        {
            if (target == null) return;

            var direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
    }
}
