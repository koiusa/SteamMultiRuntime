using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(LadderTraversalFeature))]
    [RequireComponent(typeof(LadderClimbAction))]
    [DisallowMultipleComponent]
    public sealed class LadderDetachAction : MonoBehaviour, ILadderDetachAction
    {
        private LadderTraversalFeature feature;
        public bool IsEnabled => isActiveAndEnabled;

        private void Awake() => feature = GetComponent<LadderTraversalFeature>();

        public bool TryHandleTraversal(Vector3 velocity, Vector2 moveInput, Quaternion moveReferenceRotation,
            bool jumpRequested, bool isGrounded, Vector3 upAxis, out Vector3 nextVelocity, out bool detachedByJump)
        {
            if (!IsEnabled || feature == null)
            {
                nextVelocity = velocity;
                detachedByJump = false;
                return false;
            }
            return feature.TryHandleTraversal(velocity, moveInput, moveReferenceRotation, jumpRequested,
                isGrounded, upAxis, out nextVelocity, out detachedByJump);
        }
    }
}
