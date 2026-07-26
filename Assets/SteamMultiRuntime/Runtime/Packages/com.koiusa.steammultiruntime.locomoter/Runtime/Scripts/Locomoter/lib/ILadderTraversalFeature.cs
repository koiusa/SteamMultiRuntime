using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface ILadderTraversalFeature
    {
        bool IsEnabled { get; }
        bool IsOnLadder { get; }
        float ClimbSpeed { get; }
        float WallTraversalBlockDuration { get; }

        void ResetState();

        /// <summary>
        /// 外部から梯子ゾーンへの侵入を通知する。
        /// </summary>
        void NotifyEnterLadder(LadderVolume ladder);

        /// <summary>
        /// 外部から梯子ゾーンからの離脱を通知する。
        /// </summary>
        void NotifyExitLadder(LadderVolume ladder);

        /// <summary>
        /// 意図的に梯子から離脱し、再捕捉を一時的に抑制する。
        /// </summary>
        void DetachFromLadder(float reattachDelaySeconds);
    }

    public interface ILadderClimbAction
    {
        bool IsEnabled { get; }
        bool TryApplyMovement(Vector3 velocity, float climbInput, Vector3 upAxis, out Vector3 nextVelocity);
    }

    public interface ILadderDetachAction
    {
        bool IsEnabled { get; }
        bool TryHandleTraversal(Vector3 velocity, Vector2 moveInput, Quaternion moveReferenceRotation,
            bool jumpRequested, bool isGrounded, Vector3 upAxis, out Vector3 nextVelocity, out bool detachedByJump);
    }
}
