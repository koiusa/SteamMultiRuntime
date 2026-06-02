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
        public float WallRunAwayFromWallMinSpeed;
        public float WallRunInputReleaseGraceTime;

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
                WallRunAwayFromWallMinSpeed = 0.15f,
                WallRunInputReleaseGraceTime = 0.2f,
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
