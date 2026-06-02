using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IWallJumpTraversalFeature
    {
        bool IsEnabled { get; }

        void ResetState();
        bool TryWallJump(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, out Vector3 jumpVelocity);
    }
}
