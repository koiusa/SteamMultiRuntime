using UnityEngine;
using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    [System.Serializable]
    public struct PlayerMotorSettings : INetworkSerializable
    {
        [Unit("m/s", "移動時の最大速度")]
        public float MoveSpeed;
        [Unit("m/s²", "地面上の加速度")]
        public float GroundAcceleration;
        [Unit("m/s²", "空中の加速度")]
        public float AirAcceleration;
        [Unit("度/秒", "回転速度")]
        public float RotationSpeed;
        [Unit("m/s", "ジャンプの初速度")]
        public float JumpForce;
        [Unit("倍率", "落下時の重力倍率（1.0より大きい値推奨）")]
        public float FallMultiplier;
        [Unit("秒", "ジャンプ後の地面離脱時間")]
        public float JumpDetachDuration;
        public LayerMask GroundLayer;
        [Unit("ドット積", "地面判定の法線角度閾値（0.0-1.0）")]
        public float MinGroundNormalDot;
        [Unit("秒", "接地ロスト直後に接地維持を許可する猶予時間")]
        public float GroundedGraceTime;
        [Unit("m", "近接地面として扱う最大距離")]
        public float NearbyGroundDistance;

        [Unit("m/s", "ストライフ移動の最大速度（MoveSpeedに対する比率）")]
        public float StrafeMoveSpeedMultiplier;
        [Unit("m/s²", "ストライフ移動の加速度（GroundAccelerationに対する比率）")]
        public float StrafeAccelerationMultiplier;
        [Unit("度/秒", "ストライフ中の回転速度（0=回転しない）")]
        public float StrafeRotationSpeed;
        [Unit("倍率", "後ろ向き移動時の速度倍率（0.0-1.0推奨）")]
        public float BackwardSpeedMultiplier;

        [Unit("有効/無効", "細かい段差の自動乗り越え補正")]
        public bool EnableStepAssist;
        [Unit("m", "自動で乗り越える最大段差高さ")]
        public float StepAssistMaxHeight;
        [Unit("m", "段差判定の前方チェック距離")]
        public float StepAssistCheckDistance;
        [Unit("m/s", "段差補正を開始する最小水平速度")]
        public float StepAssistMinMoveSpeed;
        [Unit("ドット積", "段差として扱う障害物の最大上方向成分（0.0-1.0）")]
        public float StepAssistObstacleUpDot;

        public PlayerMotorSettings(
            float moveSpeed,
            float groundAcceleration,
            float airAcceleration,
            float rotationSpeed,
            float jumpForce,
            float fallMultiplier,
            float jumpDetachDuration,
            LayerMask groundLayer,
            float minGroundNormalDot,
            float groundedGraceTime = 0.08f,
            float nearbyGroundDistance = 0.3f,
            float strafeMoveSpeedMultiplier = 0.8f,
            float strafeAccelerationMultiplier = 0.8f,
            float strafeRotationSpeed = 0f,
            float backwardSpeedMultiplier = 0.6f,
            bool enableStepAssist = true,
            float stepAssistMaxHeight = 0.28f,
            float stepAssistCheckDistance = 0.35f,
            float stepAssistMinMoveSpeed = 1f,
            float stepAssistObstacleUpDot = 0.35f)
        {
            MoveSpeed = moveSpeed;
            GroundAcceleration = groundAcceleration;
            AirAcceleration = airAcceleration;
            RotationSpeed = rotationSpeed;
            JumpForce = jumpForce;
            FallMultiplier = fallMultiplier;
            JumpDetachDuration = jumpDetachDuration;
            GroundLayer = groundLayer;
            MinGroundNormalDot = minGroundNormalDot;
            GroundedGraceTime = groundedGraceTime;
            NearbyGroundDistance = nearbyGroundDistance;
            StrafeMoveSpeedMultiplier = strafeMoveSpeedMultiplier;
            StrafeAccelerationMultiplier = strafeAccelerationMultiplier;
            StrafeRotationSpeed = strafeRotationSpeed;
            BackwardSpeedMultiplier = backwardSpeedMultiplier;
            EnableStepAssist = enableStepAssist;
            StepAssistMaxHeight = stepAssistMaxHeight;
            StepAssistCheckDistance = stepAssistCheckDistance;
            StepAssistMinMoveSpeed = stepAssistMinMoveSpeed;
            StepAssistObstacleUpDot = stepAssistObstacleUpDot;
        }

        public static PlayerMotorSettings CreateDefault()
        {
            return new PlayerMotorSettings(
                moveSpeed: 5f,
                groundAcceleration: 30f,
                airAcceleration: 10f,
                rotationSpeed: 720f,
                jumpForce: 6f,
                fallMultiplier: 2f,
                jumpDetachDuration: 0.1f,
                groundLayer: LayerMask.GetMask("Default"),
                minGroundNormalDot: 0.5f,
                groundedGraceTime: 0.08f,
                nearbyGroundDistance: 0.3f,
                strafeMoveSpeedMultiplier: 0.7f,
                strafeAccelerationMultiplier: 1.5f,
                strafeRotationSpeed: 300f,
                backwardSpeedMultiplier: 0.5f,
                enableStepAssist: true,
                stepAssistMaxHeight: 0.28f,
                stepAssistCheckDistance: 0.35f,
                stepAssistMinMoveSpeed: 1f,
                stepAssistObstacleUpDot: 0.35f);
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
