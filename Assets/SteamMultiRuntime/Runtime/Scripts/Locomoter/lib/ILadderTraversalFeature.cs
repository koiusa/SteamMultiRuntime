using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface ILadderTraversalFeature
    {
        bool IsEnabled { get; }
        bool IsOnLadder { get; }

        void ResetState();

        /// <summary>
        /// 梯子昇降中の速度を計算して適用する。
        /// </summary>
        /// <param name="velocity">現在の速度。</param>
        /// <param name="climbInput">昇降入力（+1: 上、-1: 下）。</param>
        /// <param name="upAxis">上方向。</param>
        /// <param name="nextVelocity">次フレームに適用する速度。</param>
        /// <returns>梯子昇降を処理した場合 true。</returns>
        bool TryApplyLadderMovement(Vector3 velocity, float climbInput, Vector3 upAxis, out Vector3 nextVelocity);

        /// <summary>
        /// 梯子中の入力・接地状態を解釈して、離脱または昇降の適用を行う。
        /// </summary>
        /// <returns>梯子処理を消費した場合 true。</returns>
        bool TryHandleTraversal(Vector3 velocity, Vector2 moveInput, bool jumpRequested, bool isGrounded, Vector3 upAxis, out Vector3 nextVelocity, out bool detachedByJump);

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
}
