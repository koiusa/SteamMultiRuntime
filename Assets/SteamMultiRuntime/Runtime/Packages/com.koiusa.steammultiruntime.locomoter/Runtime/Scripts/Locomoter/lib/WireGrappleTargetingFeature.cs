using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Finds a valid grapple anchor along the requested aim ray.</summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class WireGrappleTargetingFeature : MonoBehaviour, IWireGrappleTargetingFeature
    {
        [SerializeField] private Transform aimTransform;
        [SerializeField, Min(1f)] private float maximumRange = 45f;
        [SerializeField] private LayerMask grappleLayers = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        private Rigidbody ownerBody;
        private Collider[] ownerColliders;

        public Transform AimTransform => aimTransform;
        public float MaximumRange => maximumRange;
        public bool IsEnabled => isActiveAndEnabled;

        private void Awake()
        {
            CacheOwner();
        }

        private void OnValidate()
        {
            maximumRange = Mathf.Max(1f, maximumRange);
        }

        public bool TryResolveAnchor(Vector3 origin, Vector3 direction, out Vector3 point, out Transform anchorTransform)
        {
            point = Vector3.zero;
            anchorTransform = null;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            CacheOwner();
            var hits = Physics.RaycastAll(origin, direction.normalized, maximumRange, ~0, triggerInteraction);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                if (IsOwnerCollider(hits[i].collider))
                {
                    continue;
                }

                if ((grappleLayers.value & (1 << hits[i].collider.gameObject.layer)) == 0)
                {
                    return false;
                }

                point = hits[i].point;
                anchorTransform = hits[i].collider.transform;
                return true;
            }

            return false;
        }

        public WireAimResult EvaluateTarget(Vector3 origin, Vector3 targetPoint)
        {
            var offset = targetPoint - origin;
            var distance = offset.magnitude;
            if (distance <= 0.001f || distance > maximumRange)
            {
                return WireAimResult.Invalid(targetPoint);
            }

            CacheOwner();
            var hits = Physics.RaycastAll(origin, offset / distance, distance + 0.05f, ~0, triggerInteraction);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (var i = 0; i < hits.Length; i++)
            {
                if (IsOwnerCollider(hits[i].collider)) continue;
                if (hits[i].distance < distance - 0.02f)
                {
                    return new WireAimResult(ScreenAimTargetState.Obstructed, targetPoint);
                }

                if ((grappleLayers.value & (1 << hits[i].collider.gameObject.layer)) == 0)
                {
                    return WireAimResult.Invalid(targetPoint);
                }

                return new WireAimResult(
                    ScreenAimTargetState.Valid,
                    targetPoint,
                    hits[i].point,
                    hits[i].collider.transform);
            }

            return WireAimResult.Invalid(targetPoint);
        }

        private void CacheOwner()
        {
            if (ownerBody == null)
            {
                ownerBody = GetComponent<Rigidbody>();
            }

            if (ownerColliders == null)
            {
                ownerColliders = GetComponentsInChildren<Collider>();
            }
        }

        private bool IsOwnerCollider(Collider candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            for (var i = 0; i < ownerColliders.Length; i++)
            {
                if (candidate == ownerColliders[i])
                {
                    return true;
                }
            }

            return candidate.attachedRigidbody == ownerBody;
        }
    }
}
