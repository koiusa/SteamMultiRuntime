using UnityEngine;
namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(WireTraversalFeature)), DisallowMultipleComponent]
    public sealed class WireReelAction : MonoBehaviour, IWireReelAction, IWireReelDebugSnapshotSource
    {
        [SerializeField, Min(0f)] private float reelSpeed = 12f;
        [SerializeField, Min(0f)] private float stepDistance = 1.5f;
        private IWireConnection connection;
        private float input;
        private float lastLengthBeforeApply;
        private float lastLengthAfterApply;
        public bool IsEnabled => isActiveAndEnabled;
        public bool IsReelingIn => IsEnabled && input < -0.001f;
        private void Awake() => connection = GetComponent<IWireConnection>();
        private void OnValidate() { reelSpeed = Mathf.Max(0f, reelSpeed); stepDistance = Mathf.Max(0f, stepDistance); }
        WireReelDebugSnapshot IWireReelDebugSnapshotSource.GetDebugSnapshot() => new WireReelDebugSnapshot(
            input,
            IsReelingIn,
            reelSpeed,
            lastLengthBeforeApply,
            lastLengthAfterApply);
        // Reel axis convention: negative reels in, positive pays the wire out.
        public void SetInput(float value)
        {
            var wasReelingIn = IsReelingIn;
            input = Mathf.Clamp(value, -1f, 1f);
            if (!wasReelingIn && IsReelingIn && connection != null && connection.IsAttached)
            {
                // Ground traversal starts with the full available wire length. Remove that
                // invisible slack when reeling begins so the first input has an immediate effect.
                var actualLength = Vector3.Distance(connection.Body.worldCenterOfMass, connection.AnchorPoint);
                connection.SetRopeLength(Mathf.Min(connection.RopeLength, actualLength));
            }
        }
        public void ReelStep()
        {
            if (connection == null || !connection.IsAttached) return;

            lastLengthBeforeApply = connection.RopeLength;
            var actualLength = Vector3.Distance(connection.Body.worldCenterOfMass, connection.AnchorPoint);
            var tautLength = Mathf.Min(connection.RopeLength, actualLength);
            connection.SetRopeLength(tautLength - stepDistance);
            lastLengthAfterApply = connection.RopeLength;
        }
        public void ApplyReel(float deltaTime)
        {
            if (connection == null || !connection.IsAttached) return;

            lastLengthBeforeApply = connection.RopeLength;

            if (IsReelingIn)
            {
                // Keep the wire taut even when reel input was already held before attach,
                // or when player movement has created slack since the previous tick.
                var actualLength = Vector3.Distance(connection.Body.worldCenterOfMass, connection.AnchorPoint);
                connection.SetRopeLength(Mathf.Min(connection.RopeLength, actualLength));
            }

            connection.SetRopeLength(connection.RopeLength + input * reelSpeed * Mathf.Max(0f, deltaTime));
            lastLengthAfterApply = connection.RopeLength;
        }
    }
}
