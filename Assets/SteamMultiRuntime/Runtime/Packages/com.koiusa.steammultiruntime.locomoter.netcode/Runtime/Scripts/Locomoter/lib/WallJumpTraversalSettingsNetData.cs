using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// <see cref="WallJumpTraversalSettings"/> のネットワーク同期用ラッパー。
    /// </summary>
    internal struct WallJumpTraversalSettingsNetData : INetworkSerializable
    {
        public float WallMaxUpDot;
        public float WallJumpUpForce;
        public float WallJumpAwayForce;
        public float TriangleKickForwardForce;
        public WallJumpTrajectoryMode WallJumpTrajectoryMode;
        public float SameWallKickLockDuration;
        public float SameWallNormalDotThreshold;

        public static WallJumpTraversalSettingsNetData FromCore(WallJumpTraversalSettings s)
        {
            return new WallJumpTraversalSettingsNetData
            {
                WallMaxUpDot = s.WallMaxUpDot,
                WallJumpUpForce = s.WallJumpUpForce,
                WallJumpAwayForce = s.WallJumpAwayForce,
                TriangleKickForwardForce = s.TriangleKickForwardForce,
                WallJumpTrajectoryMode = s.WallJumpTrajectoryMode,
                SameWallKickLockDuration = s.SameWallKickLockDuration,
                SameWallNormalDotThreshold = s.SameWallNormalDotThreshold,
            };
        }

        public WallJumpTraversalSettings ToCore()
        {
            return new WallJumpTraversalSettings
            {
                WallMaxUpDot = WallMaxUpDot,
                WallJumpUpForce = WallJumpUpForce,
                WallJumpAwayForce = WallJumpAwayForce,
                TriangleKickForwardForce = TriangleKickForwardForce,
                WallJumpTrajectoryMode = WallJumpTrajectoryMode,
                SameWallKickLockDuration = SameWallKickLockDuration,
                SameWallNormalDotThreshold = SameWallNormalDotThreshold,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref WallMaxUpDot);
            serializer.SerializeValue(ref WallJumpUpForce);
            serializer.SerializeValue(ref WallJumpAwayForce);
            serializer.SerializeValue(ref TriangleKickForwardForce);
            serializer.SerializeValue(ref WallJumpTrajectoryMode);
            serializer.SerializeValue(ref SameWallKickLockDuration);
            serializer.SerializeValue(ref SameWallNormalDotThreshold);
        }
    }
}
