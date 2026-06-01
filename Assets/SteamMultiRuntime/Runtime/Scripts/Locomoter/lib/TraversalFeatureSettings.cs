using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public struct TraversalFeatureSettings : INetworkSerializable
    {
        public WallRunTraversalSettings WallRun;
        public WallJumpTraversalSettings WallJump;
        public WallSlideTraversalSettings WallSlide;
        public LadderTraversalSettings Ladder;

        public static TraversalFeatureSettings CreateDefault()
        {
            return new TraversalFeatureSettings
            {
                WallRun = WallRunTraversalSettings.CreateDefault(),
                WallJump = WallJumpTraversalSettings.CreateDefault(),
                WallSlide = WallSlideTraversalSettings.CreateDefault(),
                Ladder = LadderTraversalSettings.CreateDefault(),
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref WallRun);
            serializer.SerializeValue(ref WallJump);
            serializer.SerializeValue(ref WallSlide);
            serializer.SerializeValue(ref Ladder.ClimbSpeed);
            serializer.SerializeValue(ref Ladder.ClimbAcceleration);
            serializer.SerializeValue(ref Ladder.ExitTopBoostSpeed);
        }
    }
}
