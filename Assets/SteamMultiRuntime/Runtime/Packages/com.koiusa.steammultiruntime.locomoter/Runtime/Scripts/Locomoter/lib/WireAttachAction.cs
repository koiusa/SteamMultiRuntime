using UnityEngine;
namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(WireConnection)), RequireComponent(typeof(WireGrappleTargetingFeature)), DisallowMultipleComponent]
    public sealed class WireAttachAction : MonoBehaviour, IWireAttachAction
    {
        private IWireConnection connection;
        private IWireGrappleTargetingFeature targeting;
        private bool blockedUntilRelease;
        public bool IsEnabled => isActiveAndEnabled;
        private void Awake() { connection = GetComponent<IWireConnection>(); targeting = GetComponent<IWireGrappleTargetingFeature>(); }
        private void OnDisable() { blockedUntilRelease = false; connection?.Detach(); }
        public void SetInput(bool held, Vector3 origin, Vector3 aimDirection)
        {
            if (!held) { blockedUntilRelease = false; connection?.Detach(); return; }
            if (blockedUntilRelease || connection == null || connection.IsAttached || targeting == null || !targeting.IsEnabled) return;
            if (targeting.TryResolveAnchor(origin, aimDirection, out var point, out var anchor)) connection.Attach(point, anchor);
        }
        public void DetachUntilInputRelease() { blockedUntilRelease = true; connection?.Detach(); }
    }
}
