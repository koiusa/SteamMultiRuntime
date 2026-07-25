using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IWallRunTraversalFeature
    {
        bool IsEnabled { get; }
        bool IsWallRunning { get; }
        Vector3 WallNormal { get; }

        void ResetState();
        void NotifyWallJump();
        bool TryAccelerateOnWall(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, out Vector3 nextVelocity);
        Vector3 ApplyVerticalMotion(Vector3 velocity, Vector3 upAxis);
    }
}
