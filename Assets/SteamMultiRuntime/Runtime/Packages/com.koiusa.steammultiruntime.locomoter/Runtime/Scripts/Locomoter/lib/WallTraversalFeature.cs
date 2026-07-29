using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IWallTraversalFeature
    {
        bool IsEnabled { get; }
        bool TryGetWallNormal(Vector3 upAxis, float wallMaxUpDot, out Vector3 wallNormal);
    }

    /// <summary>Owns wall contact resolution shared by all wall actions.</summary>
    [RequireComponent(typeof(SlopeContactResolver))]
    [DisallowMultipleComponent]
    public sealed class WallTraversalFeature : MonoBehaviour, IWallTraversalFeature, IWallTraversalDebugSnapshotSource
    {
        private SlopeContactResolver contacts;

        public bool IsEnabled => isActiveAndEnabled;
        WallTraversalDebugSnapshot IWallTraversalDebugSnapshotSource.GetDebugSnapshot()
        {
            var upAxis = Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
            var hasNormal = TryGetWallNormal(upAxis, 1f, out var wallNormal);
            return new WallTraversalDebugSnapshot(
                contacts != null && contacts.HasObstacleContact,
                hasNormal,
                wallNormal);
        }

        private void Awake()
        {
            contacts = GetComponent<SlopeContactResolver>();
            if (contacts == null)
            {
                Debug.LogError("WallTraversalFeature requires SlopeContactResolver.", this);
                enabled = false;
            }
        }

        public bool TryGetWallNormal(Vector3 upAxis, float wallMaxUpDot, out Vector3 wallNormal)
        {
            if (!IsEnabled || contacts == null)
            {
                wallNormal = Vector3.zero;
                return false;
            }

            return contacts.TryGetObstacleNormal(upAxis, wallMaxUpDot, out wallNormal);
        }
    }
}
