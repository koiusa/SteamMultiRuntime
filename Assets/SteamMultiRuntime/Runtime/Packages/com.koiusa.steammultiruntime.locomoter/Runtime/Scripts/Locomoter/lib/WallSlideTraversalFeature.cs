using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(SlopeContactResolver))]
    [DisallowMultipleComponent]
    public sealed class WallSlideTraversalFeature : MonoBehaviour, IWallSlideTraversalFeature, ITraversalSettingsSync
    {
        [SerializeField] private WallSlideTraversalSettings settings;

        private SlopeContactResolver slopeContactResolver;
        private ITraversalIntentContext traversalIntentContext;
        private int wallSlideContactStreak;

        public bool IsEnabled => isActiveAndEnabled;
        public bool IsWallSliding { get; private set; }
        public Vector3 WallNormal { get; private set; }

        private void Awake()
        {
            slopeContactResolver = GetComponent<SlopeContactResolver>();
            traversalIntentContext = GetComponent<ITraversalIntentContext>();

            if (slopeContactResolver == null)
            {
                Debug.LogError("WallSlideTraversalFeature requires SlopeContactResolver component.", this);
                enabled = false;
                return;
            }

            if (IsSettingsEmpty(settings))
            {
                settings = WallSlideTraversalSettings.CreateDefault();
            }
        }

        private void OnValidate()
        {
            if (IsSettingsEmpty(settings))
            {
                settings = WallSlideTraversalSettings.CreateDefault();
            }
        }

        public void WriteSettings(ref TraversalFeatureSettings traversalSettings)
        {
            traversalSettings.WallSlide = settings;
        }

        public void ReadSettings(TraversalFeatureSettings traversalSettings)
        {
            settings = traversalSettings.WallSlide;
        }

        public void ResetState()
        {
            IsWallSliding = false;
            WallNormal = Vector3.zero;
            wallSlideContactStreak = 0;
        }

        public bool TryApplyWallSlide(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, bool isWallRunning, out Vector3 nextVelocity)
        {
            nextVelocity = velocity;
            if (traversalIntentContext != null && traversalIntentContext.HasIntent(TraversalIntentFlags.JumpRequested))
            {
                IsWallSliding = false;
                WallNormal = Vector3.zero;
                wallSlideContactStreak = 0;
                return false;
            }

            if (isWallRunning)
            {
                IsWallSliding = false;
                WallNormal = Vector3.zero;
                wallSlideContactStreak = 0;
                return false;
            }

            if (!TryGetWallNormal(upAxis, out var wallNormal))
            {
                IsWallSliding = false;
                WallNormal = Vector3.zero;
                wallSlideContactStreak = 0;
                return false;
            }

            var awaySpeed = Vector3.Dot(velocity, wallNormal);
            if (settings.WallSlideAwayFromWallMinSpeed > 0f && awaySpeed > settings.WallSlideAwayFromWallMinSpeed)
            {
                IsWallSliding = false;
                WallNormal = Vector3.zero;
                wallSlideContactStreak = 0;
                return false;
            }

            var verticalSpeed = Vector3.Dot(velocity, upAxis);
            if (verticalSpeed >= -settings.WallSlideMinDownSpeed)
            {
                IsWallSliding = false;
                WallNormal = Vector3.zero;
                wallSlideContactStreak = 0;
                return false;
            }

            var horizontalMoveDirection = Vector3.ProjectOnPlane(moveDirection, upAxis);
            if (horizontalMoveDirection.sqrMagnitude > 0.0001f)
            {
                var moveIntoWallDot = Vector3.Dot(horizontalMoveDirection.normalized, wallNormal);
                if (moveIntoWallDot >= settings.WallSlideExitMoveOppositeNormalDot)
                {
                    IsWallSliding = false;
                    WallNormal = Vector3.zero;
                    wallSlideContactStreak = 0;
                    return false;
                }
            }

            if (!IsWallSliding)
            {
                wallSlideContactStreak++;
                if (wallSlideContactStreak < Mathf.Max(1, settings.WallSlideStartContactFrames))
                {
                    return false;
                }
            }

            // Sliding only controls the vertical component. Keeping the wall-tangent
            // component lets the player steer along the wall and transition back to a run.
            var wallTangentVelocity = Vector3.ProjectOnPlane(velocity, wallNormal);
            var horizontalWallVelocity = Vector3.ProjectOnPlane(wallTangentVelocity, upAxis);
            nextVelocity = horizontalWallVelocity + upAxis * verticalSpeed;
            nextVelocity += Physics.gravity * settings.WallSlideGravityMultiplier * Time.fixedDeltaTime;
            verticalSpeed = Vector3.Dot(nextVelocity, upAxis);
            if (verticalSpeed < -settings.WallSlideMaxFallSpeed)
            {
                nextVelocity += upAxis * (-settings.WallSlideMaxFallSpeed - verticalSpeed);
            }

            IsWallSliding = true;
            WallNormal = wallNormal;
            return true;
        }

        private bool TryGetWallNormal(Vector3 upAxis, out Vector3 wallNormal)
        {
            return slopeContactResolver.TryGetObstacleNormal(upAxis, settings.WallMaxUpDot, out wallNormal);
        }

        private static bool IsSettingsEmpty(WallSlideTraversalSettings s)
        {
            return s.WallSlideGravityMultiplier == 0f && s.WallSlideMaxFallSpeed == 0f;
        }
    }
}
