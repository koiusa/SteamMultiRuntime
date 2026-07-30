using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    /// <summary>Read-only locomotion state supplied by a controller implementation.</summary>
    public interface IActorLocomotionState
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

    /// <summary>
    /// Stable public contract consumed by presentation and gameplay systems.
    /// Use ActorControllerAdapter to expose an IActorLocomotionState source.
    /// </summary>
    public interface IActorController : IActorLocomotionState
    {
    }
}
