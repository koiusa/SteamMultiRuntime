using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public static class PlayerPointerAim
    {
        public static Vector3 ResolveDirection(
            Transform cameraTransform,
            Transform fallbackTransform,
            Vector3 aimOrigin,
            Rigidbody aimOwner,
            bool hasPointerPosition,
            Vector2 pointerPosition)
        {
            var camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            if (camera == null)
            {
                camera = Camera.main;
            }

            if (camera != null && hasPointerPosition)
            {
                var pointerRay = camera.ScreenPointToRay(pointerPosition);
                var targetPoint = ResolvePointerTarget(pointerRay, camera.farClipPlane, aimOwner);
                return (targetPoint - aimOrigin).normalized;
            }

            var reference = cameraTransform != null ? cameraTransform : fallbackTransform;
            return reference != null ? reference.forward : Vector3.forward;
        }

        private static Vector3 ResolvePointerTarget(Ray pointerRay, float maximumDistance, Rigidbody aimOwner)
        {
            var hits = Physics.RaycastAll(pointerRay, maximumDistance, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (aimOwner != null
                    && (collider.attachedRigidbody == aimOwner || collider.transform.IsChildOf(aimOwner.transform)))
                {
                    continue;
                }

                return hits[i].point;
            }

            return pointerRay.GetPoint(maximumDistance);
        }
    }
}
