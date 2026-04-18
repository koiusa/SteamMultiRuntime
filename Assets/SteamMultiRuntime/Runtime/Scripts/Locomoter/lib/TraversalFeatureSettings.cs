using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public struct TraversalFeatureSettings : INetworkSerializable
    {
        public WallRunTraversalSettings WallRun;
        public WallJumpTraversalSettings WallJump;
        public WallSlideTraversalSettings WallSlide;

        public static TraversalFeatureSettings CreateDefault()
        {
            return new TraversalFeatureSettings
            {
                WallRun = WallRunTraversalSettings.CreateDefault(),
                WallJump = WallJumpTraversalSettings.CreateDefault(),
                WallSlide = WallSlideTraversalSettings.CreateDefault(),
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref WallRun);
            serializer.SerializeValue(ref WallJump);
            serializer.SerializeValue(ref WallSlide);
        }
    }
}
