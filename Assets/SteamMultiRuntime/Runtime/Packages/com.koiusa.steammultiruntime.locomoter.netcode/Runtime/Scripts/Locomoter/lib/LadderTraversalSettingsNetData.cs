using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// <see cref="LadderTraversalSettings"/> のネットワーク同期用ラッパー。
    /// </summary>
    internal struct LadderTraversalSettingsNetData : INetworkSerializable
    {
        public float ClimbSpeed;
        public float ClimbAcceleration;
        public float ExitTopBoostSpeed;
        public float DirectionalDetachReattachDelay;
        public float JumpDetachReattachDelay;

        public static LadderTraversalSettingsNetData FromCore(LadderTraversalSettings s)
        {
            return new LadderTraversalSettingsNetData
            {
                ClimbSpeed = s.ClimbSpeed,
                ClimbAcceleration = s.ClimbAcceleration,
                ExitTopBoostSpeed = s.ExitTopBoostSpeed,
                DirectionalDetachReattachDelay = s.DirectionalDetachReattachDelay,
                JumpDetachReattachDelay = s.JumpDetachReattachDelay,
            };
        }

        public LadderTraversalSettings ToCore()
        {
            return new LadderTraversalSettings
            {
                ClimbSpeed = ClimbSpeed,
                ClimbAcceleration = ClimbAcceleration,
                ExitTopBoostSpeed = ExitTopBoostSpeed,
                DirectionalDetachReattachDelay = DirectionalDetachReattachDelay,
                JumpDetachReattachDelay = JumpDetachReattachDelay,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClimbSpeed);
            serializer.SerializeValue(ref ClimbAcceleration);
            serializer.SerializeValue(ref ExitTopBoostSpeed);
            serializer.SerializeValue(ref DirectionalDetachReattachDelay);
            serializer.SerializeValue(ref JumpDetachReattachDelay);
        }
    }
}
