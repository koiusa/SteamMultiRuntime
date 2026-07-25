using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IWallSlideTraversalFeature
    {
        bool IsEnabled { get; }
        bool IsWallSliding { get; }
        Vector3 WallNormal { get; }

        void ResetState();
        bool TryApplyWallSlide(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, bool isWallRunning, out Vector3 nextVelocity);
    }
}
