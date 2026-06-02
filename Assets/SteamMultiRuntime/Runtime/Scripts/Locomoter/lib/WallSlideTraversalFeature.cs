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
            wallSlideContactStreak = 0;
        }

        public bool TryApplyWallSlide(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, bool isWallRunning, out Vector3 nextVelocity)
        {
            nextVelocity = velocity;
            if (traversalIntentContext != null && traversalIntentContext.HasIntent(TraversalIntentFlags.JumpRequested))
            {
                IsWallSliding = false;
                wallSlideContactStreak = 0;
                return false;
            }

            if (isWallRunning)
            {
                IsWallSliding = false;
                wallSlideContactStreak = 0;
                return false;
            }

            if (!TryGetWallNormal(upAxis, out var wallNormal))
            {
                IsWallSliding = false;
                wallSlideContactStreak = 0;
                return false;
            }

            var awaySpeed = Vector3.Dot(velocity, wallNormal);
            if (settings.WallSlideAwayFromWallMinSpeed > 0f && awaySpeed > settings.WallSlideAwayFromWallMinSpeed)
            {
                IsWallSliding = false;
                wallSlideContactStreak = 0;
                return false;
            }

            var verticalSpeed = Vector3.Dot(velocity, upAxis);
            if (verticalSpeed >= -settings.WallSlideMinDownSpeed)
            {
                IsWallSliding = false;
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

            nextVelocity = upAxis * verticalSpeed;
            nextVelocity += Physics.gravity * settings.WallSlideGravityMultiplier * Time.fixedDeltaTime;
            verticalSpeed = Vector3.Dot(nextVelocity, upAxis);
            if (verticalSpeed < -settings.WallSlideMaxFallSpeed)
            {
                nextVelocity = upAxis * -settings.WallSlideMaxFallSpeed;
            }

            IsWallSliding = true;
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
