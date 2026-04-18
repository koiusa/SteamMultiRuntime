using UnityEngine;
using Unity.Netcode;

namespace Koiusa.SteamMultiRuntime
{
    internal struct PlayerInputSyncState : INetworkSerializable
    {
        public Vector3 MoveDirection;
        public int JumpToken;
        public bool IsStrafeMode;

        public PlayerInputSyncState(Vector3 moveDirection, int jumpToken, bool isStrafeMode)
        {
            MoveDirection = moveDirection;
            JumpToken = jumpToken;
            IsStrafeMode = isStrafeMode;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MoveDirection);
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

        public PlayerMovementFlagsState(bool isGrounded, bool isJumping, bool isFreefall, bool isFallingAfterJump)
        {
            IsGrounded = isGrounded;
            IsJumping = isJumping;
            IsFreefall = isFreefall;
            IsFallingAfterJump = isFallingAfterJump;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsGrounded);
            serializer.SerializeValue(ref IsJumping);
            serializer.SerializeValue(ref IsFreefall);
            serializer.SerializeValue(ref IsFallingAfterJump);
        }
    }
}
