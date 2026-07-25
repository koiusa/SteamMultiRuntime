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
            var hits = Physics.RaycastAll(origin, direction.normalized, maximumRange, grappleLayers, triggerInteraction);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (var i = 0; i < hits.Length; i++)
            {
                if (IsOwnerCollider(hits[i].collider))
                {
                    continue;
                }

                point = hits[i].point;
                anchorTransform = hits[i].collider.transform;
                return true;
            }

            return false;
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
