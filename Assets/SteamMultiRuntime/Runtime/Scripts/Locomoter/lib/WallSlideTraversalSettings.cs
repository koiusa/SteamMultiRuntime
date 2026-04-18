using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public struct WallSlideTraversalSettings : INetworkSerializable
    {
        public float WallMaxUpDot;
        public float WallSlideGravityMultiplier;
        public float WallSlideMaxFallSpeed;
        public float WallSlideMinDownSpeed;
        public int WallSlideStartContactFrames;
        public float WallSlideExitMoveOppositeNormalDot;

        public static WallSlideTraversalSettings CreateDefault()
        {
            return new WallSlideTraversalSettings
            {
                WallMaxUpDot = 0.2f,
                WallSlideGravityMultiplier = 0.5f,
                WallSlideMaxFallSpeed = 3f,
                WallSlideMinDownSpeed = 1.5f,
                WallSlideStartContactFrames = 2,
                WallSlideExitMoveOppositeNormalDot = 0.3f,
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
        }
    }
}
