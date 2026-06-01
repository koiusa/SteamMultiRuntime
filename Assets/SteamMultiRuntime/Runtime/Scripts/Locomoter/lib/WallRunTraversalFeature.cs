using UnityEngine;

namespace Koiusa.SteamMultiRuntime
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SlopeContactResolver))]
    [DisallowMultipleComponent]
    public sealed class WallRunTraversalFeature : MonoBehaviour, IWallRunTraversalFeature, ITraversalSettingsSync
    {
        [SerializeField] private WallRunTraversalSettings settings;

        private Rigidbody rb;
        private SlopeContactResolver slopeContactResolver;

        private int wallContactStreak;
        private bool wallRunGateClosed;

        public bool IsEnabled => isActiveAndEnabled;
        public bool IsWallRunning { get; private set; }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            slopeContactResolver = GetComponent<SlopeContactResolver>();

            if (rb == null || slopeContactResolver == null)
            {
                Debug.LogError("WallRunTraversalFeature requires Rigidbody and SlopeContactResolver components.", this);
                enabled = false;
                return;
            }

            if (IsSettingsEmpty(settings))
            {
                settings = WallRunTraversalSettings.CreateDefault();
            }
        }

        private void OnValidate()
        {
            if (IsSettingsEmpty(settings))
            {
                settings = WallRunTraversalSettings.CreateDefault();
            }
        }

        public void WriteSettings(ref TraversalFeatureSettings traversalSettings)
        {
            traversalSettings.WallRun = settings;
        }

        public void ReadSettings(TraversalFeatureSettings traversalSettings)
        {
            settings = traversalSettings.WallRun;
        }

        public void ResetState()
        {
            IsWallRunning = false;
            wallContactStreak = 0;
            wallRunGateClosed = false;
        }

        public void NotifyWallJump()
        {
            IsWallRunning = false;
            wallContactStreak = 0;
            wallRunGateClosed = true;
        }

        public bool TryAccelerateOnWall(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, out Vector3 nextVelocity)
        {
            nextVelocity = velocity;

            if (!TryGetWallNormal(upAxis, out var wallNormal))
            {
                IsWallRunning = false;
                wallContactStreak = 0;
                wallRunGateClosed = false;
                return false;
            }

            if (wallRunGateClosed)
            {
                IsWallRunning = false;
                return false;
            }

            var wasWallRunning = IsWallRunning;
            if (!MeetsWallRunConditions(moveDirection, velocity, upAxis, wallNormal))
            {
                IsWallRunning = false;
                wallContactStreak = 0;
                if (wasWallRunning)
                {
                    wallRunGateClosed = true;
                }

                return false;
            }

            if (!IsWallRunning)
            {
                wallContactStreak++;
                if (wallContactStreak < Mathf.Max(1, settings.WallRunStartContactFrames))
                {
                    return false;
                }
            }

            IsWallRunning = true;
            nextVelocity = AccelerateOnWall(velocity, moveDirection, upAxis, wallNormal);
            return true;
        }

        public Vector3 ApplyWallRunGravity(Vector3 velocity, Vector3 upAxis)
        {
            velocity += Physics.gravity * settings.WallRunGravityMultiplier * Time.fixedDeltaTime;
            var verticalSpeed = Vector3.Dot(velocity, upAxis);
            if (verticalSpeed < -settings.WallRunMaxFallSpeed)
            {
                velocity += upAxis * (-settings.WallRunMaxFallSpeed - verticalSpeed);
            }

            return velocity;
        }

        private bool MeetsWallRunConditions(Vector3 moveDirection, Vector3 velocity, Vector3 upAxis, Vector3 wallNormal)
        {
            if (IsMovingAwayFromWall(velocity, wallNormal))
            {
                return false;
            }

            if (!HasMoveInputTowardsWall(moveDirection, upAxis, wallNormal))
            {
                return false;
            }

            if (!HasEnoughAlongWallSpeed(velocity, upAxis, wallNormal))
            {
                return false;
            }

            var upwardSpeed = Vector3.Dot(velocity, upAxis);
            return upwardSpeed <= settings.WallRunMaxUpwardStartSpeed;
        }

        private bool IsMovingAwayFromWall(Vector3 velocity, Vector3 wallNormal)
        {
            if (settings.WallRunAwayFromWallMinSpeed <= 0f)
            {
                return false;
            }

            var awaySpeed = Vector3.Dot(velocity, wallNormal);
            return awaySpeed > settings.WallRunAwayFromWallMinSpeed;
        }

        private bool HasMoveInputTowardsWall(Vector3 moveDirection, Vector3 upAxis, Vector3 wallNormal)
        {
            var horizontalInput = Vector3.ProjectOnPlane(moveDirection, upAxis);
            if (horizontalInput.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            var intoWallDot = Vector3.Dot(horizontalInput.normalized, -wallNormal);
            return intoWallDot >= settings.WallRunMinInputDot;
        }

        private bool HasEnoughAlongWallSpeed(Vector3 velocity, Vector3 upAxis, Vector3 wallNormal)
        {
            var horizontalVelocity = Vector3.ProjectOnPlane(velocity, upAxis);
            var alongWallHorizontalSpeed = Vector3.ProjectOnPlane(horizontalVelocity, wallNormal).magnitude;
            return alongWallHorizontalSpeed >= settings.WallRunMinAlongWallSpeed;
        }

        private bool TryGetWallNormal(Vector3 upAxis, out Vector3 wallNormal)
        {
            return slopeContactResolver.TryGetObstacleNormal(upAxis, settings.WallMaxUpDot, out wallNormal);
        }

        private Vector3 AccelerateOnWall(Vector3 velocity, Vector3 moveDirection, Vector3 upAxis, Vector3 wallNormal)
        {
            var wallTangentVelocity = Vector3.ProjectOnPlane(velocity, wallNormal);
            var horizontalWallDirection = Vector3.ProjectOnPlane(moveDirection, upAxis);
            var fallbackDirection = Vector3.Cross(upAxis, wallNormal).normalized;
            if (fallbackDirection.sqrMagnitude <= 0.0001f)
            {
                fallbackDirection = Vector3.ProjectOnPlane(rb.rotation * Vector3.forward, upAxis).normalized;
            }

            var alongWallDirection = Vector3.ProjectOnPlane(horizontalWallDirection, wallNormal);
            if (alongWallDirection.sqrMagnitude <= 0.0001f)
            {
                alongWallDirection = fallbackDirection;
            }
            else
            {
                alongWallDirection = alongWallDirection.normalized;
            }

            var targetWallVelocity = alongWallDirection * settings.WallRunSpeed;
            var nextWallVelocity = Vector3.MoveTowards(wallTangentVelocity, targetWallVelocity, settings.WallRunAcceleration * Time.fixedDeltaTime);
            var outwardComponent = Vector3.Project(velocity, wallNormal);
            return nextWallVelocity + outwardComponent;
        }

        private static bool IsSettingsEmpty(WallRunTraversalSettings s)
        {
            return s.WallRunSpeed == 0f && s.WallRunAcceleration == 0f;
        }
    }
}
