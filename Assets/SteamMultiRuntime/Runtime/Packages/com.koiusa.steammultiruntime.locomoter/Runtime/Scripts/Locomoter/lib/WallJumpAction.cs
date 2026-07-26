using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(WallTraversalFeature))]
    [DisallowMultipleComponent]
    public sealed class WallJumpAction : MonoBehaviour, IWallJumpAction, ITraversalSettingsSync
    {
        [SerializeField] private WallJumpTraversalSettings settings;

        private IWallTraversalFeature wallFeature;
        private ITraversalIntentContext traversalIntentContext;
        private Vector3 lastWallKickNormal;
        private float sameWallKickLockUntilTime;
        private bool hasLastWallKick;

        private void Awake()
        {
            wallFeature = GetComponent<IWallTraversalFeature>();
            traversalIntentContext = GetComponent<ITraversalIntentContext>();

            if (wallFeature == null)
            {
                Debug.LogError("WallJumpAction requires WallTraversalFeature component.", this);
                enabled = false;
                return;
            }

            if (IsSettingsEmpty(settings))
            {
                settings = WallJumpTraversalSettings.CreateDefault();
            }
        }

        private void OnValidate()
        {
            if (IsSettingsEmpty(settings))
            {
                settings = WallJumpTraversalSettings.CreateDefault();
            }
        }

        public bool IsEnabled => isActiveAndEnabled;

        public void WriteSettings(ref TraversalFeatureSettings traversalSettings)
        {
            traversalSettings.WallJump = settings;
        }

        public void ReadSettings(TraversalFeatureSettings traversalSettings)
        {
            settings = traversalSettings.WallJump;
        }

        public void ResetState()
        {
            sameWallKickLockUntilTime = 0f;
            lastWallKickNormal = Vector3.zero;
            hasLastWallKick = false;
        }

        public bool TryWallJump(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, out Vector3 jumpVelocity)
        {
            jumpVelocity = velocity;

            // Coordinator 側の呼び出し条件と整合する防御判定
            if (traversalIntentContext != null && !traversalIntentContext.HasIntent(TraversalIntentFlags.JumpRequested))
            {
                return false;
            }

            if (!TryGetWallNormal(upAxis, out var wallNormal))
            {
                return false;
            }

            if (IsSameWallKickLocked(wallNormal))
            {
                return false;
            }

            jumpVelocity = ApplyWallJump(velocity, moveDirection, upAxis, wallNormal);
            hasLastWallKick = true;
            lastWallKickNormal = wallNormal;
            sameWallKickLockUntilTime = Time.time + settings.SameWallKickLockDuration;
            return true;
        }

        private bool TryGetWallNormal(Vector3 upAxis, out Vector3 wallNormal)
        {
            return wallFeature.TryGetWallNormal(upAxis, settings.WallMaxUpDot, out wallNormal);
        }

        private Vector3 ApplyWallJump(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, Vector3 wallNormal)
        {
            var horizontalInput = Vector3.ProjectOnPlane(moveDirection, upAxis);
            var hasInput = horizontalInput.sqrMagnitude > 0.0001f;
            if (hasInput)
            {
                horizontalInput.Normalize();
            }

            var awayDirection = Vector3.ProjectOnPlane(wallNormal, upAxis).normalized;
            if (awayDirection.sqrMagnitude <= 0.0001f)
            {
                awayDirection = wallNormal.normalized;
            }

            if (settings.WallJumpTrajectoryMode == WallJumpTrajectoryMode.Arc)
            {
                var currentVerticalSpeed = Vector3.Dot(velocity, upAxis);
                var preservedHorizontal = Vector3.ProjectOnPlane(velocity, upAxis) * 0.25f;
                var upwardVelocity = upAxis * Mathf.Max(settings.WallJumpUpForce, Mathf.Max(0f, currentVerticalSpeed));
                var awayVelocity = awayDirection * (settings.WallJumpAwayForce * 0.75f);
                var forwardVelocity = hasInput
                    ? horizontalInput * (settings.TriangleKickForwardForce * 0.6f)
                    : Vector3.zero;
                return preservedHorizontal + upwardVelocity + awayVelocity + forwardVelocity;
            }

            velocity -= Vector3.Project(velocity, upAxis);

            var nextVelocity = upAxis * settings.WallJumpUpForce + awayDirection * settings.WallJumpAwayForce;
            if (hasInput)
            {
                nextVelocity += horizontalInput * settings.TriangleKickForwardForce;
            }

            return nextVelocity;
        }

        private bool IsSameWallKickLocked(Vector3 wallNormal)
        {
            if (!hasLastWallKick)
            {
                return false;
            }

            if (Time.time > sameWallKickLockUntilTime)
            {
                return false;
            }

            return Vector3.Dot(lastWallKickNormal, wallNormal) >= settings.SameWallNormalDotThreshold;
        }

        private static bool IsSettingsEmpty(WallJumpTraversalSettings s)
        {
            return s.WallJumpUpForce == 0f && s.WallJumpAwayForce == 0f;
        }
    }
}
