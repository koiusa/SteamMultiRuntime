using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal interface IPlayerMovementDebugTarget
    {
        Object Context { get; }
        bool IsValid { get; }
        bool IsEnabled { get; }
        bool IsGrounded { get; }
        bool IsTraversalActive { get; }
        bool IsJumping { get; }
        bool IsFallingAfterJump { get; }
        bool IsFreefall { get; }
        float HorizontalVelocity { get; }
        float VerticalVelocity { get; }
        Vector3 InheritedGroundVelocity { get; }
        PlayerCompositeMotorDebugSnapshot Composite { get; }
        IPlayerMotor BaseMotor { get; }
        IPlayerTraversalCoordinator Traversal { get; }
        TraversalCoordinatorDebugSnapshot TraversalDebug { get; }
        bool HasTraversalCoordinator { get; }
        IWallTraversalFeature Wall { get; }
        WallTraversalDebugSnapshot WallDebug { get; }
        ILadderTraversalFeature Ladder { get; }
        LadderTraversalDebugSnapshot LadderDebug { get; }
        IWireConnection Wire { get; }
        WireTraversalDebugSnapshot WireDebug { get; }
        WireReelDebugSnapshot WireReelDebug { get; }
        bool WallRunInstalled { get; }
        bool WallRunEnabled { get; }
        bool WallJumpInstalled { get; }
        bool WallJumpEnabled { get; }
        bool WallSlideInstalled { get; }
        bool WallSlideEnabled { get; }
        bool LadderClimbInstalled { get; }
        bool LadderClimbEnabled { get; }
        bool LadderDetachInstalled { get; }
        bool LadderDetachEnabled { get; }
        bool WireAttachInstalled { get; }
        bool WireAttachEnabled { get; }
        bool WireSwingInstalled { get; }
        bool WireSwingEnabled { get; }
        bool WireReelInstalled { get; }
        bool WireReelEnabled { get; }
        bool WireGroundInstalled { get; }
        bool WireGroundEnabled { get; }
        void SetStateTransitionLogging(bool enabled);
    }

    internal sealed class PlayerMovementDebugTarget : IPlayerMovementDebugTarget
    {
        private readonly PlayerCompositeMotor composite;
        private readonly IPlayerMotor motor;
        private readonly PlayerTraversalCoordinator coordinator;
        private readonly WallTraversalFeature wall;
        private readonly LadderTraversalFeature ladder;
        private readonly WireTraversalFeature wire;
        private readonly IWallRunAction wallRun;
        private readonly IWallJumpAction wallJump;
        private readonly IWallSlideAction wallSlide;
        private readonly ILadderClimbAction ladderClimb;
        private readonly ILadderDetachAction ladderDetach;
        private readonly IWireAttachAction wireAttach;
        private readonly IWireSwingAction wireSwing;
        private readonly WireReelAction wireReel;
        private readonly IWireGroundAction wireGround;

        public PlayerMovementDebugTarget(PlayerCompositeMotor composite)
        {
            this.composite = composite;
            motor = composite.GetComponent<IPlayerMotor>();
            coordinator = composite.GetComponent<PlayerTraversalCoordinator>();
            wall = composite.GetComponent<WallTraversalFeature>();
            ladder = composite.GetComponent<LadderTraversalFeature>();
            wire = composite.GetComponent<WireTraversalFeature>();
            wallRun = composite.GetComponent<IWallRunAction>();
            wallJump = composite.GetComponent<IWallJumpAction>();
            wallSlide = composite.GetComponent<IWallSlideAction>();
            ladderClimb = composite.GetComponent<ILadderClimbAction>();
            ladderDetach = composite.GetComponent<ILadderDetachAction>();
            wireAttach = composite.GetComponent<IWireAttachAction>();
            wireSwing = composite.GetComponent<IWireSwingAction>();
            wireReel = composite.GetComponent<WireReelAction>();
            wireGround = composite.GetComponent<IWireGroundAction>();
        }

        public Object Context => composite;
        public bool IsValid => composite != null;
        public bool IsEnabled => composite != null && composite.isActiveAndEnabled;
        public bool IsGrounded => composite != null && composite.IsGrounded;
        public bool IsTraversalActive => composite != null && composite.IsTraversalActive;
        public bool IsJumping => composite != null && composite.IsJumping;
        public bool IsFallingAfterJump => composite != null && composite.IsFallingAfterJump;
        public bool IsFreefall => composite != null && composite.IsFreefall;
        public float HorizontalVelocity => composite != null ? composite.HorizontalVelocity : 0f;
        public float VerticalVelocity => composite != null ? composite.VerticalVelocity : 0f;
        public Vector3 InheritedGroundVelocity => composite != null ? composite.InheritedGroundVelocity : Vector3.zero;
        public PlayerCompositeMotorDebugSnapshot Composite => composite != null ? composite.GetDebugSnapshot() : default;
        public IPlayerMotor BaseMotor => IsAlive(motor) ? motor : null;
        public IPlayerTraversalCoordinator Traversal => coordinator != null ? coordinator : null;
        public TraversalCoordinatorDebugSnapshot TraversalDebug => coordinator != null ? coordinator.GetDebugSnapshot() : default;
        public bool HasTraversalCoordinator => coordinator != null;
        public IWallTraversalFeature Wall => wall != null ? wall : null;
        public WallTraversalDebugSnapshot WallDebug => wall != null ? wall.GetDebugSnapshot() : default;
        public ILadderTraversalFeature Ladder => ladder != null ? ladder : null;
        public LadderTraversalDebugSnapshot LadderDebug => ladder != null ? ladder.GetDebugSnapshot() : default;
        public IWireConnection Wire => wire != null ? wire : null;
        public WireTraversalDebugSnapshot WireDebug => wire != null ? wire.GetDebugSnapshot() : default;
        public WireReelDebugSnapshot WireReelDebug => wireReel != null ? wireReel.GetDebugSnapshot() : default;
        public bool WallRunInstalled => IsAlive(wallRun);
        public bool WallRunEnabled => IsAlive(wallRun) && wallRun.IsEnabled;
        public bool WallJumpInstalled => IsAlive(wallJump);
        public bool WallJumpEnabled => IsAlive(wallJump) && wallJump.IsEnabled;
        public bool WallSlideInstalled => IsAlive(wallSlide);
        public bool WallSlideEnabled => IsAlive(wallSlide) && wallSlide.IsEnabled;
        public bool LadderClimbInstalled => IsAlive(ladderClimb);
        public bool LadderClimbEnabled => IsAlive(ladderClimb) && ladderClimb.IsEnabled;
        public bool LadderDetachInstalled => IsAlive(ladderDetach);
        public bool LadderDetachEnabled => IsAlive(ladderDetach) && ladderDetach.IsEnabled;
        public bool WireAttachInstalled => IsAlive(wireAttach);
        public bool WireAttachEnabled => IsAlive(wireAttach) && wireAttach.IsEnabled;
        public bool WireSwingInstalled => IsAlive(wireSwing);
        public bool WireSwingEnabled => IsAlive(wireSwing) && wireSwing.IsEnabled;
        public bool WireReelInstalled => IsAlive(wireReel);
        public bool WireReelEnabled => IsAlive(wireReel) && wireReel.IsEnabled;
        public bool WireGroundInstalled => IsAlive(wireGround);
        public bool WireGroundEnabled => IsAlive(wireGround) && wireGround.IsEnabled;

        public void SetStateTransitionLogging(bool enabled)
        {
            if (coordinator != null) coordinator.LogStateTransitions = enabled;
        }

        private static bool IsAlive(object value)
        {
            return value is Object unityObject ? unityObject != null : value != null;
        }
    }
}
