using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// <see cref="TraversalFeatureSettings"/> のネットワーク同期用ラッパー。
    /// 各TraversalSettings に対応する専用 NetData を集約します。
    /// - WallRun         → WallRunTraversalSettingsNetData
    /// - WallJump        → WallJumpTraversalSettingsNetData
    /// - WallSlide       → WallSlideTraversalSettingsNetData
    /// - Ladder          → LadderTraversalSettingsNetData
    /// </summary>
    internal struct TraversalFeatureSettingsNetData : INetworkSerializable
    {
        public WallRunTraversalSettingsNetData WallRun;
        public WallJumpTraversalSettingsNetData WallJump;
        public WallSlideTraversalSettingsNetData WallSlide;
        public LadderTraversalSettingsNetData Ladder;

        public static TraversalFeatureSettingsNetData FromCore(TraversalFeatureSettings s)
        {
            return new TraversalFeatureSettingsNetData
            {
                WallRun = WallRunTraversalSettingsNetData.FromCore(s.WallRun),
                WallJump = WallJumpTraversalSettingsNetData.FromCore(s.WallJump),
                WallSlide = WallSlideTraversalSettingsNetData.FromCore(s.WallSlide),
                Ladder = LadderTraversalSettingsNetData.FromCore(s.Ladder),
            };
        }

        public TraversalFeatureSettings ToCore()
        {
            return new TraversalFeatureSettings
            {
                WallRun = WallRun.ToCore(),
                WallJump = WallJump.ToCore(),
                WallSlide = WallSlide.ToCore(),
                Ladder = Ladder.ToCore(),
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeNetworkSerializable(ref WallRun);
            serializer.SerializeNetworkSerializable(ref WallJump);
            serializer.SerializeNetworkSerializable(ref WallSlide);
            serializer.SerializeNetworkSerializable(ref Ladder);
        }
    }
}
