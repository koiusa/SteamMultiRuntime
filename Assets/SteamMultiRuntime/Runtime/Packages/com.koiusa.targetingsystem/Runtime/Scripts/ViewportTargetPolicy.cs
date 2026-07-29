using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ViewportTargetPolicy : MonoBehaviour, ITargetFilter, ITargetScorer
    {
        [SerializeField, Min(0f)] private float acquisitionDistance = 20f;
        [SerializeField, Range(0f, 0.49f)] private float viewportPadding = 0.02f;
        [SerializeField, Min(0f)] private float centerWeight = 1f;
        [SerializeField, Min(0f)] private float distanceWeight = 0.25f;
        [SerializeField, Min(0f)] private float priorityWeight = 1f;

        public bool Accept(ITargetable target, in TargetingContext context)
        {
            var aimPoint = ResolveAimPoint(target);
            if (aimPoint == null)
            {
                return false;
            }

            var offset = aimPoint.position - context.Origin;
            if (offset.sqrMagnitude > acquisitionDistance * acquisitionDistance)
            {
                return false;
            }

            if (context.ViewCamera == null)
            {
                return Vector3.Dot(context.Forward, offset) > 0f;
            }

            var viewport = context.ViewCamera.WorldToViewportPoint(aimPoint.position);
            var min = viewportPadding;
            var max = 1f - viewportPadding;
            return viewport.z > 0f
                && viewport.x >= min && viewport.x <= max
                && viewport.y >= min && viewport.y <= max;
        }

        public float Score(ITargetable target, in TargetingContext context)
        {
            var aimPoint = ResolveAimPoint(target);
            if (aimPoint == null)
            {
                return float.PositiveInfinity;
            }

            var offset = aimPoint.position - context.Origin;
            var normalizedDistance = acquisitionDistance > 0f
                ? Mathf.Clamp01(offset.magnitude / acquisitionDistance)
                : 0f;
            var centerDistance = 1f;

            if (context.ViewCamera != null)
            {
                var viewport = context.ViewCamera.WorldToViewportPoint(aimPoint.position);
                centerDistance = new Vector2(viewport.x - 0.5f, viewport.y - 0.5f).sqrMagnitude;
            }
            else if (offset.sqrMagnitude > 0f)
            {
                centerDistance = 1f - Mathf.Clamp01(Vector3.Dot(context.Forward.normalized, offset.normalized));
            }

            return centerDistance * centerWeight
                + normalizedDistance * distanceWeight
                - target.Priority * priorityWeight;
        }

        private static Transform ResolveAimPoint(ITargetable target) =>
            target?.AimPoint != null ? target.AimPoint : target?.Root;
    }
}
