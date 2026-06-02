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
        public float WallRunMinInputDot;
        public float WallRunMinAlongWallSpeed;
        public float WallRunMaxUpwardStartSpeed;
        public float WallMaxUpDot;
        public int WallRunStartContactFrames;
        public float WallRunAwayFromWallMinSpeed;
        public float WallRunInputReleaseGraceTime;

        public static WallRunTraversalSettingsNetData FromCore(WallRunTraversalSettings s)
        {
            return new WallRunTraversalSettingsNetData
            {
                WallRunSpeed = s.WallRunSpeed,
                WallRunAcceleration = s.WallRunAcceleration,
                WallRunGravityMultiplier = s.WallRunGravityMultiplier,
                WallRunMaxFallSpeed = s.WallRunMaxFallSpeed,
                WallRunMinInputDot = s.WallRunMinInputDot,
                WallRunMinAlongWallSpeed = s.WallRunMinAlongWallSpeed,
                WallRunMaxUpwardStartSpeed = s.WallRunMaxUpwardStartSpeed,
                WallMaxUpDot = s.WallMaxUpDot,
                WallRunStartContactFrames = s.WallRunStartContactFrames,
                WallRunAwayFromWallMinSpeed = s.WallRunAwayFromWallMinSpeed,
                WallRunInputReleaseGraceTime = s.WallRunInputReleaseGraceTime,
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
                WallRunMinInputDot = WallRunMinInputDot,
                WallRunMinAlongWallSpeed = WallRunMinAlongWallSpeed,
                WallRunMaxUpwardStartSpeed = WallRunMaxUpwardStartSpeed,
                WallMaxUpDot = WallMaxUpDot,
                WallRunStartContactFrames = WallRunStartContactFrames,
                WallRunAwayFromWallMinSpeed = WallRunAwayFromWallMinSpeed,
                WallRunInputReleaseGraceTime = WallRunInputReleaseGraceTime,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref WallRunSpeed);
            serializer.SerializeValue(ref WallRunAcceleration);
            serializer.SerializeValue(ref WallRunGravityMultiplier);
            serializer.SerializeValue(ref WallRunMaxFallSpeed);
            serializer.SerializeValue(ref WallRunMinInputDot);
            serializer.SerializeValue(ref WallRunMinAlongWallSpeed);
            serializer.SerializeValue(ref WallRunMaxUpwardStartSpeed);
            serializer.SerializeValue(ref WallMaxUpDot);
            serializer.SerializeValue(ref WallRunStartContactFrames);
            serializer.SerializeValue(ref WallRunAwayFromWallMinSpeed);
            serializer.SerializeValue(ref WallRunInputReleaseGraceTime);
        }
    }
}
