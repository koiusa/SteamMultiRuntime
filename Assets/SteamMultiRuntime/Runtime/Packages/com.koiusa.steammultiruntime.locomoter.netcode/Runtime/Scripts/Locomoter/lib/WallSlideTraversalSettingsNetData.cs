using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// <see cref="WallSlideTraversalSettings"/> のネットワーク同期用ラッパー。
    /// </summary>
    internal struct WallSlideTraversalSettingsNetData : INetworkSerializable
    {
        public float WallMaxUpDot;
        public float WallSlideGravityMultiplier;
        public float WallSlideMaxFallSpeed;
        public float WallSlideMinDownSpeed;
        public int WallSlideStartContactFrames;
        public float WallSlideExitMoveOppositeNormalDot;
        public float WallSlideAwayFromWallMinSpeed;

        public static WallSlideTraversalSettingsNetData FromCore(WallSlideTraversalSettings s)
        {
            return new WallSlideTraversalSettingsNetData
            {
                WallMaxUpDot = s.WallMaxUpDot,
                WallSlideGravityMultiplier = s.WallSlideGravityMultiplier,
                WallSlideMaxFallSpeed = s.WallSlideMaxFallSpeed,
                WallSlideMinDownSpeed = s.WallSlideMinDownSpeed,
                WallSlideStartContactFrames = s.WallSlideStartContactFrames,
                WallSlideExitMoveOppositeNormalDot = s.WallSlideExitMoveOppositeNormalDot,
                WallSlideAwayFromWallMinSpeed = s.WallSlideAwayFromWallMinSpeed,
            };
        }

        public WallSlideTraversalSettings ToCore()
        {
            return new WallSlideTraversalSettings
            {
                WallMaxUpDot = WallMaxUpDot,
                WallSlideGravityMultiplier = WallSlideGravityMultiplier,
                WallSlideMaxFallSpeed = WallSlideMaxFallSpeed,
                WallSlideMinDownSpeed = WallSlideMinDownSpeed,
                WallSlideStartContactFrames = WallSlideStartContactFrames,
                WallSlideExitMoveOppositeNormalDot = WallSlideExitMoveOppositeNormalDot,
                WallSlideAwayFromWallMinSpeed = WallSlideAwayFromWallMinSpeed,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref WallMaxUpDot);
            serializer.SerializeValue(ref WallSlideGravityMultiplier);
            serializer.SerializeValue(ref WallSlideMaxFallSpeed);
            serializer.SerializeValue(ref WallSlideMinDownSpeed);
            serializer.SerializeValue(ref WallSlideStartContactFrames);
            serializer.SerializeValue(ref WallSlideExitMoveOppositeNormalDot);
            serializer.SerializeValue(ref WallSlideAwayFromWallMinSpeed);
        }
    }
}
