using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public struct LadderTraversalSettings
    {
        /// <summary>梯子昇降速度（Units/秒）。</summary>
        public float ClimbSpeed;

        /// <summary>梯子昇降加速度。</summary>
        public float ClimbAcceleration;

        /// <summary>梯子の上端に到達したときに与える射出速度。</summary>
        public float ExitTopBoostSpeed;

        /// <summary>意図的離脱後の再捕捉抑制秒数（横入力・下降離脱用）。</summary>
        public float DirectionalDetachReattachDelay;

        /// <summary>ジャンプ離脱後の再捕捉抑制秒数。</summary>
        public float JumpDetachReattachDelay;

        /// <summary>梯子離脱後にWallRun/WallSlideを禁止する秒数。</summary>
        [Min(0f), Tooltip("梯子離脱後にWallRun/WallSlideを禁止する秒数")]
        public float WallTraversalBlockDuration;
        [Range(0f, 1f), Tooltip("横方向入力で梯子を離脱する閾値")]
        public float LateralDetachInputThreshold;
        [Min(0f), Tooltip("地上から梯子へ入った直後の自動離脱防止時間")]
        public float GroundEnterDetachGraceTime;
        [Min(0f), Tooltip("梯子面へ正対する回転速度（度/秒）")]
        public float FacingRotationSpeed;
        [Range(0f, 1f), Tooltip("梯子面がカメラに対してこの値より横向きになったら、左右入力で昇降する")]
        public float SideViewEnterFaceAlignment;
        [Range(0f, 1f), Tooltip("左右昇降中、梯子面がこの値より正面向きになったら上下昇降に戻す")]
        public float SideViewExitFaceAlignment;

        public static LadderTraversalSettings CreateDefault()
        {
            return new LadderTraversalSettings
            {
                ClimbSpeed = 4f,
                ClimbAcceleration = 20f,
                ExitTopBoostSpeed = 2f,
                DirectionalDetachReattachDelay = 0.15f,
                JumpDetachReattachDelay = 0.12f,
                WallTraversalBlockDuration = 0.3f,
                LateralDetachInputThreshold = 0.2f,
                GroundEnterDetachGraceTime = 0.2f,
                FacingRotationSpeed = 720f,
                SideViewEnterFaceAlignment = 0.35f,
                SideViewExitFaceAlignment = 0.55f,
            };
        }
    }
}
