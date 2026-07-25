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
        public float WallTraversalBlockDuration;
        public float LateralDetachInputThreshold;
        public float GroundEnterDetachGraceTime;
        public float FacingRotationSpeed;
        public float SideViewEnterFaceAlignment;
        public float SideViewExitFaceAlignment;

        public static LadderTraversalSettingsNetData FromCore(LadderTraversalSettings s)
        {
            return new LadderTraversalSettingsNetData
            {
                ClimbSpeed = s.ClimbSpeed,
                ClimbAcceleration = s.ClimbAcceleration,
                ExitTopBoostSpeed = s.ExitTopBoostSpeed,
                DirectionalDetachReattachDelay = s.DirectionalDetachReattachDelay,
                JumpDetachReattachDelay = s.JumpDetachReattachDelay,
                WallTraversalBlockDuration = s.WallTraversalBlockDuration,
                LateralDetachInputThreshold = s.LateralDetachInputThreshold,
                GroundEnterDetachGraceTime = s.GroundEnterDetachGraceTime,
                FacingRotationSpeed = s.FacingRotationSpeed,
                SideViewEnterFaceAlignment = s.SideViewEnterFaceAlignment,
                SideViewExitFaceAlignment = s.SideViewExitFaceAlignment,
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
                WallTraversalBlockDuration = WallTraversalBlockDuration,
                LateralDetachInputThreshold = LateralDetachInputThreshold,
                GroundEnterDetachGraceTime = GroundEnterDetachGraceTime,
                FacingRotationSpeed = FacingRotationSpeed,
                SideViewEnterFaceAlignment = SideViewEnterFaceAlignment,
                SideViewExitFaceAlignment = SideViewExitFaceAlignment,
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClimbSpeed);
            serializer.SerializeValue(ref ClimbAcceleration);
            serializer.SerializeValue(ref ExitTopBoostSpeed);
            serializer.SerializeValue(ref DirectionalDetachReattachDelay);
            serializer.SerializeValue(ref JumpDetachReattachDelay);
            serializer.SerializeValue(ref WallTraversalBlockDuration);
            serializer.SerializeValue(ref LateralDetachInputThreshold);
            serializer.SerializeValue(ref GroundEnterDetachGraceTime);
            serializer.SerializeValue(ref FacingRotationSpeed);
            serializer.SerializeValue(ref SideViewEnterFaceAlignment);
            serializer.SerializeValue(ref SideViewExitFaceAlignment);
        }
    }
}
