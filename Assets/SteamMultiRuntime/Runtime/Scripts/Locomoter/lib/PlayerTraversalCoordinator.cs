using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GroundMotionTracker))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [DisallowMultipleComponent]
    public sealed class PlayerTraversalCoordinator : MonoBehaviour, IPlayerTraversalCoordinator
    {
        private Rigidbody rb;
        private GroundMotionTracker groundMotionTracker;
        private SlopeContactResolver slopeContactResolver;

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
                return (wallRunFeature != null && wallRunFeature.IsEnabled && wallRunFeature.IsWallRunning)
                    || (wallSlideFeature != null && wallSlideFeature.IsEnabled && wallSlideFeature.IsWallSliding);
            }
        }

        public void ResetState()
        {
            GetComponent<IWallRunTraversalFeature>()?.ResetState();
            GetComponent<IWallJumpTraversalFeature>()?.ResetState();
            GetComponent<IWallSlideTraversalFeature>()?.ResetState();
        }

        public void ApplyTraversal(Vector3 moveDirection, bool jumpRequested, bool isGrounded)
        {
            var wallRunFeature = GetComponent<IWallRunTraversalFeature>();
            var wallJumpFeature = GetComponent<IWallJumpTraversalFeature>();
            var wallSlideFeature = GetComponent<IWallSlideTraversalFeature>();

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

            var hasFeatureTraversal = wallRunFeature != null || wallJumpFeature != null || wallSlideFeature != null;
            if (rb == null || !hasFeatureTraversal)
            {
                return;
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
            }

            if (wallRunFeature != null && wallRunFeature.IsWallRunning && Vector3.Dot(velocity, upAxis) < 0f)
            {
                velocity = wallRunFeature.ApplyWallRunGravity(velocity, upAxis);
            }
            else if (wallSlideFeature != null && wallSlideFeature.TryApplyWallSlide(velocity, moveDirection, upAxis, wallRunFeature != null && wallRunFeature.IsWallRunning, out var wallSlideVelocity))
            {
                velocity = wallSlideVelocity;
            }

            rb.linearVelocity = velocity;
        }

        private static Vector3 GetUpAxis()
        {
            return Physics.gravity.sqrMagnitude > 0f ? -Physics.gravity.normalized : Vector3.up;
        }
    }
}
