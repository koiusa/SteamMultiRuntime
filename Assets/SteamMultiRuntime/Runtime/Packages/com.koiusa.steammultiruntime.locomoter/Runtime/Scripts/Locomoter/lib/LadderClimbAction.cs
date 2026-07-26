using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(LadderTraversalFeature))]
    [DisallowMultipleComponent]
    public sealed class LadderClimbAction : MonoBehaviour, ILadderClimbAction
    {
        private LadderTraversalFeature feature;
        public bool IsEnabled => isActiveAndEnabled;

        private void Awake() => feature = GetComponent<LadderTraversalFeature>();

        public bool TryApplyMovement(Vector3 velocity, float climbInput, Vector3 upAxis, out Vector3 nextVelocity)
        {
            if (!IsEnabled || feature == null)
            {
                nextVelocity = velocity;
                return false;
            }
            return feature.TryApplyLadderMovement(velocity, climbInput, upAxis, out nextVelocity);
        }
    }
}
