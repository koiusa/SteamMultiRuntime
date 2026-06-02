using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public enum WallJumpTrajectoryMode
    {
        Snappy = 0,
        Arc = 1,
    }

    [System.Serializable]
    public partial struct TraversalMotorSettings
    {
        [Unit("m/s", "壁走行時の最大速度")]
        public float WallRunSpeed;
        [Unit("m/s²", "壁走行時の加速度")]
        public float WallRunAcceleration;
        [Unit("倍率", "壁走行時の重力倍率（0.0-1.0推奨）")]
        public float WallRunGravityMultiplier;
        [Unit("m/s", "壁走行時の最大落下速度")]
        public float WallRunMaxFallSpeed;
        [Unit("ドット積", "壁走行開始に必要な入力方向の最小値（-1.0-1.0）")]
        public float WallRunMinInputDot;
        [Unit("m/s", "壁走行開始に必要な最小速度")]
        public float WallRunMinAlongWallSpeed;
        [Unit("m/s", "壁走行開始可能な最大上昇速度")]
        public float WallRunMaxUpwardStartSpeed;
        [Unit("ドット積", "壁判定の最大上方向成分（0.0-1.0）")]
        public float WallMaxUpDot;
        [Unit("m/s", "壁ジャンプの上昇力")]
        public float WallJumpUpForce;
        [Unit("m/s", "壁ジャンプの離脱力")]
        public float WallJumpAwayForce;
        [Unit("m/s", "壁ジャンプ前方蹴り力")]
        public float TriangleKickForwardForce;
        [Unit("フレーム", "壁走行開始に必要な接触フレーム数")]
        public int WallRunStartContactFrames;
        [Unit("m/s", "壁から離れる速度がこの値を超えると壁走行を開始しない")]
        public float WallRunAwayFromWallMinSpeed;
        [Unit("秒", "壁走行中に入力を離しても維持する猶予時間")]
        public float WallRunInputReleaseGraceTime;
        [Unit("倍率", "壁スライド時の重力倍率（0.0-1.0推奨）")]
        public float WallSlideGravityMultiplier;
        [Unit("m/s", "壁スライド時の最大落下速度")]
        public float WallSlideMaxFallSpeed;
        public WallJumpTrajectoryMode WallJumpTrajectoryMode;
        [Unit("秒", "同じ壁へのキック禁止時間")]
        public float SameWallKickLockDuration;
        [Unit("ドット積", "同じ壁判定の閾値（0.9-1.0推奨）")]
        public float SameWallNormalDotThreshold;
        [Unit("m/s", "壁スライド開始に必要な最小下降速度")]
        public float WallSlideMinDownSpeed;
        [Unit("フレーム", "壁スライド開始に必要な接触フレーム数")]
        public int WallSlideStartContactFrames;
        [Unit("ドット積", "壁スライド終了の入力判定閾値（-1.0-1.0）")]
        public float WallSlideExitMoveOppositeNormalDot;
        [Unit("m/s", "壁から離れる速度がこの値を超えると壁スライドを開始しない")]
        public float WallSlideAwayFromWallMinSpeed;

        public TraversalMotorSettings(
            float wallRunSpeed,
            float wallRunAcceleration,
            float wallRunGravityMultiplier,
            float wallRunMaxFallSpeed,
            float wallRunMinInputDot,
            float wallRunMinAlongWallSpeed,
            float wallRunMaxUpwardStartSpeed,
            float wallMaxUpDot,
            float wallJumpUpForce,
            float wallJumpAwayForce,
            float triangleKickForwardForce,
            int wallRunStartContactFrames,
            float wallRunAwayFromWallMinSpeed,
            float wallRunInputReleaseGraceTime,
            float wallSlideGravityMultiplier,
            float wallSlideMaxFallSpeed,
            WallJumpTrajectoryMode wallJumpTrajectoryMode,
            float sameWallKickLockDuration,
            float sameWallNormalDotThreshold,
            float wallSlideMinDownSpeed,
            int wallSlideStartContactFrames,
            float wallSlideExitMoveOppositeNormalDot,
            float wallSlideAwayFromWallMinSpeed)
        {
            WallRunSpeed = wallRunSpeed;
            WallRunAcceleration = wallRunAcceleration;
            WallRunGravityMultiplier = wallRunGravityMultiplier;
            WallRunMaxFallSpeed = wallRunMaxFallSpeed;
            WallRunMinInputDot = wallRunMinInputDot;
            WallRunMinAlongWallSpeed = wallRunMinAlongWallSpeed;
            WallRunMaxUpwardStartSpeed = wallRunMaxUpwardStartSpeed;
            WallMaxUpDot = wallMaxUpDot;
            WallJumpUpForce = wallJumpUpForce;
            WallJumpAwayForce = wallJumpAwayForce;
            TriangleKickForwardForce = triangleKickForwardForce;
            WallRunStartContactFrames = wallRunStartContactFrames;
            WallRunAwayFromWallMinSpeed = wallRunAwayFromWallMinSpeed;
            WallRunInputReleaseGraceTime = wallRunInputReleaseGraceTime;
            WallSlideGravityMultiplier = wallSlideGravityMultiplier;
            WallSlideMaxFallSpeed = wallSlideMaxFallSpeed;
            WallJumpTrajectoryMode = wallJumpTrajectoryMode;
            SameWallKickLockDuration = sameWallKickLockDuration;
            SameWallNormalDotThreshold = sameWallNormalDotThreshold;
            WallSlideMinDownSpeed = wallSlideMinDownSpeed;
            WallSlideStartContactFrames = wallSlideStartContactFrames;
            WallSlideExitMoveOppositeNormalDot = wallSlideExitMoveOppositeNormalDot;
            WallSlideAwayFromWallMinSpeed = wallSlideAwayFromWallMinSpeed;
        }

        public static TraversalMotorSettings CreateDefault()
        {
            return new TraversalMotorSettings(
                wallRunSpeed: 7f,
                wallRunAcceleration: 20f,
                wallRunGravityMultiplier: 0.35f,
                wallRunMaxFallSpeed: 2f,
                wallRunMinInputDot: 0.15f,
                wallRunMinAlongWallSpeed: 2f,
                wallRunMaxUpwardStartSpeed: 0f,
                wallMaxUpDot: 0.2f,
                wallJumpUpForce: 6.5f,
                wallJumpAwayForce: 5f,
                triangleKickForwardForce: 3f,
                wallRunStartContactFrames: 2,
                wallRunAwayFromWallMinSpeed: 0.15f,
                wallRunInputReleaseGraceTime: 0.2f,
                wallSlideGravityMultiplier: 0.5f,
                wallSlideMaxFallSpeed: 3f,
                wallJumpTrajectoryMode: WallJumpTrajectoryMode.Snappy,
                sameWallKickLockDuration: 0.2f,
                sameWallNormalDotThreshold: 0.97f,
                wallSlideMinDownSpeed: 1.5f,
                wallSlideStartContactFrames: 2,
                wallSlideExitMoveOppositeNormalDot: 0.3f,
                wallSlideAwayFromWallMinSpeed: 0.15f);
        }

            }
        }
