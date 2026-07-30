using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// <see cref="ActorMotorSettings"/> のネットワーク同期用ラッパー。
    /// INetworkSerializable はネットコードパッケージ側で実装し、コアパッケージを Netcode フリーに保つ。
    /// </summary>
    internal struct ActorMotorSettingsNetData : INetworkSerializable
    {
        public float MoveSpeed;
        public float GroundAcceleration;
        public float AirAcceleration;
        public float RotationSpeed;
        public float JumpForce;
        public float FallMultiplier;
        public float JumpDetachDuration;
        public float MinGroundNormalDot;
        public float GroundedGraceTime;
        public float NearbyGroundDistance;
        public float StrafeMoveSpeedMultiplier;
        public float StrafeAccelerationMultiplier;
        public float StrafeRotationSpeed;
        public float BackwardSpeedMultiplier;
        public bool EnableStepAssist;
        public float StepAssistMaxHeight;
        public float StepAssistCheckDistance;
        public float StepAssistMinMoveSpeed;
        public float StepAssistObstacleUpDot;

        public static ActorMotorSettingsNetData FromCore(ActorMotorSettings s)
        {
            return new ActorMotorSettingsNetData
            {
                MoveSpeed = s.MoveSpeed,
                GroundAcceleration = s.GroundAcceleration,
                AirAcceleration = s.AirAcceleration,
                RotationSpeed = s.RotationSpeed,
                JumpForce = s.JumpForce,
                FallMultiplier = s.FallMultiplier,
                JumpDetachDuration = s.JumpDetachDuration,
                MinGroundNormalDot = s.MinGroundNormalDot,
                GroundedGraceTime = s.GroundedGraceTime,
                NearbyGroundDistance = s.NearbyGroundDistance,
                StrafeMoveSpeedMultiplier = s.StrafeMoveSpeedMultiplier,
                StrafeAccelerationMultiplier = s.StrafeAccelerationMultiplier,
                StrafeRotationSpeed = s.StrafeRotationSpeed,
                BackwardSpeedMultiplier = s.BackwardSpeedMultiplier,
                EnableStepAssist = s.EnableStepAssist,
                StepAssistMaxHeight = s.StepAssistMaxHeight,
                StepAssistCheckDistance = s.StepAssistCheckDistance,
                StepAssistMinMoveSpeed = s.StepAssistMinMoveSpeed,
                StepAssistObstacleUpDot = s.StepAssistObstacleUpDot,
            };
        }

        public ActorMotorSettings ToCore(UnityEngine.LayerMask groundLayer)
        {
            return new ActorMotorSettings(
                moveSpeed: MoveSpeed,
                groundAcceleration: GroundAcceleration,
                airAcceleration: AirAcceleration,
                rotationSpeed: RotationSpeed,
                jumpForce: JumpForce,
                fallMultiplier: FallMultiplier,
                jumpDetachDuration: JumpDetachDuration,
                groundLayer: groundLayer,
                minGroundNormalDot: MinGroundNormalDot,
                groundedGraceTime: GroundedGraceTime,
                nearbyGroundDistance: NearbyGroundDistance,
                strafeMoveSpeedMultiplier: StrafeMoveSpeedMultiplier,
                strafeAccelerationMultiplier: StrafeAccelerationMultiplier,
                strafeRotationSpeed: StrafeRotationSpeed,
                backwardSpeedMultiplier: BackwardSpeedMultiplier,
                enableStepAssist: EnableStepAssist,
                stepAssistMaxHeight: StepAssistMaxHeight,
                stepAssistCheckDistance: StepAssistCheckDistance,
                stepAssistMinMoveSpeed: StepAssistMinMoveSpeed,
                stepAssistObstacleUpDot: StepAssistObstacleUpDot);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MoveSpeed);
            serializer.SerializeValue(ref GroundAcceleration);
            serializer.SerializeValue(ref AirAcceleration);
            serializer.SerializeValue(ref RotationSpeed);
            serializer.SerializeValue(ref JumpForce);
            serializer.SerializeValue(ref FallMultiplier);
            serializer.SerializeValue(ref JumpDetachDuration);
            serializer.SerializeValue(ref MinGroundNormalDot);
            serializer.SerializeValue(ref GroundedGraceTime);
            serializer.SerializeValue(ref NearbyGroundDistance);
            serializer.SerializeValue(ref StrafeMoveSpeedMultiplier);
            serializer.SerializeValue(ref StrafeAccelerationMultiplier);
            serializer.SerializeValue(ref StrafeRotationSpeed);
            serializer.SerializeValue(ref BackwardSpeedMultiplier);
            serializer.SerializeValue(ref EnableStepAssist);
            serializer.SerializeValue(ref StepAssistMaxHeight);
            serializer.SerializeValue(ref StepAssistCheckDistance);
            serializer.SerializeValue(ref StepAssistMinMoveSpeed);
            serializer.SerializeValue(ref StepAssistObstacleUpDot);
        }
    }
}
