using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>
    /// Read-only state produced by NPC navigation and its active motor.
    /// This contract deliberately does not identify the component as the
    /// authoritative player controller on networked NPCs.
    /// </summary>
    public interface INpcLocomotionState
    {
        bool HasPath { get; }
        bool IsMoving { get; }
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
