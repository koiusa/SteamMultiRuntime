using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    internal interface IPlayerCompositeMotorDebugSnapshotSource { PlayerCompositeMotorDebugSnapshot GetDebugSnapshot(); }
    internal interface ITraversalCoordinatorDebugSnapshotSource
    {
        TraversalCoordinatorDebugSnapshot GetDebugSnapshot();
        void SetStateTransitionLogging(bool enabled);
    }
    internal interface IWallTraversalDebugSnapshotSource { WallTraversalDebugSnapshot GetDebugSnapshot(); }
    internal interface ILadderTraversalDebugSnapshotSource { LadderTraversalDebugSnapshot GetDebugSnapshot(); }
    internal interface IWireTraversalDebugSnapshotSource { WireTraversalDebugSnapshot GetDebugSnapshot(); }

    internal interface IWireReelDebugSnapshotSource
    {
        WireReelDebugSnapshot GetDebugSnapshot();
    }

    internal readonly struct PlayerCompositeMotorDebugSnapshot
    {
        public readonly Vector2 RawMoveInput;
        public readonly Quaternion MoveReferenceRotation;
        public readonly bool HasActiveExternalMotion;
        public readonly float ActiveExternalMotionRemaining;

        public PlayerCompositeMotorDebugSnapshot(Vector2 input, Quaternion rotation, bool active, float remaining)
        {
            RawMoveInput = input;
            MoveReferenceRotation = rotation;
            HasActiveExternalMotion = active;
            ActiveExternalMotionRemaining = remaining;
        }
    }

    internal readonly struct TraversalCoordinatorDebugSnapshot
    {
        public readonly TraversalIntentFlags IntentFlags;
        public readonly float WallTraversalBlockRemaining;
        public readonly bool WallRunBlockedUntilWallExit;
        public readonly WireAimResult WireAimResult;
        public readonly bool LogStateTransitions;

        public TraversalCoordinatorDebugSnapshot(TraversalIntentFlags intentFlags, float blockRemaining,
            bool wallRunBlocked, WireAimResult wireAimResult, bool logStateTransitions)
        {
            IntentFlags = intentFlags;
            WallTraversalBlockRemaining = blockRemaining;
            WallRunBlockedUntilWallExit = wallRunBlocked;
            WireAimResult = wireAimResult;
            LogStateTransitions = logStateTransitions;
        }
    }

    internal readonly struct WallTraversalDebugSnapshot
    {
        public readonly bool HasObstacleContact;
        public readonly bool HasWallNormal;
        public readonly Vector3 WallNormal;

        public WallTraversalDebugSnapshot(bool contact, bool hasNormal, Vector3 normal)
        {
            HasObstacleContact = contact;
            HasWallNormal = hasNormal;
            WallNormal = normal;
        }
    }

    internal readonly struct LadderTraversalDebugSnapshot
    {
        public readonly LadderVolume CurrentLadder;
        public readonly int OverlappingLadderCount;
        public readonly float ReattachBlockRemaining;
        public readonly bool UsesGravity;
        public readonly Vector3 FacingDirection;

        public LadderTraversalDebugSnapshot(LadderVolume ladder, int count, float blockRemaining,
            bool usesGravity, Vector3 facingDirection)
        {
            CurrentLadder = ladder;
            OverlappingLadderCount = count;
            ReattachBlockRemaining = blockRemaining;
            UsesGravity = usesGravity;
            FacingDirection = facingDirection;
        }
    }

    internal readonly struct WireTraversalDebugSnapshot
    {
        public readonly float ActualLength;
        public readonly float RopeStretch;
        public readonly bool HasDynamicAnchor;

        public WireTraversalDebugSnapshot(float actualLength, float ropeStretch, bool hasDynamicAnchor)
        {
            ActualLength = actualLength;
            RopeStretch = ropeStretch;
            HasDynamicAnchor = hasDynamicAnchor;
        }
    }

    internal readonly struct WireReelDebugSnapshot
    {
        public readonly float Input;
        public readonly bool IsReelingIn;
        public readonly float ReelSpeed;
        public readonly float LastLengthBeforeApply;
        public readonly float LastLengthAfterApply;

        public WireReelDebugSnapshot(float input, bool isReelingIn, float reelSpeed,
            float lastLengthBeforeApply, float lastLengthAfterApply)
        {
            Input = input;
            IsReelingIn = isReelingIn;
            ReelSpeed = reelSpeed;
            LastLengthBeforeApply = lastLengthBeforeApply;
            LastLengthAfterApply = lastLengthAfterApply;
        }
    }
}
