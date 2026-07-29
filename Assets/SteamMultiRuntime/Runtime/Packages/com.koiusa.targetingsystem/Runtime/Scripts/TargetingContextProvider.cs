using UnityEngine;

namespace Koiusa.TargetingSystem.Runtime
{
    [DisallowMultipleComponent]
    public sealed class TargetingContextProvider : MonoBehaviour, ITargetingContextSource
    {
        [SerializeField] private Transform owner;
        [SerializeField] private Camera viewCamera;

        public bool TryGetContext(out TargetingContext context)
        {
            var resolvedOwner = owner != null ? owner : transform;
            var resolvedCamera = viewCamera != null ? viewCamera : Camera.main;
            var forward = resolvedCamera != null ? resolvedCamera.transform.forward : resolvedOwner.forward;
            var origin = resolvedCamera != null ? resolvedCamera.transform.position : resolvedOwner.position;
            context = new TargetingContext(resolvedOwner, origin, forward, resolvedCamera);
            return resolvedOwner != null;
        }
    }
}
