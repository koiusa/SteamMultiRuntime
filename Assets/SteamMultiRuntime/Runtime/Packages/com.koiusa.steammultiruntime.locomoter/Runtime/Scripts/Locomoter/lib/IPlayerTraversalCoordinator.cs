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
        WireSwing = 6,
        Cooldown = 7,
    }

    public interface IPlayerTraversalCoordinator
    {
        bool IsEnabled { get; }
        PlayerTraversalState CurrentState { get; }
        float StateElapsedTime { get; }
        bool IsTraversalActive { get; }
        bool IsOnLadder { get; }
        float LadderSpeed { get; }
        bool IsWallRunning { get; }
        Vector3 WallNormal { get; }
        bool IsWireAttached { get; }
        bool IsWireGroundActionActive { get; }
        bool UsesWireGroundStrafe { get; }
        float WireGroundStrafeBlend { get; }
        float WireGroundFacingBlend { get; }
        Vector3 WireAnchorPoint { get; }
        Transform WireAnchorTransform { get; }
        float WireRopeLength { get; }
        void ResetState();
        WireAimResult SetWireAimCursor(Vector2 screenPosition, bool hasScreenPosition, Vector3 origin = default, Vector3 targetPoint = default, bool isAiming = false);
        void SetWireInput(bool held, bool fireRequested, float reelInput, Vector3 origin, Vector3 targetPoint);
        void SetReplicatedWireState(bool isAttached, Vector3 anchorPoint, float ropeLength, Transform movingAnchor = null);
        bool ProcessMotorInput(Vector3 moveDirection, bool jumpRequested, bool isGrounded);
        void ApplyTraversal(Vector3 moveDirection, Vector2 moveInput, Quaternion moveReferenceRotation, bool jumpRequested, bool isGrounded);
    }
}
