using UnityEngine;
using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    internal struct PlayerInputSyncState : INetworkSerializable
    {
        public Vector3 MoveDirection;
        public Vector2 MoveInput;
        public Quaternion MoveReferenceRotation;
        public int JumpToken;
        public bool IsStrafeMode;

        public PlayerInputSyncState(Vector3 moveDirection, Vector2 moveInput, Quaternion moveReferenceRotation, int jumpToken, bool isStrafeMode)
        {
            MoveDirection = moveDirection;
            MoveInput = moveInput;
            MoveReferenceRotation = moveReferenceRotation;
            JumpToken = jumpToken;
            IsStrafeMode = isStrafeMode;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MoveDirection);
            serializer.SerializeValue(ref MoveInput);
            serializer.SerializeValue(ref MoveReferenceRotation);
            serializer.SerializeValue(ref JumpToken);
            serializer.SerializeValue(ref IsStrafeMode);
        }
    }

    internal struct PlayerKinematicState : INetworkSerializable
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float HorizontalVelocity;
        public float VerticalVelocity;

        public PlayerKinematicState(Vector3 position, Quaternion rotation, float horizontalVelocity, float verticalVelocity)
        {
            Position = position;
            Rotation = rotation;
            HorizontalVelocity = horizontalVelocity;
            VerticalVelocity = verticalVelocity;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref HorizontalVelocity);
            serializer.SerializeValue(ref VerticalVelocity);
        }
    }

    internal struct PlayerMovementFlagsState : INetworkSerializable
    {
        public bool IsGrounded;
        public bool IsJumping;
        public bool IsFreefall;
        public bool IsFallingAfterJump;
        public bool IsOnLadder;
        public float LadderSpeed;
        public bool IsWallRunning;
        public Vector3 WallNormal;

        public PlayerMovementFlagsState(bool isGrounded, bool isJumping, bool isFreefall, bool isFallingAfterJump, bool isOnLadder, float ladderSpeed, bool isWallRunning, Vector3 wallNormal)
        {
            IsGrounded = isGrounded;
            IsJumping = isJumping;
            IsFreefall = isFreefall;
            IsFallingAfterJump = isFallingAfterJump;
            IsOnLadder = isOnLadder;
            LadderSpeed = ladderSpeed;
            IsWallRunning = isWallRunning;
            WallNormal = wallNormal;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsGrounded);
            serializer.SerializeValue(ref IsJumping);
            serializer.SerializeValue(ref IsFreefall);
            serializer.SerializeValue(ref IsFallingAfterJump);
            serializer.SerializeValue(ref IsOnLadder);
            serializer.SerializeValue(ref LadderSpeed);
            serializer.SerializeValue(ref IsWallRunning);
            serializer.SerializeValue(ref WallNormal);
        }
    }
}
