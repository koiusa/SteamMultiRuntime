using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [DisallowMultipleComponent]
    public sealed class ActorTraversalCoordinator : MonoBehaviour, IActorTraversalCoordinator, ITraversalIntentContext,
        IActorFacingRequestSource,
        ITraversalCoordinatorDebugSnapshotSource
    {
        [SerializeField, Tooltip("TraversalのState遷移をUnity Consoleへ出力します。")]
        private bool logStateTransitions;

        private Rigidbody rb;
        private GroundMotionTracker groundMotionTracker;
        private SlopeContactResolver slopeContactResolver;
        private IWallRunAction wallRunAction;
        private IWallJumpAction wallJumpAction;
        private IWallSlideAction wallSlideAction;
        private ILadderTraversalFeature ladderFeature;
        private ILadderDetachAction ladderDetachAction;
        private IWireConnection wireConnection;
        private IWireAttachAction wireAttachAction;
        private IWireGrappleTargetingFeature wireTargeting;
        private IWireSwingAction wireSwingAction;
        private IWireReelAction wireReelAction;
        private IWireGroundAction wireGroundAction;
        private IScreenAimCursor screenAimCursor;
        private float wallTraversalBlockedUntilTime;
        private bool wallRunBlockedUntilWallExit;
        private bool lastIsGrounded;
        private float stateEnteredAt;
        private WireAimResult currentWireAimResult;

        public TraversalIntentFlags CurrentIntentFlags { get; private set; }
        public bool IsEnabled => isActiveAndEnabled;
        public ActorTraversalState CurrentState { get; private set; } = ActorTraversalState.Grounded;
        public float StateElapsedTime => Mathf.Max(0f, Time.time - stateEnteredAt);
        public bool IsOnLadder => ladderFeature != null && ladderFeature.IsEnabled && ladderFeature.IsOnLadder;
        public float LadderSpeed => IsOnLadder ? ladderFeature.ClimbSpeed : 0f;
        public bool IsWallRunning => wallRunAction != null && wallRunAction.IsEnabled && wallRunAction.IsWallRunning;
        public Vector3 WallNormal => IsWallRunning ? wallRunAction.WallNormal : Vector3.zero;
        public bool IsWireAttached => wireConnection != null && wireConnection.IsEnabled && wireConnection.IsAttached;
        public bool IsWireGroundActionActive => IsWireAttached && wireGroundAction != null && wireGroundAction.BlocksSwing;
        public bool UsesWireGroundStrafe => IsWireGroundActionActive && wireGroundAction.UsesStrafeMovement;
        public float WireGroundStrafeBlend => wireGroundAction != null && wireGroundAction.IsEnabled
            ? Mathf.Clamp01(wireGroundAction.StrafeBlend)
            : 0f;
        public float WireGroundFacingBlend => UsesWireGroundStrafe ? Mathf.Clamp01(wireGroundAction.FacingBlend) : 0f;
        public Vector3 WireAnchorPoint => IsWireAttached ? wireConnection.AnchorPoint : Vector3.zero;
        public Transform WireAnchorTransform => IsWireAttached ? wireConnection.AnchorTransform : null;
        public float WireRopeLength => IsWireAttached ? wireConnection.RopeLength : 0f;

        public bool TryGetFacingRequest(Vector3 origin, bool isStrafeMode, out ActorFacingRequest request)
        {
            if (!UsesWireGroundStrafe)
            {
                request = default;
                return false;
            }

            request = new ActorFacingRequest(
                WireAnchorPoint - origin,
                ActorFacingPriority.WireGround,
                wireGroundAction.FacingBlend,
                wireGroundAction.FacingRotationSpeed);
            return request.IsValid;
        }
        TraversalCoordinatorDebugSnapshot ITraversalCoordinatorDebugSnapshotSource.GetDebugSnapshot() => new TraversalCoordinatorDebugSnapshot(
            CurrentIntentFlags,
            Mathf.Max(0f, wallTraversalBlockedUntilTime - Time.time),
            wallRunBlockedUntilWallExit,
            currentWireAimResult,
            logStateTransitions);

        void ITraversalCoordinatorDebugSnapshotSource.SetStateTransitionLogging(bool enabled) =>
            logStateTransitions = enabled;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            groundMotionTracker = GetComponent<GroundMotionTracker>();
            slopeContactResolver = GetComponent<SlopeContactResolver>();
            CacheFeatures();
            screenAimCursor = GetComponent<IScreenAimCursor>();
            if (screenAimCursor == null)
            {
                screenAimCursor = gameObject.AddComponent<WireAimCursorOverlay>();
            }
            stateEnteredAt = Time.time;
        }

        private void OnDisable()
        {
            screenAimCursor?.SetVisible(false);
        }

        public bool IsTraversalActive
        {
            get
            {
                return IsEnabled && (CurrentState == ActorTraversalState.WallRun
                    || CurrentState == ActorTraversalState.WallSlide
                    || CurrentState == ActorTraversalState.Ladder
                    || IsWireAttached);
            }
        }

        public void ResetState()
        {
            CurrentIntentFlags = TraversalIntentFlags.None;
            wallTraversalBlockedUntilTime = 0f;
            wallRunBlockedUntilWallExit = false;
            lastIsGrounded = false;
            SetState(ActorTraversalState.Grounded);
            wallRunAction?.ResetState();
            wallJumpAction?.ResetState();
            wallSlideAction?.ResetState();
            ladderFeature?.ResetState();
            wireConnection?.Detach();
            screenAimCursor?.SetVisible(false);
        }

        public WireAimResult SetWireAimCursor(Vector2 screenPosition, bool hasScreenPosition, Vector3 origin = default, Vector3 targetPoint = default, bool isAiming = false)
        {
            var canTarget = IsEnabled
                && wireAttachAction != null
                && wireAttachAction.IsEnabled
                && wireConnection != null
                && wireConnection.IsEnabled
                && !wireConnection.IsAttached;
            currentWireAimResult = EvaluateWireAim(origin, targetPoint);
            if (screenAimCursor != null)
            {
                screenAimCursor.SetPosition(screenPosition);
                screenAimCursor.SetAiming(isAiming);
                screenAimCursor.SetTargetState(currentWireAimResult.State);
                screenAimCursor.SetVisible(hasScreenPosition && canTarget);
            }

            return currentWireAimResult;
        }

        public void SetWireInput(bool held, bool fireRequested, float reelInput, Vector3 origin, Vector3 targetPoint)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (wireReelAction != null && wireReelAction.IsEnabled) wireReelAction.SetInput(reelInput);
            if ((currentWireAimResult.RequestedPoint - targetPoint).sqrMagnitude > 0.0001f)
            {
                currentWireAimResult = EvaluateWireAim(origin, targetPoint);
            }

            if (wireAttachAction != null && wireAttachAction.IsEnabled) wireAttachAction.SetInput(held, fireRequested, currentWireAimResult);
        }

        private WireAimResult EvaluateWireAim(Vector3 origin, Vector3 targetPoint)
        {
            return wireTargeting != null && wireTargeting.IsEnabled
                ? wireTargeting.EvaluateTarget(origin, targetPoint)
                : WireAimResult.Invalid(targetPoint);
        }

        public void SetReplicatedWireState(bool isAttached, Vector3 anchorPoint, float ropeLength, Transform movingAnchor = null)
        {
            wireConnection?.SetReplicatedState(isAttached, anchorPoint, ropeLength, movingAnchor);
        }

        public bool ProcessMotorInput(Vector3 moveDirection, bool jumpRequested, bool isGrounded)
        {
            if (!IsEnabled || wireConnection == null || !wireConnection.IsEnabled)
            {
                return false;
            }

            if (wireSwingAction != null && wireSwingAction.IsEnabled) wireSwingAction.SetMoveDirection(moveDirection);
            if (wireGroundAction != null && wireGroundAction.IsEnabled) wireGroundAction.SetMoveDirection(moveDirection);
            if (!wireConnection.IsAttached || !jumpRequested)
            {
                return false;
            }

            if (isGrounded)
            {
                // Ground traversal intentionally keeps spare wire available. Freeze the
                // current distance at takeoff so airborne reeling starts immediately and
                // does not have to consume that invisible ground slack first.
                wireConnection.CaptureCurrentLength();
                return false;
            }

            if (wireReelAction == null || !wireReelAction.IsEnabled) return false;
            wireReelAction.ReelStep();
            return true;
        }

        public bool HasIntent(TraversalIntentFlags flag)
        {
            return (CurrentIntentFlags & flag) == flag;
        }

        public void ApplyTraversal(Vector3 moveDirection, Vector2 moveInput, Quaternion moveReferenceRotation, bool jumpRequested, bool isGrounded)
        {
            if (!IsEnabled)
            {
                return;
            }

            var activeWallRunFeature = wallRunAction != null && wallRunAction.IsEnabled ? wallRunAction : null;
            var activeWallJumpFeature = wallJumpAction != null && wallJumpAction.IsEnabled ? wallJumpAction : null;
            var activeWallSlideFeature = wallSlideAction != null && wallSlideAction.IsEnabled ? wallSlideAction : null;
            var activeLadderFeature = ladderFeature != null && ladderFeature.IsEnabled ? ladderFeature : null;
            var activeLadderDetachAction = ladderDetachAction != null && ladderDetachAction.IsEnabled ? ladderDetachAction : null;
            var hasFeatureTraversal = activeWallRunFeature != null
                || activeWallJumpFeature != null
                || activeWallSlideFeature != null
                || activeLadderFeature != null;
            if (rb == null)
            {
                return;
            }

            lastIsGrounded = isGrounded;
            // Reel input is applied by the authoritative traversal tick. Keeping this
            // beside state evaluation prevents MonoBehaviour FixedUpdate ordering from
            // delaying or skipping the operation when entering WireSwing.
            if (wireReelAction != null && wireReelAction.IsEnabled)
            {
                wireReelAction.ApplyReel(Time.fixedDeltaTime);
            }
            var allowLadderIntents = !IsWireAttached
                && activeLadderFeature != null
                && activeLadderFeature.IsOnLadder;
            CurrentIntentFlags = BuildIntentFlags(moveInput, jumpRequested, isGrounded, allowLadderIntents);
            if (IsWireAttached)
            {
                activeLadderFeature?.ResetState();
                activeWallRunFeature?.ResetState();
                activeWallJumpFeature?.ResetState();
                activeWallSlideFeature?.ResetState();
                SetState(ActorTraversalState.WireSwing);
                return;
            }

            if (!hasFeatureTraversal)
            {
                SetState(isGrounded ? ActorTraversalState.Grounded : ActorTraversalState.Airborne);
                return;
            }

            // 梯子処理は feature 側に委譲する
            if (activeLadderFeature != null)
            {
                var upAxisForLadder = GetUpAxis();
                if (activeLadderDetachAction != null && activeLadderDetachAction.TryHandleTraversal(rb.linearVelocity, moveInput, moveReferenceRotation, jumpRequested, isGrounded, upAxisForLadder, out var ladderVelocity, out var detachedByJump))
                {
                    if (detachedByJump)
                    {
                        // 梯子離脱直後の壁接触残りをクリアして、壁ズリ誤判定を抑える
                        wallTraversalBlockedUntilTime = Time.time + activeLadderFeature.WallTraversalBlockDuration;
                        slopeContactResolver?.Clear();
                        activeWallRunFeature?.ResetState();
                        activeWallJumpFeature?.ResetState();
                        activeWallSlideFeature?.ResetState();
                        SetState(ActorTraversalState.Cooldown);
                    }
                    else if (activeLadderFeature.IsOnLadder)
                    {
                        wallTraversalBlockedUntilTime = 0f;
                        rb.linearVelocity = ladderVelocity;
                        activeWallRunFeature?.ResetState();
                        activeWallJumpFeature?.ResetState();
                        activeWallSlideFeature?.ResetState();
                        SetState(ActorTraversalState.Ladder);
                    }
                    else
                    {
                        // Directional/ground detach must not reinterpret the ladder surface
                        // as a runnable wall on the following physics frame.
                        wallTraversalBlockedUntilTime = Time.time + activeLadderFeature.WallTraversalBlockDuration;
                        slopeContactResolver?.Clear();
                        activeWallRunFeature?.ResetState();
                        activeWallJumpFeature?.ResetState();
                        activeWallSlideFeature?.ResetState();
                        SetState(ActorTraversalState.Cooldown);
                    }

                    return;
                }
            }

            if (isGrounded)
            {
                wallRunBlockedUntilWallExit = false;
                activeWallRunFeature?.ResetState();
                activeWallJumpFeature?.ResetState();
                activeWallSlideFeature?.ResetState();
                SetState(ActorTraversalState.Grounded);
                return;
            }

            var upAxis = GetUpAxis();
            var velocity = rb.linearVelocity;
            if (wallRunBlockedUntilWallExit && !slopeContactResolver.HasObstacleContact)
            {
                wallRunBlockedUntilWallExit = false;
            }

            if (Time.time < wallTraversalBlockedUntilTime)
            {
                activeWallRunFeature?.ResetState();
                activeWallSlideFeature?.ResetState();
                rb.linearVelocity = velocity;
                SetState(ActorTraversalState.Cooldown);
                return;
            }

            var wallJumped = false;
            var wallRunApplied = false;
            if (jumpRequested && activeWallJumpFeature != null && activeWallJumpFeature.TryWallJump(velocity, moveDirection, upAxis, out var wallJumpVelocity))
            {
                velocity = wallJumpVelocity;
                wallJumped = true;
                activeWallRunFeature?.NotifyWallJump();
                activeWallSlideFeature?.ResetState();
                slopeContactResolver?.Clear();
                groundMotionTracker?.ClearGroundContacts();
                SetState(ActorTraversalState.WallJump);
            }
            else if (!wallRunBlockedUntilWallExit
                && CanProcessWallRun(CurrentState)
                && activeWallRunFeature != null
                && activeWallRunFeature.TryAccelerateOnWall(velocity, moveDirection, upAxis, out var wallVelocity))
            {
                velocity = wallVelocity;
                wallRunApplied = true;
                activeWallSlideFeature?.ResetState();
                SetState(ActorTraversalState.WallRun);
            }

            if (wallRunApplied && activeWallRunFeature != null)
            {
                velocity = activeWallRunFeature.ApplyVerticalMotion(velocity, upAxis);
            }
            else if (!wallJumped && activeWallSlideFeature != null && activeWallSlideFeature.TryApplyWallSlide(velocity, moveDirection, upAxis, false, out var wallSlideVelocity))
            {
                velocity = wallSlideVelocity;
                wallRunBlockedUntilWallExit = true;
                SetState(ActorTraversalState.WallSlide);
            }
            else if (!wallJumped)
            {
                SetState(ActorTraversalState.Airborne);
            }

            rb.linearVelocity = velocity;
        }

        private void CacheFeatures()
        {
            wallRunAction = GetComponent<IWallRunAction>();
            wallJumpAction = GetComponent<IWallJumpAction>();
            wallSlideAction = GetComponent<IWallSlideAction>();
            ladderFeature = GetComponent<ILadderTraversalFeature>();
            ladderDetachAction = GetComponent<ILadderDetachAction>();
            wireConnection = GetComponent<IWireConnection>();
            wireAttachAction = GetComponent<IWireAttachAction>();
            wireTargeting = GetComponent<IWireGrappleTargetingFeature>();
            wireSwingAction = GetComponent<IWireSwingAction>();
            wireReelAction = GetComponent<IWireReelAction>();
            wireGroundAction = GetComponent<IWireGroundAction>();
        }

        private void SetState(ActorTraversalState nextState)
        {
            if (CurrentState == nextState)
            {
                return;
            }

            var previousState = CurrentState;
            var previousStateElapsed = StateElapsedTime;
            CurrentState = nextState;
            stateEnteredAt = Time.time;
            if (logStateTransitions)
            {
                Debug.Log(
                    $"[Traversal] {name}: {previousState} -> {nextState} " +
                    $"(elapsed: {previousStateElapsed:F3}s, intent: {CurrentIntentFlags}, " +
                    $"grounded: {lastIsGrounded}, wire: {IsWireAttached}, ladder: {IsOnLadder})",
                    this);
            }
        }

        private static bool CanProcessWallRun(ActorTraversalState state)
        {
            // WallSlideは壁との接触中にラッチする。カメラ相対入力の向きが変化しても
            // WallRunへ自動昇格させず、壁を離れてAirborneへ戻ってから再判定する。
            // Ladder/Cooldownからの直接遷移も禁止する。
            return state == ActorTraversalState.Airborne
                || state == ActorTraversalState.WallRun;
        }

        private static TraversalIntentFlags BuildIntentFlags(
            Vector2 moveInput,
            bool jumpRequested,
            bool isGrounded,
            bool allowLadderIntents)
        {
            var flags = TraversalIntentFlags.None;

            if (jumpRequested)
            {
                flags |= TraversalIntentFlags.JumpRequested;
            }

            if (!allowLadderIntents)
            {
                return flags;
            }

            if (Mathf.Abs(moveInput.x) > 0.2f)
            {
                flags |= TraversalIntentFlags.WantsLadderDetachByLateral;
            }

            if (isGrounded && moveInput.y < -0.01f)
            {
                flags |= TraversalIntentFlags.WantsLadderDetachByDescendOnGround;
            }

            if (isGrounded && Mathf.Abs(moveInput.y) <= 0.01f)
            {
                flags |= TraversalIntentFlags.WantsLadderIdleOnGround;
            }

            return flags;
        }

        private static Vector3 GetUpAxis()
        {
            return Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
        }
    }
}
