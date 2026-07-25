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
            GetComponent<IWallRunTraversalFeature>()?.ResetState();
            GetComponent<IWallJumpTraversalFeature>()?.ResetState();
            GetComponent<IWallSlideTraversalFeature>()?.ResetState();
            GetComponent<ILadderTraversalFeature>()?.ResetState();
        }

        public bool HasIntent(TraversalIntentFlags flag)
        {
            return (CurrentIntentFlags & flag) == flag;
        }

        public void ApplyTraversal(Vector3 moveDirection, Vector2 moveInput, bool jumpRequested, bool isGrounded)
        {
            if (!IsEnabled)
            {
                return;
            }

            var wallRunFeature = GetComponent<IWallRunTraversalFeature>();
            var wallJumpFeature = GetComponent<IWallJumpTraversalFeature>();
            var wallSlideFeature = GetComponent<IWallSlideTraversalFeature>();
            var ladderFeature = GetComponent<ILadderTraversalFeature>();

            if (wallRunFeature != null && !wallRunFeature.IsEnabled)
            {
                wallRunFeature = null;
            }

            if (wallJumpFeature != null && !wallJumpFeature.IsEnabled)
            {
                wallJumpFeature = null;
            }

            if (wallSlideFeature != null && !wallSlideFeature.IsEnabled)
            {
                wallSlideFeature = null;
            }

            if (ladderFeature != null && !ladderFeature.IsEnabled)
            {
                ladderFeature = null;
            }

            var hasFeatureTraversal = wallRunFeature != null || wallJumpFeature != null || wallSlideFeature != null || ladderFeature != null;
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
            if (ladderFeature != null)
            {
                var upAxisForLadder = GetUpAxis();
                if (ladderFeature.TryHandleTraversal(rb.linearVelocity, moveInput, jumpRequested, isGrounded, upAxisForLadder, out var ladderVelocity, out var detachedByJump))
                {
                    if (detachedByJump)
                    {
                        // 梯子離脱直後の壁接触残りをクリアして、壁ズリ誤判定を抑える
                        wallTraversalBlockedUntilTime = Time.time + ladderFeature.WallTraversalBlockDuration;
                        slopeContactResolver?.Clear();
                        wallRunFeature?.ResetState();
                        wallJumpFeature?.ResetState();
                        wallSlideFeature?.ResetState();
                        SetState(PlayerTraversalState.Cooldown);
                    }
                    else if (ladderFeature.IsOnLadder)
                    {
                        wallTraversalBlockedUntilTime = 0f;
                        rb.linearVelocity = ladderVelocity;
                        wallRunFeature?.ResetState();
                        wallJumpFeature?.ResetState();
                        wallSlideFeature?.ResetState();
                        SetState(PlayerTraversalState.Ladder);
                    }
                    else
                    {
                        // Directional/ground detach must not reinterpret the ladder surface
                        // as a runnable wall on the following physics frame.
                        wallTraversalBlockedUntilTime = Time.time + ladderFeature.WallTraversalBlockDuration;
                        slopeContactResolver?.Clear();
                        wallRunFeature?.ResetState();
                        wallJumpFeature?.ResetState();
                        wallSlideFeature?.ResetState();
                        SetState(PlayerTraversalState.Cooldown);
                    }

                    return;
                }
            }

            if (isGrounded)
            {
                wallRunFeature?.ResetState();
                wallJumpFeature?.ResetState();
                wallSlideFeature?.ResetState();
                SetState(PlayerTraversalState.Grounded);
                return;
            }

            var upAxis = GetUpAxis();
            var velocity = rb.linearVelocity;

            if (Time.time < wallTraversalBlockedUntilTime)
            {
                wallRunFeature?.ResetState();
                wallSlideFeature?.ResetState();
                rb.linearVelocity = velocity;
                SetState(PlayerTraversalState.Cooldown);
                return;
            }

            var wallJumped = false;
            var wallRunApplied = false;
            if (jumpRequested && wallJumpFeature != null && wallJumpFeature.TryWallJump(velocity, moveDirection, upAxis, out var wallJumpVelocity))
            {
                velocity = wallJumpVelocity;
                wallJumped = true;
                wallRunFeature?.NotifyWallJump();
                wallSlideFeature?.ResetState();
                slopeContactResolver?.Clear();
                groundMotionTracker?.ClearGroundContacts();
                SetState(PlayerTraversalState.WallJump);
            }
            else if (CanProcessWallRun(CurrentState)
                && wallRunFeature != null
                && wallRunFeature.TryAccelerateOnWall(velocity, moveDirection, upAxis, out var wallVelocity))
            {
                velocity = wallVelocity;
                wallRunApplied = true;
                wallSlideFeature?.ResetState();
                SetState(PlayerTraversalState.WallRun);
            }

            if (wallRunApplied && wallRunFeature != null)
            {
                velocity = wallRunFeature.ApplyVerticalMotion(velocity, upAxis);
            }
            else if (!wallJumped && wallSlideFeature != null && wallSlideFeature.TryApplyWallSlide(velocity, moveDirection, upAxis, false, out var wallSlideVelocity))
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
            // WallSlide/Ladder/Cooldownからの直接遷移は禁止する。
            // WallRunを開始できるのは通常空中状態、または既存WallRunの継続だけ。
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
