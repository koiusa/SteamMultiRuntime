using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [DisallowMultipleComponent]
    public sealed class PlayerTraversalCoordinator : MonoBehaviour, IPlayerTraversalCoordinator, ITraversalIntentContext
    {
        private Rigidbody rb;
        private GroundMotionTracker groundMotionTracker;
        private SlopeContactResolver slopeContactResolver;
        private IWallRunTraversalFeature wallRunFeature;
        private IWallJumpTraversalFeature wallJumpFeature;
        private IWallSlideTraversalFeature wallSlideFeature;
        private ILadderTraversalFeature ladderFeature;
        private IWireConnection wireConnection;
        private IWireAttachAction wireAttachAction;
        private IWireSwingAction wireSwingAction;
        private IWireReelAction wireReelAction;
        private IWireGroundAction wireGroundAction;
        private float wallTraversalBlockedUntilTime;
        private bool wallRunBlockedUntilWallExit;
        private float stateEnteredAt;

        public TraversalIntentFlags CurrentIntentFlags { get; private set; }
        public bool IsEnabled => isActiveAndEnabled;
        public PlayerTraversalState CurrentState { get; private set; } = PlayerTraversalState.Grounded;
        public float StateElapsedTime => Mathf.Max(0f, Time.time - stateEnteredAt);
        public bool IsOnLadder => ladderFeature != null && ladderFeature.IsEnabled && ladderFeature.IsOnLadder;
        public float LadderSpeed => IsOnLadder ? ladderFeature.ClimbSpeed : 0f;
        public bool IsWallRunning => wallRunFeature != null && wallRunFeature.IsEnabled && wallRunFeature.IsWallRunning;
        public Vector3 WallNormal => IsWallRunning ? wallRunFeature.WallNormal : Vector3.zero;
        public bool IsWireAttached => wireConnection != null && wireConnection.IsEnabled && wireConnection.IsAttached;
        public bool IsWireGroundActionActive => IsWireAttached && wireGroundAction != null && wireGroundAction.BlocksSwing;
        public bool UsesWireGroundStrafe => IsWireGroundActionActive && wireGroundAction.UsesStrafeMovement;
        public Vector3 WireAnchorPoint => IsWireAttached ? wireConnection.AnchorPoint : Vector3.zero;
        public float WireRopeLength => IsWireAttached ? wireConnection.RopeLength : 0f;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            groundMotionTracker = GetComponent<GroundMotionTracker>();
            slopeContactResolver = GetComponent<SlopeContactResolver>();
            CacheFeatures();
            stateEnteredAt = Time.time;
        }

        public bool IsTraversalActive
        {
            get
            {
                return IsEnabled && (CurrentState == PlayerTraversalState.WallRun
                    || CurrentState == PlayerTraversalState.WallSlide
                    || CurrentState == PlayerTraversalState.Ladder
                    || IsWireAttached);
            }
        }

        public void ResetState()
        {
            CurrentIntentFlags = TraversalIntentFlags.None;
            wallTraversalBlockedUntilTime = 0f;
            wallRunBlockedUntilWallExit = false;
            SetState(PlayerTraversalState.Grounded);
            wallRunFeature?.ResetState();
            wallJumpFeature?.ResetState();
            wallSlideFeature?.ResetState();
            ladderFeature?.ResetState();
            wireConnection?.Detach();
        }

        public void SetWireInput(bool held, float reelInput, Vector3 origin, Vector3 aimDirection)
        {
            if (!IsEnabled)
            {
                return;
            }

            if (wireReelAction != null && wireReelAction.IsEnabled) wireReelAction.SetInput(reelInput);
            if (wireAttachAction != null && wireAttachAction.IsEnabled) wireAttachAction.SetInput(held, origin, aimDirection);
        }

        public void SetReplicatedWireState(bool isAttached, Vector3 anchorPoint, float ropeLength)
        {
            wireConnection?.SetReplicatedState(isAttached, anchorPoint, ropeLength);
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

            var activeWallRunFeature = wallRunFeature != null && wallRunFeature.IsEnabled ? wallRunFeature : null;
            var activeWallJumpFeature = wallJumpFeature != null && wallJumpFeature.IsEnabled ? wallJumpFeature : null;
            var activeWallSlideFeature = wallSlideFeature != null && wallSlideFeature.IsEnabled ? wallSlideFeature : null;
            var activeLadderFeature = ladderFeature != null && ladderFeature.IsEnabled ? ladderFeature : null;
            var hasFeatureTraversal = activeWallRunFeature != null
                || activeWallJumpFeature != null
                || activeWallSlideFeature != null
                || activeLadderFeature != null;
            if (rb == null)
            {
                return;
            }

            CurrentIntentFlags = BuildIntentFlags(moveInput, jumpRequested, isGrounded);
            if (IsWireAttached)
            {
                activeLadderFeature?.ResetState();
                activeWallRunFeature?.ResetState();
                activeWallJumpFeature?.ResetState();
                activeWallSlideFeature?.ResetState();
                SetState(PlayerTraversalState.WireSwing);
                return;
            }

            if (!hasFeatureTraversal)
            {
                SetState(isGrounded ? PlayerTraversalState.Grounded : PlayerTraversalState.Airborne);
                return;
            }

            // 梯子処理は feature 側に委譲する
            if (activeLadderFeature != null)
            {
                var upAxisForLadder = GetUpAxis();
                if (activeLadderFeature.TryHandleTraversal(rb.linearVelocity, moveInput, moveReferenceRotation, jumpRequested, isGrounded, upAxisForLadder, out var ladderVelocity, out var detachedByJump))
                {
                    if (detachedByJump)
                    {
                        // 梯子離脱直後の壁接触残りをクリアして、壁ズリ誤判定を抑える
                        wallTraversalBlockedUntilTime = Time.time + activeLadderFeature.WallTraversalBlockDuration;
                        slopeContactResolver?.Clear();
                        activeWallRunFeature?.ResetState();
                        activeWallJumpFeature?.ResetState();
                        activeWallSlideFeature?.ResetState();
                        SetState(PlayerTraversalState.Cooldown);
                    }
                    else if (activeLadderFeature.IsOnLadder)
                    {
                        wallTraversalBlockedUntilTime = 0f;
                        rb.linearVelocity = ladderVelocity;
                        activeWallRunFeature?.ResetState();
                        activeWallJumpFeature?.ResetState();
                        activeWallSlideFeature?.ResetState();
                        SetState(PlayerTraversalState.Ladder);
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
                        SetState(PlayerTraversalState.Cooldown);
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
                SetState(PlayerTraversalState.Grounded);
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
                SetState(PlayerTraversalState.Cooldown);
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
                SetState(PlayerTraversalState.WallJump);
            }
            else if (!wallRunBlockedUntilWallExit
                && CanProcessWallRun(CurrentState)
                && activeWallRunFeature != null
                && activeWallRunFeature.TryAccelerateOnWall(velocity, moveDirection, upAxis, out var wallVelocity))
            {
                velocity = wallVelocity;
                wallRunApplied = true;
                activeWallSlideFeature?.ResetState();
                SetState(PlayerTraversalState.WallRun);
            }

            if (wallRunApplied && activeWallRunFeature != null)
            {
                velocity = activeWallRunFeature.ApplyVerticalMotion(velocity, upAxis);
            }
            else if (!wallJumped && activeWallSlideFeature != null && activeWallSlideFeature.TryApplyWallSlide(velocity, moveDirection, upAxis, false, out var wallSlideVelocity))
            {
                velocity = wallSlideVelocity;
                wallRunBlockedUntilWallExit = true;
                SetState(PlayerTraversalState.WallSlide);
            }
            else if (!wallJumped)
            {
                SetState(PlayerTraversalState.Airborne);
            }

            rb.linearVelocity = velocity;
        }

        private void CacheFeatures()
        {
            wallRunFeature = GetComponent<IWallRunTraversalFeature>();
            wallJumpFeature = GetComponent<IWallJumpTraversalFeature>();
            wallSlideFeature = GetComponent<IWallSlideTraversalFeature>();
            ladderFeature = GetComponent<ILadderTraversalFeature>();
            wireConnection = GetComponent<IWireConnection>();
            wireAttachAction = GetComponent<IWireAttachAction>();
            wireSwingAction = GetComponent<IWireSwingAction>();
            wireReelAction = GetComponent<IWireReelAction>();
            wireGroundAction = GetComponent<IWireGroundAction>();
        }

        private void SetState(PlayerTraversalState nextState)
        {
            if (CurrentState == nextState)
            {
                return;
            }

            CurrentState = nextState;
            stateEnteredAt = Time.time;
        }

        private static bool CanProcessWallRun(PlayerTraversalState state)
        {
            // WallSlideは壁との接触中にラッチする。カメラ相対入力の向きが変化しても
            // WallRunへ自動昇格させず、壁を離れてAirborneへ戻ってから再判定する。
            // Ladder/Cooldownからの直接遷移も禁止する。
            return state == PlayerTraversalState.Airborne
                || state == PlayerTraversalState.WallRun;
        }

        private static TraversalIntentFlags BuildIntentFlags(Vector2 moveInput, bool jumpRequested, bool isGrounded)
        {
            var flags = TraversalIntentFlags.None;

            if (jumpRequested)
            {
                flags |= TraversalIntentFlags.JumpRequested;
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
