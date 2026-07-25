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
        private float wallTraversalBlockedUntilTime;
        private float stateEnteredAt;

        public TraversalIntentFlags CurrentIntentFlags { get; private set; }
        public bool IsEnabled => isActiveAndEnabled;
        public PlayerTraversalState CurrentState { get; private set; } = PlayerTraversalState.Grounded;
        public float StateElapsedTime => Mathf.Max(0f, Time.time - stateEnteredAt);

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
                    || CurrentState == PlayerTraversalState.Ladder);
            }
        }

        public void ResetState()
        {
            CurrentIntentFlags = TraversalIntentFlags.None;
            wallTraversalBlockedUntilTime = 0f;
            SetState(PlayerTraversalState.Grounded);
            wallRunFeature?.ResetState();
            wallJumpFeature?.ResetState();
            wallSlideFeature?.ResetState();
            ladderFeature?.ResetState();
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
                activeWallRunFeature?.ResetState();
                activeWallJumpFeature?.ResetState();
                activeWallSlideFeature?.ResetState();
                SetState(PlayerTraversalState.Grounded);
                return;
            }

            var upAxis = GetUpAxis();
            var velocity = rb.linearVelocity;

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
            else if (CanProcessWallRun(CurrentState)
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
            // WallSlide中も壁沿い速度を維持するため、速度条件を満たしたらWallRunへ復帰できる。
            // Ladder/Cooldownからの直接遷移は禁止する。
            return state == PlayerTraversalState.Airborne
                || state == PlayerTraversalState.WallRun
                || state == PlayerTraversalState.WallSlide;
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
