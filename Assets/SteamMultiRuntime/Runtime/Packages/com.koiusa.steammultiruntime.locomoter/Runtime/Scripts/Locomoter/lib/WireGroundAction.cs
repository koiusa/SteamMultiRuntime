using UnityEngine;
namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(WireConnection)), RequireComponent(typeof(WireAttachAction)), DisallowMultipleComponent]
    public sealed class WireGroundAction : MonoBehaviour, IWireGroundAction
    {
        private IWireConnection connection; private IWireAttachAction attachAction; private SlopeContactResolver ground;
        public bool IsEnabled => isActiveAndEnabled;
        public bool BlocksSwing => IsEnabled && ground != null && ground.IsGrounded;
        private void Awake() { connection = GetComponent<IWireConnection>(); attachAction = GetComponent<IWireAttachAction>(); ground = GetComponent<SlopeContactResolver>(); }
        public bool HandleJump(bool jumpRequested, bool isGrounded)
        {
            if (!jumpRequested || !isGrounded || connection == null || !connection.IsAttached) return false;
            attachAction?.DetachUntilInputRelease();
            return true;
        }
    }
}
