using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public struct WallRunTraversalSettings : INetworkSerializable
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

        public static WallRunTraversalSettings CreateDefault()
        {
            return new WallRunTraversalSettings
            {
                WallRunSpeed = 7f,
                WallRunAcceleration = 20f,
                WallRunGravityMultiplier = 0.35f,
                WallRunMaxFallSpeed = 2f,
                WallRunMinInputDot = 0.15f,
                WallRunMinAlongWallSpeed = 2f,
                WallRunMaxUpwardStartSpeed = 0f,
                WallMaxUpDot = 0.2f,
                WallRunStartContactFrames = 2,
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
        }
    }
}
