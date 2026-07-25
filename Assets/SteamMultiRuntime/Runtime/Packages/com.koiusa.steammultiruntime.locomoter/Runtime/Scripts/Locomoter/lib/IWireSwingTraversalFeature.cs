using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Contract for a wire-swing traversal feature. Consumers such as player
    /// controllers, AI, and network authority code can use this interface
    /// without depending on the concrete MonoBehaviour implementation.
    /// </summary>
    public interface IWireSwingTraversalFeature
    {
        bool IsEnabled { get; }
        bool IsAttached { get; }
        Vector3 AnchorPoint { get; }
        float RopeLength { get; }

        void SetMoveDirection(Vector3 moveDirection);
        void SetGrappleInput(bool held, Vector3 origin, Vector3 aimDirection);
        void SetReelInput(float reelInput);
        void ReelByJump();
        void SetReplicatedState(bool isAttached, Vector3 anchorPoint, float ropeLength);
        bool TryAttach(Vector3 origin, Vector3 direction);
        void Attach(Vector3 worldPoint, Transform movingAnchor = null);
        void Detach(bool applyBoost = false);
    }
}
