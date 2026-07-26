using UnityEngine;
namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(WireTraversalFeature)), RequireComponent(typeof(WireGrappleTargetingFeature)), DisallowMultipleComponent]
    public sealed class WireAttachAction : MonoBehaviour, IWireAttachAction
    {
        private IWireConnection connection;
        private IWireGrappleTargetingFeature targeting;
        private bool blockedUntilRelease;
        public bool IsEnabled => isActiveAndEnabled;
        private void Awake() { connection = GetComponent<IWireConnection>(); targeting = GetComponent<IWireGrappleTargetingFeature>(); }
        private void OnDisable() { blockedUntilRelease = false; connection?.Detach(); }
        public void SetInput(bool held, bool fireRequested, WireAimResult aimResult)
        {
            if (!held) { blockedUntilRelease = false; connection?.Detach(); return; }
            if (!fireRequested || !aimResult.CanAttach || blockedUntilRelease || connection == null || connection.IsAttached) return;
            connection.Attach(aimResult.AttachPoint, aimResult.AnchorTransform);
        }
        public void DetachUntilInputRelease() { blockedUntilRelease = true; connection?.Detach(); }
    }
}
