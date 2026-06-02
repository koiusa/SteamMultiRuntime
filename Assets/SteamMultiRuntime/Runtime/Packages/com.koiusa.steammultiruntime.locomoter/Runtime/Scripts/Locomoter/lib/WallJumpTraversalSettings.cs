using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public struct WallJumpTraversalSettings : INetworkSerializable
    {
        public float WallMaxUpDot;
        public float WallJumpUpForce;
        public float WallJumpAwayForce;
        public float TriangleKickForwardForce;
        public WallJumpTrajectoryMode WallJumpTrajectoryMode;
        public float SameWallKickLockDuration;
        public float SameWallNormalDotThreshold;

        public static WallJumpTraversalSettings CreateDefault()
        {
            return new WallJumpTraversalSettings
            {
                WallMaxUpDot = 0.2f,
                WallJumpUpForce = 6.5f,
                WallJumpAwayForce = 5f,
                TriangleKickForwardForce = 3f,
                WallJumpTrajectoryMode = WallJumpTrajectoryMode.Snappy,
                SameWallKickLockDuration = 0.2f,
                SameWallNormalDotThreshold = 0.97f,
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
