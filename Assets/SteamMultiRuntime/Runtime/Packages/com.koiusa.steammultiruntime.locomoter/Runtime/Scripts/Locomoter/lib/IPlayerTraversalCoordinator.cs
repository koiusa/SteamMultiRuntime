using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    public enum PlayerTraversalState
    {
        Grounded = 0,
        Airborne = 1,
        WallRun = 2,
        WallSlide = 3,
        Ladder = 4,
        WallJump = 5,
        Cooldown = 6,
    }

    public interface IPlayerTraversalCoordinator
    {
        bool IsEnabled { get; }
        PlayerTraversalState CurrentState { get; }
        float StateElapsedTime { get; }
        bool IsTraversalActive { get; }
        void ResetState();
        void ApplyTraversal(Vector3 moveDirection, Vector2 moveInput, bool jumpRequested, bool isGrounded);
    }
}
