using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// <see cref="TraversalFeatureSettings"/> のネットワーク同期用ラッパー。
    /// </summary>
    internal struct TraversalFeatureSettingsNetData : INetworkSerializable
    {
        public WallRunTraversalSettings WallRun;
        public WallJumpTraversalSettings WallJump;
        public WallSlideTraversalSettings WallSlide;
        public float LadderClimbSpeed;
        public float LadderClimbAcceleration;
        public float LadderExitTopBoostSpeed;
        public float LadderDirectionalDetachReattachDelay;
        public float LadderJumpDetachReattachDelay;

        public static TraversalFeatureSettingsNetData FromCore(TraversalFeatureSettings s)
        {
            return new TraversalFeatureSettingsNetData
            {
                WallRun = s.WallRun,
                WallJump = s.WallJump,
                WallSlide = s.WallSlide,
                LadderClimbSpeed = s.Ladder.ClimbSpeed,
                LadderClimbAcceleration = s.Ladder.ClimbAcceleration,
                LadderExitTopBoostSpeed = s.Ladder.ExitTopBoostSpeed,
                LadderDirectionalDetachReattachDelay = s.Ladder.DirectionalDetachReattachDelay,
                LadderJumpDetachReattachDelay = s.Ladder.JumpDetachReattachDelay,
            };
        }

        public TraversalFeatureSettings ToCore()
        {
            return new TraversalFeatureSettings
            {
                WallRun = WallRun,
                WallJump = WallJump,
                WallSlide = WallSlide,
                Ladder = new LadderTraversalSettings
                {
                    ClimbSpeed = LadderClimbSpeed,
                    ClimbAcceleration = LadderClimbAcceleration,
                    ExitTopBoostSpeed = LadderExitTopBoostSpeed,
                    DirectionalDetachReattachDelay = LadderDirectionalDetachReattachDelay,
                    JumpDetachReattachDelay = LadderJumpDetachReattachDelay,
                },
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref WallRun.WallRunSpeed);
            serializer.SerializeValue(ref WallRun.WallRunAcceleration);
            serializer.SerializeValue(ref WallRun.WallRunGravityMultiplier);
            serializer.SerializeValue(ref WallRun.WallRunMaxFallSpeed);
            serializer.SerializeValue(ref WallRun.WallRunMinInputDot);
            serializer.SerializeValue(ref WallRun.WallRunMinAlongWallSpeed);
            serializer.SerializeValue(ref WallRun.WallRunMaxUpwardStartSpeed);
            serializer.SerializeValue(ref WallRun.WallMaxUpDot);
            serializer.SerializeValue(ref WallRun.WallRunStartContactFrames);
            serializer.SerializeValue(ref WallRun.WallRunAwayFromWallMinSpeed);
            serializer.SerializeValue(ref WallRun.WallRunInputReleaseGraceTime);
            serializer.SerializeValue(ref WallJump.WallMaxUpDot);
            serializer.SerializeValue(ref WallJump.WallJumpUpForce);
            serializer.SerializeValue(ref WallJump.WallJumpAwayForce);
            serializer.SerializeValue(ref WallJump.TriangleKickForwardForce);
            serializer.SerializeValue(ref WallJump.WallJumpTrajectoryMode);
            serializer.SerializeValue(ref WallJump.SameWallKickLockDuration);
            serializer.SerializeValue(ref WallJump.SameWallNormalDotThreshold);
            serializer.SerializeValue(ref WallSlide.WallMaxUpDot);
            serializer.SerializeValue(ref WallSlide.WallSlideGravityMultiplier);
            serializer.SerializeValue(ref WallSlide.WallSlideMaxFallSpeed);
            serializer.SerializeValue(ref WallSlide.WallSlideMinDownSpeed);
            serializer.SerializeValue(ref WallSlide.WallSlideStartContactFrames);
            serializer.SerializeValue(ref WallSlide.WallSlideExitMoveOppositeNormalDot);
            serializer.SerializeValue(ref WallSlide.WallSlideAwayFromWallMinSpeed);
            serializer.SerializeValue(ref LadderClimbSpeed);
            serializer.SerializeValue(ref LadderClimbAcceleration);
            serializer.SerializeValue(ref LadderExitTopBoostSpeed);
            serializer.SerializeValue(ref LadderDirectionalDetachReattachDelay);
            serializer.SerializeValue(ref LadderJumpDetachReattachDelay);
        }
    }
}
