using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerController
    {
        bool IsGrounded { get; }
        bool IsJumping { get; }
        bool IsFreefall { get; }
        bool IsFallingAfterJump { get; }
        bool IsStrafeMode { get; }
        Vector3 InheritedGroundVelocity { get; }
        Vector2 MoveInput { get; }
        Vector3 MoveDirection { get; }
        float HorizontalVelocity { get; }
        float VerticalVelocity { get; }
        float MaxMoveSpeed { get; }
    }
}
