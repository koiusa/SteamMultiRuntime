using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal interface IActorMovementDebugTarget
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
        ActorCompositeMotorDebugSnapshot Composite { get; }
        IActorMotor BaseMotor { get; }
        IActorTraversalCoordinator Traversal { get; }
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

    internal sealed class ActorMovementDebugTarget : IActorMovementDebugTarget
    {
        private readonly ActorCompositeMotor composite;
        private readonly IActorCompositeMotorDebugSnapshotSource compositeDebugSource;
        private readonly IActorMotor motor;
        private readonly IActorTraversalCoordinator coordinator;
        private readonly ITraversalCoordinatorDebugSnapshotSource coordinatorDebugSource;
        private readonly IWallTraversalFeature wall;
        private readonly IWallTraversalDebugSnapshotSource wallDebugSource;
        private readonly ILadderTraversalFeature ladder;
        private readonly ILadderTraversalDebugSnapshotSource ladderDebugSource;
        private readonly IWireConnection wire;
        private readonly IWireTraversalDebugSnapshotSource wireDebugSource;
        private readonly IWallRunAction wallRun;
        private readonly IWallJumpAction wallJump;
        private readonly IWallSlideAction wallSlide;
        private readonly ILadderClimbAction ladderClimb;
        private readonly ILadderDetachAction ladderDetach;
        private readonly IWireAttachAction wireAttach;
        private readonly IWireSwingAction wireSwing;
        private readonly IWireReelAction wireReel;
        private readonly IWireReelDebugSnapshotSource wireReelDebugSource;
        private readonly IWireGroundAction wireGround;

        public ActorMovementDebugTarget(ActorCompositeMotor composite)
        {
            this.composite = composite;
            compositeDebugSource = composite.GetComponent<IActorCompositeMotorDebugSnapshotSource>();
            motor = composite.GetComponent<IActorMotor>();
            coordinator = composite.GetComponent<IActorTraversalCoordinator>();
            coordinatorDebugSource = composite.GetComponent<ITraversalCoordinatorDebugSnapshotSource>();
            wall = composite.GetComponent<IWallTraversalFeature>();
            wallDebugSource = composite.GetComponent<IWallTraversalDebugSnapshotSource>();
            ladder = composite.GetComponent<ILadderTraversalFeature>();
            ladderDebugSource = composite.GetComponent<ILadderTraversalDebugSnapshotSource>();
            wire = composite.GetComponent<IWireConnection>();
            wireDebugSource = composite.GetComponent<IWireTraversalDebugSnapshotSource>();
            wallRun = composite.GetComponent<IWallRunAction>();
            wallJump = composite.GetComponent<IWallJumpAction>();
            wallSlide = composite.GetComponent<IWallSlideAction>();
            ladderClimb = composite.GetComponent<ILadderClimbAction>();
            ladderDetach = composite.GetComponent<ILadderDetachAction>();
            wireAttach = composite.GetComponent<IWireAttachAction>();
            wireSwing = composite.GetComponent<IWireSwingAction>();
            wireReel = composite.GetComponent<IWireReelAction>();
            wireReelDebugSource = composite.GetComponent<IWireReelDebugSnapshotSource>();
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
        public ActorCompositeMotorDebugSnapshot Composite => IsAlive(compositeDebugSource)
            ? compositeDebugSource.GetDebugSnapshot()
            : default;
        public IActorMotor BaseMotor => IsAlive(motor) ? motor : null;
        public IActorTraversalCoordinator Traversal => IsAlive(coordinator) ? coordinator : null;
        public TraversalCoordinatorDebugSnapshot TraversalDebug => IsAlive(coordinatorDebugSource)
            ? coordinatorDebugSource.GetDebugSnapshot()
            : default;
        public bool HasTraversalCoordinator => IsAlive(coordinator);
        public IWallTraversalFeature Wall => IsAlive(wall) ? wall : null;
        public WallTraversalDebugSnapshot WallDebug => IsAlive(wallDebugSource) ? wallDebugSource.GetDebugSnapshot() : default;
        public ILadderTraversalFeature Ladder => IsAlive(ladder) ? ladder : null;
        public LadderTraversalDebugSnapshot LadderDebug => IsAlive(ladderDebugSource) ? ladderDebugSource.GetDebugSnapshot() : default;
        public IWireConnection Wire => IsAlive(wire) ? wire : null;
        public WireTraversalDebugSnapshot WireDebug => IsAlive(wireDebugSource) ? wireDebugSource.GetDebugSnapshot() : default;
        public WireReelDebugSnapshot WireReelDebug => IsAlive(wireReelDebugSource)
            ? wireReelDebugSource.GetDebugSnapshot()
            : default;
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
            if (IsAlive(coordinatorDebugSource)) coordinatorDebugSource.SetStateTransitionLogging(enabled);
        }

        private static bool IsAlive(object value)
        {
            return value is Object unityObject ? unityObject != null : value != null;
        }
    }
}
