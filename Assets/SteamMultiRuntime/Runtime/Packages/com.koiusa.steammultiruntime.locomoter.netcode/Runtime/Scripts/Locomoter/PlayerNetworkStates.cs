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
        public bool GrappleHeld;
        public int GrappleFireToken;
        public float ReelInput;
        public Vector3 GrappleTargetPoint;

        public PlayerInputSyncState(Vector3 moveDirection, Vector2 moveInput, Quaternion moveReferenceRotation, int jumpToken, bool isStrafeMode, bool grappleHeld = false, float reelInput = 0f, Vector3 grappleTargetPoint = default, int grappleFireToken = 0)
        {
            MoveDirection = moveDirection;
            MoveInput = moveInput;
            MoveReferenceRotation = moveReferenceRotation;
            JumpToken = jumpToken;
            IsStrafeMode = isStrafeMode;
            GrappleHeld = grappleHeld;
            ReelInput = reelInput;
            GrappleTargetPoint = grappleTargetPoint;
            GrappleFireToken = grappleFireToken;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MoveDirection);
            serializer.SerializeValue(ref MoveInput);
            serializer.SerializeValue(ref MoveReferenceRotation);
            serializer.SerializeValue(ref JumpToken);
            serializer.SerializeValue(ref IsStrafeMode);
            serializer.SerializeValue(ref GrappleHeld);
            serializer.SerializeValue(ref ReelInput);
            serializer.SerializeValue(ref GrappleTargetPoint);
            serializer.SerializeValue(ref GrappleFireToken);
        }
    }

    internal struct WireSwingNetworkState : INetworkSerializable, System.IEquatable<WireSwingNetworkState>
    {
        public bool IsAttached;
        public Vector3 AnchorPoint;
        public bool HasAnchorObject;
        public NetworkObjectReference AnchorObject;
        public Vector3 AnchorLocalPoint;
        public float RopeLength;

        public WireSwingNetworkState(bool isAttached, Vector3 anchorPoint, float ropeLength,
            NetworkObjectReference anchorObject = default, Vector3 anchorLocalPoint = default,
            bool hasAnchorObject = false)
        {
            IsAttached = isAttached;
            AnchorPoint = anchorPoint;
            HasAnchorObject = hasAnchorObject;
            AnchorObject = anchorObject;
            AnchorLocalPoint = anchorLocalPoint;
            RopeLength = ropeLength;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref IsAttached);
            serializer.SerializeValue(ref AnchorPoint);
            serializer.SerializeValue(ref HasAnchorObject);
            serializer.SerializeValue(ref AnchorObject);
            serializer.SerializeValue(ref AnchorLocalPoint);
            serializer.SerializeValue(ref RopeLength);
        }

        public bool Equals(WireSwingNetworkState other)
        {
            return IsAttached == other.IsAttached
                && AnchorPoint == other.AnchorPoint
                && HasAnchorObject == other.HasAnchorObject
                && AnchorObject.Equals(other.AnchorObject)
                && AnchorLocalPoint == other.AnchorLocalPoint
                && RopeLength.Equals(other.RopeLength);
        }
    }

    internal struct PlayerKinematicState : INetworkSerializable
    {
        public float HorizontalVelocity;
        public float VerticalVelocity;

        public PlayerKinematicState(float horizontalVelocity, float verticalVelocity)
        {
            HorizontalVelocity = horizontalVelocity;
            VerticalVelocity = verticalVelocity;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
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
