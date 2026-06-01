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
        /// <param name="moveDirection">入力方向。</param>
        /// <param name="upAxis">上方向。</param>
        /// <param name="nextVelocity">次フレームに適用する速度。</param>
        /// <returns>梯子昇降を処理した場合 true。</returns>
        bool TryApplyLadderMovement(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, out Vector3 nextVelocity);

        /// <summary>
        /// 外部から梯子ゾーンへの侵入を通知する。
        /// </summary>
        void NotifyEnterLadder(LadderVolume ladder);

        /// <summary>
        /// 外部から梯子ゾーンからの離脱を通知する。
        /// </summary>
        void NotifyExitLadder(LadderVolume ladder);
    }
}
