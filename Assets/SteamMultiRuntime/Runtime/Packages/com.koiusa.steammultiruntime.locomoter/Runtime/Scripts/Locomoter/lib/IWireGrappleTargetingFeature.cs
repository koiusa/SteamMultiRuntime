using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IWireGrappleTargetingFeature
    {
        bool IsEnabled { get; }
        Transform AimTransform { get; }
        float MaximumRange { get; }

        bool TryResolveAnchor(Vector3 origin, Vector3 direction, out Vector3 point, out Transform anchorTransform);
        bool HasClearLineToTarget(Vector3 origin, Vector3 targetPoint, Collider targetCollider);
    }
}
