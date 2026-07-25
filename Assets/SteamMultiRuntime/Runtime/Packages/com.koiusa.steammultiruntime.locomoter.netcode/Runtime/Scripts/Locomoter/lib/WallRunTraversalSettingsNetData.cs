using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// <see cref="WallRunTraversalSettings"/> のネットワーク同期用ラッパー。
    /// </summary>
    internal struct WallRunTraversalSettingsNetData : INetworkSerializable
    {
        public float WallRunSpeed;
        public float WallRunAcceleration;
        public float WallRunGravityMultiplier;
        public float WallRunMaxFallSpeed;
        public float WallMaxUpDot;
        public int WallRunStartContactFrames;
        public float WallRunAwayFromWallMinSpeed;
        public float WallRunInputReleaseGraceTime;
        public int VerticalMotionMode;
        public float HeightHoldAcceleration;
        public float ArcInitialUpSpeed;
        public float ArcGravityMultiplier;
        public float EnterMinimumAlongWallSpeed;
        public float MaintainMinimumAlongWallSpeed;
        public float EnterMinimumAlongWallRatio;
        public float MaintainMinimumAlongWallRatio;
        public float ExitAwayInputDot;
        public float MinimumMoveInputMagnitude;

        public static WallRunTraversalSettingsNetData FromCore(WallRunTraversalSettings s)
        {
            return new WallRunTraversalSettingsNetData
            {
                WallRunSpeed = s.WallRunSpeed,
                WallRunAcceleration = s.WallRunAcceleration,
                WallRunGravityMultiplier = s.WallRunGravityMultiplier,
                WallRunMaxFallSpeed = s.WallRunMaxFallSpeed,
                WallMaxUpDot = s.WallMaxUpDot,
                WallRunStartContactFrames = s.WallRunStartContactFrames,
                WallRunAwayFromWallMinSpeed = s.WallRunAwayFromWallMinSpeed,
                WallRunInputReleaseGraceTime = s.WallRunInputReleaseGraceTime,
                VerticalMotionMode = (int)s.VerticalMotionMode,
                HeightHoldAcceleration = s.HeightHoldAcceleration,
                ArcInitialUpSpeed = s.ArcInitialUpSpeed,
                ArcGravityMultiplier = s.ArcGravityMultiplier,
                EnterMinimumAlongWallSpeed = s.EnterMinimumAlongWallSpeed,
                MaintainMinimumAlongWallSpeed = s.MaintainMinimumAlongWallSpeed,
                EnterMinimumAlongWallRatio = s.EnterMinimumAlongWallRatio,
                MaintainMinimumAlongWallRatio = s.MaintainMinimumAlongWallRatio,
                ExitAwayInputDot = s.ExitAwayInputDot,
                MinimumMoveInputMagnitude = s.MinimumMoveInputMagnitude,
            };
        }

        public WallRunTraversalSettings ToCore()
        {
            return new WallRunTraversalSettings
            {
                WallRunSpeed = WallRunSpeed,
                WallRunAcceleration = WallRunAcceleration,
                WallRunGravityMultiplier = WallRunGravityMultiplier,
                WallRunMaxFallSpeed = WallRunMaxFallSpeed,
                WallMaxUpDot = WallMaxUpDot,
                WallRunStartContactFrames = WallRunStartContactFrames,
                WallRunAwayFromWallMinSpeed = WallRunAwayFromWallMinSpeed,
                WallRunInputReleaseGraceTime = WallRunInputReleaseGraceTime,
                VerticalMotionMode = (WallRunVerticalMotionMode)VerticalMotionMode,
                HeightHoldAcceleration = HeightHoldAcceleration,
                ArcInitialUpSpeed = ArcInitialUpSpeed,
                ArcGravityMultiplier = ArcGravityMultiplier,
                EnterMinimumAlongWallSpeed = EnterMinimumAlongWallSpeed,
                MaintainMinimumAlongWallSpeed = MaintainMinimumAlongWallSpeed,
                EnterMinimumAlongWallRatio = EnterMinimumAlongWallRatio,
                MaintainMinimumAlongWallRatio = MaintainMinimumAlongWallRatio,
                ExitAwayInputDot = ExitAwayInputDot,
                MinimumMoveInputMagnitude = MinimumMoveInputMagnitude,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref WallRunSpeed);
            serializer.SerializeValue(ref WallRunAcceleration);
            serializer.SerializeValue(ref WallRunGravityMultiplier);
            serializer.SerializeValue(ref WallRunMaxFallSpeed);
            serializer.SerializeValue(ref WallMaxUpDot);
            serializer.SerializeValue(ref WallRunStartContactFrames);
            serializer.SerializeValue(ref WallRunAwayFromWallMinSpeed);
            serializer.SerializeValue(ref WallRunInputReleaseGraceTime);
            serializer.SerializeValue(ref VerticalMotionMode);
            serializer.SerializeValue(ref HeightHoldAcceleration);
            serializer.SerializeValue(ref ArcInitialUpSpeed);
            serializer.SerializeValue(ref ArcGravityMultiplier);
            serializer.SerializeValue(ref EnterMinimumAlongWallSpeed);
            serializer.SerializeValue(ref MaintainMinimumAlongWallSpeed);
            serializer.SerializeValue(ref EnterMinimumAlongWallRatio);
            serializer.SerializeValue(ref MaintainMinimumAlongWallRatio);
            serializer.SerializeValue(ref ExitAwayInputDot);
            serializer.SerializeValue(ref MinimumMoveInputMagnitude);
        }
    }
}
