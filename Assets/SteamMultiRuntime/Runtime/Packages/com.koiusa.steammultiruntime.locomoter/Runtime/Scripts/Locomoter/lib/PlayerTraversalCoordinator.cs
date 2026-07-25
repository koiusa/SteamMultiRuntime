using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [DisallowMultipleComponent]
    public sealed class PlayerTraversalCoordinator : MonoBehaviour, IPlayerTraversalCoordinator, ITraversalIntentContext
    {
        private const float WallTraversalBlockAfterLadderDetach = 0.3f;

        private Rigidbody rb;
        private GroundMotionTracker groundMotionTracker;
        private SlopeContactResolver slopeContactResolver;
        private float wallTraversalBlockedUntilTime;

        public TraversalIntentFlags CurrentIntentFlags { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            groundMotionTracker = GetComponent<GroundMotionTracker>();
            slopeContactResolver = GetComponent<SlopeContactResolver>();
        }

        public bool IsTraversalActive
        {
            get
            {
                var wallRunFeature = GetComponent<IWallRunTraversalFeature>();
                var wallSlideFeature = GetComponent<IWallSlideTraversalFeature>();
                var ladderFeature = GetComponent<ILadderTraversalFeature>();
                return (wallRunFeature != null && wallRunFeature.IsEnabled && wallRunFeature.IsWallRunning)
                    || (wallSlideFeature != null && wallSlideFeature.IsEnabled && wallSlideFeature.IsWallSliding)
                    || (ladderFeature != null && ladderFeature.IsEnabled && ladderFeature.IsOnLadder);
            }
        }

        public void ResetState()
        {
            CurrentIntentFlags = TraversalIntentFlags.None;
            wallTraversalBlockedUntilTime = 0f;
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
            if (rb == null || !hasFeatureTraversal)
            {
                return;
            }

            CurrentIntentFlags = BuildIntentFlags(moveInput, jumpRequested, isGrounded);

            // 梯子処理は feature 側に委譲する
            if (ladderFeature != null)
            {
                var upAxisForLadder = GetUpAxis();
                if (ladderFeature.TryHandleTraversal(rb.linearVelocity, moveInput, jumpRequested, isGrounded, upAxisForLadder, out var ladderVelocity, out var detachedByJump))
                {
                    if (detachedByJump)
                    {
                        // 梯子離脱直後の壁接触残りをクリアして、壁ズリ誤判定を抑える
                        wallTraversalBlockedUntilTime = Time.time + WallTraversalBlockAfterLadderDetach;
                        slopeContactResolver?.Clear();
                        wallRunFeature?.ResetState();
                        wallJumpFeature?.ResetState();
                        wallSlideFeature?.ResetState();
                    }
                    else if (ladderFeature.IsOnLadder)
                    {
                        wallTraversalBlockedUntilTime = 0f;
                        rb.linearVelocity = ladderVelocity;
                        wallRunFeature?.ResetState();
                        wallJumpFeature?.ResetState();
                        wallSlideFeature?.ResetState();
                    }
                    else
                    {
                        // Directional/ground detach must not reinterpret the ladder surface
                        // as a runnable wall on the following physics frame.
                        wallTraversalBlockedUntilTime = Time.time + WallTraversalBlockAfterLadderDetach;
                        slopeContactResolver?.Clear();
                        wallRunFeature?.ResetState();
                        wallJumpFeature?.ResetState();
                        wallSlideFeature?.ResetState();
                    }

                    return;
                }
            }

            if (isGrounded)
            {
                wallRunFeature?.ResetState();
                wallJumpFeature?.ResetState();
                wallSlideFeature?.ResetState();
                return;
            }

            var upAxis = GetUpAxis();
            var velocity = rb.linearVelocity;

            if (Time.time < wallTraversalBlockedUntilTime)
            {
                wallRunFeature?.ResetState();
                wallSlideFeature?.ResetState();
                rb.linearVelocity = velocity;
                return;
            }

            if (jumpRequested && wallJumpFeature != null && wallJumpFeature.TryWallJump(velocity, moveDirection, upAxis, out var wallJumpVelocity))
            {
                velocity = wallJumpVelocity;
                wallRunFeature?.NotifyWallJump();
                wallSlideFeature?.ResetState();
                slopeContactResolver?.Clear();
                groundMotionTracker?.ClearGroundContacts();
            }
            else if (wallRunFeature != null && wallRunFeature.TryAccelerateOnWall(velocity, moveDirection, upAxis, out var wallVelocity))
            {
                velocity = wallVelocity;
                wallSlideFeature?.ResetState();
            }

            if (wallRunFeature != null && wallRunFeature.IsWallRunning)
            {
                velocity = wallRunFeature.ApplyVerticalMotion(velocity, upAxis);
            }
            else if (wallSlideFeature != null && wallSlideFeature.TryApplyWallSlide(velocity, moveDirection, upAxis, wallRunFeature != null && wallRunFeature.IsWallRunning, out var wallSlideVelocity))
            {
                velocity = wallSlideVelocity;
            }

            rb.linearVelocity = velocity;
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
