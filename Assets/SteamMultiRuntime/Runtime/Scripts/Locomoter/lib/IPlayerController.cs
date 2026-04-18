using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public interface IPlayerController
    {
        bool IsGrounded { get; }
        bool IsJumping { get; }
        bool IsFreefall { get; }
        bool IsFallingAfterJump { get; }
        Vector3 InheritedGroundVelocity { get; }
        float HorizontalVelocity { get; }
        float VerticalVelocity { get; }
        float MaxMoveSpeed { get; }
    }
}
