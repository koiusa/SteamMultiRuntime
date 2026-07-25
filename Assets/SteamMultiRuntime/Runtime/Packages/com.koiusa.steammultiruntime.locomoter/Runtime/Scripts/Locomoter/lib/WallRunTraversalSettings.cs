using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public enum WallRunVerticalMotionMode
    {
        Arc = 0,
        MaintainHeight = 1,
        Gravity = 2,
    }

    [System.Serializable]
    public partial struct WallRunTraversalSettings
    {
        public float WallRunSpeed;
        public float WallRunAcceleration;
        public float WallRunGravityMultiplier;
        public float WallRunMaxFallSpeed;
        public float WallMaxUpDot;
        public int WallRunStartContactFrames;
        public float WallRunAwayFromWallMinSpeed;
        public float WallRunInputReleaseGraceTime;
        [Tooltip("WallRun中の垂直移動方式")]
        public WallRunVerticalMotionMode VerticalMotionMode;
        [Min(0f), Tooltip("MaintainHeightで上下速度を0へ近づける加速度")]
        public float HeightHoldAcceleration;
        [Min(0f), Tooltip("Arc開始時に与える上向き速度")]
        public float ArcInitialUpSpeed;
        [Min(0f), Tooltip("Arc中の重力倍率")]
        public float ArcGravityMultiplier;
        [Min(0f), Tooltip("WallRun開始に必要な実際の壁沿い水平速度")]
        public float EnterMinimumAlongWallSpeed;
        [Min(0f), Tooltip("WallRun継続に必要な実際の壁沿い水平速度")]
        public float MaintainMinimumAlongWallSpeed;
        [Range(0f, 1f), Tooltip("WallRun開始時に水平速度のうち壁沿い成分が占める必要割合")]
        public float EnterMinimumAlongWallRatio;
        [Range(0f, 1f), Tooltip("WallRun継続時に水平速度のうち壁沿い成分が占める必要割合")]
        public float MaintainMinimumAlongWallRatio;
        [Range(0f, 1f), Tooltip("この値を超えて壁から離れる入力でWallRunを解除")]
        public float ExitAwayInputDot;
        [Range(0f, 1f), Tooltip("WallRun判定に必要な入力の最小強度")]
        public float MinimumMoveInputMagnitude;

        public static WallRunTraversalSettings CreateDefault()
        {
            return new WallRunTraversalSettings
            {
                WallRunSpeed = 7f,
                WallRunAcceleration = 20f,
                WallRunGravityMultiplier = 0.35f,
                WallRunMaxFallSpeed = 2f,
                WallMaxUpDot = 0.2f,
                WallRunStartContactFrames = 2,
                WallRunAwayFromWallMinSpeed = 0.15f,
                WallRunInputReleaseGraceTime = 0.2f,
                VerticalMotionMode = WallRunVerticalMotionMode.Arc,
                HeightHoldAcceleration = 12f,
                ArcInitialUpSpeed = 1.5f,
                ArcGravityMultiplier = 0.45f,
                EnterMinimumAlongWallSpeed = 4f,
                MaintainMinimumAlongWallSpeed = 4f,
                EnterMinimumAlongWallRatio = 0.45f,
                MaintainMinimumAlongWallRatio = 0.25f,
                ExitAwayInputDot = 0.25f,
                MinimumMoveInputMagnitude = 0.25f,
            };
        }
    }
}
